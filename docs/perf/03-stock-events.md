# 03. Stock event store queries

## Question

Which Stock queries over `events_store` degrade with a hot stock and a long history, and what does the hourly read model worker (`HourlyReadModelWorker`) cost per hour?

## Setup

- Database `StockPerf` in the same SQL Server 2022 container as `OrderPerf`, created by Stock's own DbUp migrations (Stock schema only; the Messaging outbox/inbox tables were not applied in the measured run). `READ_COMMITTED_SNAPSHOT` off, as Stock never enables it. 16 schedulers.
- Seed `perf/stock/seed/events-hot-stocks.sql` (~2 min), 4.57M events:
  - 3 hot stocks: 1M events over 7 days + 360k in the last full hour (100/s);
  - 97 cold stocks: 5k + 50;
  - `version` and `occured_at` grow together; `position` follows insert order (stock by stock, not interleaved);
  - hourly and daily projections for the 7 days.
- `events_store` indexes: clustered `position`; `pk_events_store (event_id)`; `idx_event_store_version (aggregate_id, version) INCLUDE (price_change, occured_at)`; `idx_event_store_aggregate_occured_at (aggregate_id, occured_at) INCLUDE (price_change, version)`.
- Queries exactly as Dapper sends them (`sp_executesql`), on `HOT1`, second run, data in memory.

## Results

| Query | Caller | Logical reads | CPU | Verdict |
|---|---|---|---|---|
| `MAX(version) … IN (@p1) GROUP BY` | `StockEventStore.AppendAsync`, every price change | 4 | 0 ms | fine: `TOP 1` backward seek |
| `ListEventsAsync`, one page | `HourlyReadModelWorker` | ~3k | 64–317 ms | **bad**: every page scans the whole hour |
| `ListEventsAsync`, full hour | same | **6.58M** | **45 s elapsed** | 1,801 pages |
| `GetLastVersionAsync`, current hour | `HourlyReadModelWorker`, `DailyReadModelWorker` | 4 | 0 ms | fine while `@Before` is recent |
| `SumPriceChangeAsync` from `MinValue` | first hourly candle of a stock | 8.1k | 242 ms | reads all 1M history rows, once per stock |
| Hourly history, 7 days | `StockPriceHourlyReader` | 265 | 3 ms | 168 key lookups into a clustered random `id`; minor |

## ListEventsAsync

- The keyset predicate `@LastOccuredAt IS NULL OR occured_at > @LastOccuredAt OR (…)` cannot be a seek predicate: the Index Seek on `idx_event_store_aggregate_occured_at` returns all 360k rows of the hour, and a Filter drops all but the current page.
- `ORDER BY occured_at, version` has `version` only as an included column, so every page adds a Sort (Top N) and runs in parallel.
- Every page costs the same (~3.6k reads in the drain loop); only the sort shrinks towards the end (34 → 15 ms). Total per hour is pages × hour size: O(N²/200).
- The worker needs only open/high/low/close and the last version, but receives all 360k rows through 1,801 round trips.

## GetLastVersionAsync

`MAX(version) WHERE aggregate_id = @A AND occured_at < @Before` always runs as a backward scan of `idx_event_store_version` until the first row with `occured_at < @Before`. With `MAX`, the row goal is one row, and the optimizer assumes matching rows are spread evenly; they all sit at the far end, because `version` and `occured_at` are correlated.

| `@Before` | Version index (chosen) | Time index (forced) |
|---|---|---|
| current hour | 4 | — |
| 1 day back | 3,845 / 272–712 ms CPU | 6,988 |
| 7 days back | 10,352 / 719–1,336 ms CPU | 55 |

- Cost equals the events after `@Before`: cheap in normal operation, expensive when the worker lags or backfills. The two feed each other: a slower worker lags more, and lag makes this query slower.
- No index answers "max version before a time" in O(log n) unless `version` is monotonic in `occured_at`. `occured_at` comes from the request and `version` from the server, so that is not guaranteed.

## Candle in one query

`perf/stock/queries/hour-candle.sql`: running `SUM(price_change) OVER (ORDER BY occured_at, version ROWS UNBOUNDED PRECEDING)`, then `MIN`/`MAX`/first/total and `MAX(version)` in one pass. The worker adds its start price (previous close + gap) to the returned changes.

| | `ListEventsAsync` loop | Candle query |
|---|---|---|
| Round trips | 1,801 | 1 |
| Logical reads | 6.58M | 2,968 |
| Elapsed | 45 s | 228 ms |
| Rows to the service | 360,000 | 1 |

- Cost grows with the events in the hour, not with the history.
- The plan still has two Sorts (one per window function) in batch mode, ~2 s CPU on 16 threads. Candidates: one window frame for both functions (`FIRST_VALUE` instead of `ROW_NUMBER`), or `version` as a key column of the time index, which removes the Sort and its memory grant.
- `LastVersion` here is the max version inside the hour; `GetLastVersionAsync` takes the max over all events before the hour end. They differ only when versions are out of time order.

## Findings

- The hot path is fine: `AppendAsync`'s `MAX(version)` is 4 reads.
- `ListEventsAsync` is the one real problem: a catch-all keyset plus an ORDER BY outside the index key turns one hour into 1,801 full scans of that hour.
- `GetLastVersionAsync` is fragile rather than slow: cheap on the current hour, linear in lag.
- My predictions were wrong for both `MAX` queries: I expected the `GROUP BY` and the time filter to read the whole stream.

## Decision

Suggested, not applied:

1. Replace the `ListEventsAsync` loop and `GetLastVersionAsync` in `HourlyReadModelWorker` with one candle query in `StockEventStoreReader`.
2. Measure the Sort-free variant (one window frame, then `version` in the index key) before adding a migration.
3. Candle semantics (decided): order by `version`, the order Stock accepted events in, the same order the price projection applies them; bucket hours by server time (`created_at`), not the client's `occured_at`. A late event then lands in the current hour instead of rewriting a closed candle, and with time monotonic in `version` the last version of an hour becomes a `TOP 1` seek. Today both the worker and `GetLastVersionAsync` filter by `occured_at`. `created_at` defaults to `GETDATE()`; `SYSDATETIMEOFFSET()` is the correct default. No index on `(aggregate_id, created_at)` exists yet.

Longer term, an incremental candle updated on append would remove re-reading events entirely; revisit with the matcher as the price source.

## Reproduce

1. Create the database with Stock's migrations: run the service once against it, `ConnectionStrings__DefaultConnection="Server=localhost,1435;Database=StockPerf;User Id=sa;Password=…;TrustServerCertificate=True" dotnet run --project src/Services/Stock`; `DbMigrator.ApplyMigrations` runs before the host starts, stop it after the migration log.
2. `perf/stock/schema/stockperf-options.sql`, then `perf/stock/seed/events-hot-stocks.sql`.
3. Single queries: `perf/stock/queries/*.sql`, run twice; `list-events-page.sql` takes `@page`, `last-version.sql` takes `@hoursBack`.
4. Full hour: `perf/stock/queries/list-events-drain.sql`.
5. Candle: `perf/stock/queries/hour-candle.sql`.

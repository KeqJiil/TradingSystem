# 01. Outbox claim with blocked keys

## Question

How expensive is the outbox claim query (`Messaging.EFCore/Outbox/OutboxReader.GetPendingAsync`) when keys have unfinished heads, and can it be made cheaper without breaking per-key ordering?

## Setup

- SQL Server 2022 in Docker (WSL2), database `OrderPerf` with all Order migrations, Query Store in `ALL` capture mode, `READ_COMMITTED_SNAPSHOT` on (same as `OrderDb`).
- The query exactly as EF sends it: `sp_executesql`, `@p0 = 50` (BatchSize), `@p1 = 2` (LeaseMinutes), wrapped in `BEGIN TRAN … ROLLBACK`.
- Numbers are from the second run: plan cached, `physical reads = 0`. Format: logical reads / CPU.
- 1M rows in `messaging.outbox`, 200k keys.

| Scenario | Seed | Unfinished rows |
|---|---|---|
| Normal | `perf/outbox/seed/outbox-mostly-completed.sql` | 10k Pending, 10k keys |
| Hot key | `perf/outbox/seed/outbox-hot-key.sql` | 1 Processing + 99,999 Pending on one key, followed by 10k Pending on other keys |
| Kafka down | `perf/outbox/seed/outbox-kafka-down.sql` | 10k keys, each with a Processing head under an active lease + 10 Pending behind it |

## Current query

- Normal: `idx_outbox_pending` + 50 key lookups, 566–718 / 2–3 ms.
- Hot key: 402k / ~905 ms.
  - Clustered Index Scan on `pk_outbox`: row goal estimated 50 rows, but the scan walked through 890k Completed rows, ~75k reads.
  - Index Seek on `idx_outbox_unfinished_key` × 100,049 for the `NOT EXISTS` check, ~327k reads (~3.3 per seek, the index depth).
- The plan flips with the share of unfinished rows (1% → 11%): it degrades exactly when the backlog grows.
- Cost grows with the number of blocked rows that `TOP 50 ORDER BY id` has to walk on every poll.

## Attempts

1. `WITH (INDEX(idx_outbox_pending))` — 626k, worse: the index lacks `topic`/`message_key`, so every row needs a key lookup.
2. Without `NOT EXISTS` — ~75k. Only measures the share of the check; breaks per-key ordering.
3. Key heads, so each key is considered once:
   - `DISTINCT` keys + `CROSS APPLY TOP 1` per key — 61.5k / 265 ms (SELECT, hot key).
   - `o.id = (SELECT MIN(e.id) … same key)` — the optimizer builds a Stream Aggregate over `idx_outbox_unfinished_key` on its own. In the UPDATE form it falls back to a Merge Join with a `pk_outbox` scan (84k); with `OPTION (LOOP JOIN)` — 1,732 / 75 ms. File: `perf/outbox/queries/outbox-claim-heads.sql`.
   - `ROW_NUMBER` + `INCLUDE (processed_at)` on `idx_outbox_unfinished_key` — the lease is checked straight from the index. Files: `perf/outbox/schema/outbox-unfinished-key-include.sql`, `perf/outbox/queries/outbox-claim-heads-include.sql`.

## Results

With the lab `INCLUDE (processed_at)` on `idx_outbox_unfinished_key`:

| Scenario | `NOT EXISTS` (current) | `MIN` + `LOOP JOIN` | `ROW_NUMBER` + `INCLUDE` |
|---|---|---|---|
| Normal | 718 / 3 ms | 908 / 18 ms | 937 / 17 ms |
| Hot key | 402k / 922 ms | 2.0k / 71 ms | 2.2k / 112 ms |
| Kafka down (0 rows) | 403k / 927 ms | 31.8k / 93 ms | 1.2k / 149 ms |

`MIN` + `LOOP JOIN` without `INCLUDE`: 1.7k / 75 ms (hot key), 32.4k / 103 ms (Kafka down). Normal without `INCLUDE` was not measured.

## Cold cache at 5M rows

Hot-key seed with `@rows = 5000000` and `@hot = 500000` (499,999 Pending behind one stuck head, then 10k fresh Pending), lab `INCLUDE` applied. Each run starts with `CHECKPOINT; DBCC DROPCLEANBUFFERS`.

| Query | Logical reads | Read-ahead reads | CPU | Elapsed |
|---|---|---|---|---|
| `NOT EXISTS` (current) | 2,545,026 | 418,633 (~3.3 GB) | 7,205 ms | 7.9 s |
| `MIN` + `LOOP JOIN` | 8,529 | 6,447 (~50 MB) | 308 ms | 0.38 s |
| `ROW_NUMBER` + `INCLUDE` | 8,257 | 6,455 | 555 ms | 0.63 s |

- Warm, the current query is 2.54M / 6.25 s CPU / 6.9 s elapsed: almost all disk reads were read-ahead on a sequential scan, and on Docker/WSL2 the host file cache likely served them, so lab physical reads are cheaper than production disk.
- 5× the blocked rows made the current query ~6× more expensive (402k → 2.5M); the head-based query grew 2k → 8.5k, following the size of the unfinished index.
- Warm-cache numbers understate the current query's worst case: under memory pressure it pulls gigabytes per poll, while the head-based variants touch only the small filtered index.
- More realistic next step: cap `max server memory` so the table doesn't fit in the buffer pool, instead of dropping the cache before each run.

## Correctness

- Two relays (`perf/outbox/queries/outbox-claim-heads-relay-a.sql` holds its transaction open, `…-relay-b.sql` claims concurrently, hot-key data): B took the next 50 heads, `not_heads = 0`, and did not wait for A. B doesn't block because under `READ_COMMITTED_SNAPSHOT` the head search reads the last committed versions, while the claim itself (`UPDLOCK, READPAST`) still locks.
- The lease is checked on the head after it is chosen. Filtering earlier would make the second row of a key the head while its first row is still in flight.
- `READPAST` goes only on the rows being claimed, never on the head search; otherwise a locked head is skipped and the next row of the same key gets claimed.
- In the `ROW_NUMBER` variant heads are read without locks, so status and lease are re-checked on `o` under `UPDLOCK`. Without that, a row claimed and committed by another relay could be claimed again.

## Findings

- Current query: cost grows with blocked rows; it is the best in the normal case.
- Head-based variants: cost grows with unfinished rows on every batch; normal case 3 → 18 ms. Hypothesis, not measured: draining a large unblocked backlog becomes close to quadratic.
- `INCLUDE` cuts reads but not CPU: with data in memory the window function and sort dominate.
- Cold cache makes the gap wider, not narrower: at 5M rows the current query reads ~3.3 GB from disk per poll in the hot-key case.

## Decision

Keep the current query. A blocked key is bounded: `MaxAttempts = 3` with a 2-minute lease is about 6 minutes until `Dead`, and on `order-events-topic` the key is the OrderId, so there are no hot keys.

Revisit when `matcher-commands-topic` arrives (key = stock, hot keys become real). Candidate: a hybrid — the cheap current path, falling back to heads only when it runs into blocked rows. Before that, add a "large unblocked backlog" seed. The same query lives in `Messaging.Dapper/Outbox/OutboxReader.cs`.

## Reproduce

1. Create the database: `ConnectionStrings__OrderDbConnectionString="Server=localhost,1435;Database=OrderPerf;User Id=sa;Password=…;TrustServerCertificate=True" dotnet ef database update --project src/Services/Order`.
2. Run a seed from `perf/outbox/seed/`.
3. Run a query from `perf/outbox/queries/` twice and compare the second run.
4. For the `INCLUDE` variant, run `perf/outbox/schema/outbox-unfinished-key-include.sql` first.

# 04. Order reader queries

## Question

How do Order's read queries behave for a user with a long order history: the first page, a deep keyset page, a status filter, and a full walk through all pages?

## Setup

- Database `OrderPerf` in the same SQL Server 2022 container as `StockPerf`, Order's EF Core migrations up to `AddStock`, `READ_COMMITTED_SNAPSHOT` on (`perf/order/schema/orderperf-options.sql`). 16 schedulers.
- Seed `perf/order/seed/orders-hot-user.sql` (~22 s):
  - 300 stocks in `order.stock`;
  - 1 hot user with 100k orders over 365 days;
  - 10k background users × 20 orders over the same period, interleaved in time with the hot user;
  - status mix: Filled 55%, Canceled 25%, Expired 9%, Rejected 8%, New 2%, PartiallyFilled 0.5%, PendingNew 0.3%, PendingCancel 0.2%; active orders spread over the whole year, the pessimistic case for a rare status;
  - 1–5 fills per order with a filled quantity, ~595k fills;
  - ids are Guid v7 built from `created_at`. SQL Server orders `uniqueidentifier` by its last 6 bytes, which are random in v7, so the hot user's rows are spread over the clustered index as in production.
- Indexes before: clustered `pk_order (id)`; `order_user_id_idx (user_id, created_at, id)` DESC; `order_status_partial_idx (status)` filtered on active statuses; `fill`: clustered identity `id`, `IX_fill_order_id`, unique `fill_execution_id_idx`.
- Queries exactly as EF Core sends them (`sp_executesql`), captured from the real `OrderReader` and repositories, page size 20, data in memory.

## Results

| Query | Caller | Logical reads | Verdict |
|---|---|---|---|
| `GetOrdersByUserIdAsync`, first page | `GetListByUserId` | 261 (order 71, fill 150, stock 40) | **wasteful**: 57% of reads load fills the list never returns |
| same, page 2,500 | same | 277 | keyset is fine: two seek ranges, cost does not grow with depth |
| same, all 5,000 pages | same | ~490 per page in the drain loop, flat | 17.0 s for 5,000 round trips |
| same, `status = PartiallyFilled` | same | **12,142–15,018** | **bad**: no index on `(user_id, status)`, plan depends on the first call |
| `GetOrderByIdAsync` | `GetById` | 14 (order 3, stock 2, fill 9) | fine |
| `OrderRepository.GetByIdAsync` | every command handler | 12 (order 3, fill 9) | fine |
| `StockRepository.GetByIdAsync` | `PlaceOrder` | — | PK seek on 300 rows, not measured |

## Fills in the list

- `OrderReader` projected with `.Select(x => MapToOrderDto(x.o, x.StockName))`. EF Core cannot see inside a C# method, so it materializes the whole aggregate `x.o`, including the owned `Fills` collection.
- The SQL becomes a `TOP(@p)` subquery with all 19 order columns, an outer `LEFT JOIN [order].[fill]`, and an extra sort key `stock_id0`. 20 orders return ~46 rows.
- The fill join is 150 of 261 reads: 20 seeks on `IX_fill_order_id` plus key lookups into the clustered `fill` index for every fill.
- The keyset predicate `created_at < @a OR (created_at = @a AND id < @id)` is not a problem: it runs as two seek ranges (scan count 2), and the drain shows the same reads on page 1 and page 5,000.

## Status filter

- `status` is only a residual predicate after the seek on `user_id`. EF Core sends the same text for every status, so the plan compiled for the first value is reused for all of them.
- Compiled for a rare status, the optimizer picks a parallel scan of the whole clustered index. Compiled for a common one, it walks `order_user_id_idx` with key lookups until it finds 20 matches, ~4,000 orders for PartiallyFilled.
- `order_status_partial_idx` does not help: SQL Server cannot match a filtered index to a parameterized predicate at compile time. No other query uses it.

`perf/order/queries/orders-list-status-sniffing.sql`, logical reads including the loop's temp table and compilation, `small` is a background user with 20 orders:

| Step | Plan compiled for | User | Status | Without new index | With `order_user_status_idx` |
|---|---|---|---|---|---|
| 1 | PartiallyFilled | hot | PartiallyFilled | 16,776 | 291 |
| 2 | PartiallyFilled | hot | Filled | **34,758** | 191 |
| 3 | PartiallyFilled | small | Filled | **15,712** | 104 |
| 4 | Filled | hot | Filled | 307 | 250 |
| 5 | Filled | hot | PartiallyFilled | **12,740** | 191 |
| 6 | Filled | small | PartiallyFilled | 66 | 3 |
| 7 | — | hot | none | 247 | 247 |

- Step 3: a user with 20 orders pays 15.7k reads because someone else filtered by a rare status first after a restart.
- In a trading UI the worst case is the common one: "my open orders" is the main filter, and open orders are a tiny share of the history.
- `OPTIMIZE FOR` or `OPTION (RECOMPILE)` would only choose who pays: with no `(user_id, status)` access path, a rare status costs 12–15k reads under any plan. EF Core also has no API for query hints, only a command interceptor.

## After

| | Before | After |
|---|---|---|
| First page | 261 | 111 (order 71, stock 40) |
| Page 2,500 | 277 | 114 |
| Status filter, any status, any page | 123–15,018 | 111–115 |
| All 5,000 pages (drain loop) | 2.45M reads, 17.0 s | 0.94M reads, 4.8 s |
| Rows to the service per page | ~46 | 20 |

- The remaining 71 reads on `order` are 20 key lookups from the user index into the clustered index (~3 reads each).

## Findings

- The keyset pagination was already correct: constant cost at any depth.
- Loading the aggregate for a DTO was the main waste on the unfiltered list; it is a LINQ shape issue, not an index issue.
- The status filter was the real problem: up to 35k reads per page, and the plan of the first caller decided the cost for everyone.
- Write cost of the new index was not measured: it moves a row only when `status` changes, 3–4 times per order lifecycle; fills that keep the status do not touch it.

## Decision

Applied:

1. `OrderReader.GetOrdersByUserIdAsync` projects `OrderDto` inline in `Select`; `MapToOrderDto` stays for the details query, which needs the fills.
2. Migration `AddOrderUserStatusIndex`: add `order_user_status_idx (user_id, status, created_at, id)` DESC, drop `order_status_partial_idx`.

Not applied:

- `INCLUDE` of the list columns in both user indexes would remove the 20 key lookups (~60 of 111 reads) at the cost of wider indexes on every write. Revisit if the list becomes hot.

## Reproduce

1. Apply Order's migrations up to `AddStock` to `OrderPerf` (`dotnet ef migrations script` and run it in SSMS), then `perf/order/schema/orderperf-options.sql`.
2. `perf/order/seed/orders-hot-user.sql`.
3. One page: `perf/order/queries/orders-list-page.sql` (before) and `orders-list-page-projection.sql` (after); both take `@page` and `@status`.
4. All pages: `orders-list-drain.sql` and `orders-list-drain-projection.sql`; both take `@status`.
5. Details and aggregate load: `order-details.sql`.
6. Status index: `perf/order/schema/order-user-status-idx.sql`; switch indexes off and on with `order-indexes-toggle.sql`, then run `orders-list-status-sniffing.sql`.

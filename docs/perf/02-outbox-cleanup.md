# 02. Outbox cleanup

## Question

How does the outbox cleanup (`Messaging.EFCore/Outbox/OutboxCleanser.cs`) behave on a large backlog of expired rows, what batch size is safe for concurrent writers, and does deleting from the front of the table get slower over time?

## Setup

- Same lab as [01](01-outbox-claim.md): `OrderPerf`, SQL Server 2022 in Docker, `READ_COMMITTED_SNAPSHOT` on, 16 schedulers.
- Seed `perf/seed/outbox-mostly-completed.sql`: 1M rows, one row per second, 990k Completed + 10k Pending. ~395k Completed rows are past the 7-day retention.
- The statement as reconstructed from `ExecuteDeleteAsync` (not yet checked against an EF log):

  ```sql
  DELETE TOP(@p0) FROM [o]
  FROM [messaging].[outbox] AS [o]
  WHERE [o].[status] = 'Completed' AND [o].[created_at] < @p1
  ```

## Single batch

- Plan: Index Seek on `idx_outbox_completed` (`created_at < @p1`, ordered forward) → Clustered Index Delete on `pk_outbox` + `idx_outbox_completed`.
- The filtered `idx_outbox_pending` and `idx_outbox_unfinished_key` are not maintained: Completed rows are outside their filters.
- ~9 logical reads per row in a rolled-back batch of 200: each row is a root-to-leaf traversal in two B-trees. Nothing to remove without changing the approach (e.g. partition switching).

## Lock escalation

- Each deleted row takes two `KEY X` locks, one per index it lives in.
- Escalation is attempted at ~5,000 locks per statement, so the limit is ~2.5k rows per batch.
- At 25k rows (`perf/queries/outbox-cleanup-hold.sql`, transaction left open) the session held no `KEY` locks, only `OBJECT X` — 16 of them, one per lock partition (`cpu_count = 16`). An exclusive table lock is taken on every partition; `IS`/`IX` go to the local partition only.
- While it was held, another cleanup batch waited with `LCK_M_IX` on `OBJECT: 7:…:6` (`perf/queries/blocking.sql`). The relay claim and Order's `INSERT` also need `IX`, so they stop too. `READPAST` skips locked rows, not table locks; RCSI only helps plain reads.

## Committed loop

`perf/queries/outbox-cleanup-loop.sql` repeats the batch with autocommit until a batch is short, as `CleanupAsync` does, and logs reads and time per batch.

| Batch | Batches | Rows | Logical reads | Reads / row | Elapsed |
|---|---|---|---|---|---|
| 200 (current) | 1,713 | 342,533 | 4.68M | 13.7 | 49.0 s |
| 2000 | 198 | 395,284 | 2.88M | 7.3 | 16.7 s |

The 200 run had fewer rows because earlier test batches had already been committed.

- Reads per batch stay flat across the run (by tenths: ~2.7k for 200, ~14–15k for 2000). Deleting from the front of the index does not get slower.
- Two points fit ~6.5 reads per row plus ~1.4k per batch. The fixed part is why bigger batches are cheaper per row; its cause was not isolated.
- Ghost records after the 2000 run: 353k in `pk_outbox`, ~40k in `idx_outbox_completed`. Cleanup outpaces the ghost cleanup task on the clustered index, but deletes reach `pk_outbox` by key seeks, so those ghosts cost space, not reads. Ghosts in the index that is scanned stay bounded.

## Findings

- The single-batch plan is already optimal.
- The batch size is bounded by lock escalation, which turns cleanup into a table-wide writer stop.
- Within that bound, larger batches are ~3× faster: a fixed per-batch cost and fewer commits.
- No ghost-record degradation over a full drain.

## Decision

Suggested, not applied: `OutboxBatchSize` 200 → 2000 (`Messaging.EFCore/CleanupOptions.cs`, `Messaging.Dapper/CleansionOptions.cs`). 2000 rows = 4,000 key locks, under the threshold. The headroom depends on the number of indexes holding Completed rows: a third one would cut the safe batch to ~1,600.

## Reproduce

1. Run `perf/seed/outbox-mostly-completed.sql`.
2. Escalation: `perf/queries/outbox-cleanup-hold.sql` with `@batch` set, then `perf/queries/locks-held.sql` and `perf/queries/blocking.sql` from other sessions; `ROLLBACK` in the first one.
3. Loop: `perf/queries/outbox-cleanup-loop.sql` with `@batch` set; reseed between runs, the loop deletes for real.

# TradingSystem

.NET 10 microservices over Kafka. Solution: `TradingSystem.slnx`, packages pinned centrally in `Directory.Packages.props`.

## Layout

- `src/Services/Stock` — Dapper + DbUp, event store, read models.
- `src/Services/Order` — EF Core, DDD aggregate, MediatR; consumes matcher reports, publishes order events. gRPC only, no HTTP API.
- `src/Services/Messaging*` — in-repo messaging library (Kafka, outbox/inbox, Dapper and EF Core backends, `Messaging.Testing` for tests).
- `src/Contracts` — proto contracts. Schema-first: `.proto` is the source of truth; Order classes are generated at build (`ProtobufClasses/Order/*.proto`).

## Commands

- Build: `dotnet build TradingSystem.slnx`
- Tests: `dotnet test src/Services/Order.Tests` — integration tests use Testcontainers, so Docker must be running.
- Without Docker (Order): `dotnet test src/Services/Order.Tests --filter "FullyQualifiedName~Domain|FullyQualifiedName~Translator"`
- Run everything: `docker compose up --build` (needs `.env`; Aspire dashboard via `--profile observability`). Migrations apply on service start.

## Conventions

- Handlers return `Result`/`Result<T>`; callers map `ErrorType` to outcomes (Kafka `ConsumeOutcome`, later gRPC status). No exceptions for expected failures, no exception filters.
- No explanatory comments. Keep changes minimal; no polish on CRUD code.
- Tests: real dependencies only (`MssqlFixture`, real Persistence implementations) — no Moq, no hand-rolled fakes.
- Test names: `Method_Condition_Expected`. Scenarios are often drafted as comments in empty test classes; implement each one, and flag vague or contradicting expectations instead of fitting the test to the code.
- Services never read `messaging.outbox`/`inbox` directly; verify publishing through `Messaging.Testing` (`Published.OfTypeAsync<T>`) or Kafka.
- Order uses Guid v7 PKs and per-order ordering (Kafka key = OrderId); don't re-propose changing these.

## Collaboration

- Proto contracts and `Messaging.EFCore` are written by the owner: give hints and review there, not full implementations, unless asked.
- `docs/adr` is abandoned and may be stale.
- Suggest improvements if you see any, but don’t apply them without asking.
- Don't commit changes.
- Do changes step by step instead of doing a lot at once.

## Architecture

- Clean architecture in most of the services.
- DDD in services where we have certain domain and invariants.
- Shouldn't be any god classes.

# TradingSystem

[![CI](https://github.com/KeqJiil/TradingSystem/actions/workflows/ci.yml/badge.svg)](https://github.com/KeqJiil/TradingSystem/actions/workflows/ci.yml)

A learning/pet project implementing a trading platform as a set of independent microservices. The goal is to practice patterns actually used in high-load distributed systems: event sourcing, the outbox pattern, saga orchestration, CQRS, and reliable messaging between services.

> The project is under active development. Below is an honest status of what's done, what's in progress, and what's planned.

## Architecture (target)

The system is made up of 5 services:

| Service | Responsibility | Status |
|---|---|---|
| **Stock** | Stock metadata, price, price history, OHLC candles (hourly/daily) | 🟢 Feature-complete |
| **Order** | Accepting and executing buy/sell orders | 🟡 In progress (domain model) |
| **Portfolio** | User positions, balance, portfolio value aggregation | ⚪ Planned |
| **Saga** | Orchestrates distributed transactions across Order/Portfolio | ⚪ Planned |
| **Identity** | User authentication and authorization | ⚪ Planned |

Services communicate asynchronously over Kafka, using the outbox pattern to guarantee event delivery without losing consistency between the database and the message broker.

```mermaid
flowchart LR
    Client([Client])

    subgraph Sync["Synchronous (HTTP)"]
        Identity[("Identity")]
        Order[("Order")]
        Portfolio[("Portfolio")]
        Stock[("Stock")]
    end

    Saga[("Saga\n(orchestrator)")]
    Kafka{{"Kafka"}}

    Client -->|"auth"| Identity
    Client -->|"place order"| Order
    Client -->|"view positions"| Portfolio
    Client -->|"quotes / metadata"| Stock

    Order -- outbox --> Kafka
    Portfolio -- outbox --> Kafka
    Stock -- outbox --> Kafka

    Kafka -- events --> Saga
    Saga -- commands --> Order
    Saga -- commands --> Portfolio

    style Saga fill:#f9dfae,stroke:#b8860b
    style Sync fill:transparent,stroke:#999,stroke-dasharray: 4 3
```

Each service owns its own database and publishes domain events through its outbox; the Saga service subscribes to these events and drives cross-service workflows (e.g. *place order → reserve funds → update portfolio → confirm*), including compensating actions on partial failure.

Stock is not a saga participant: it publishes price and metadata events, and Order / Saga keep local replicas of them (tentative).

## Tech stack

- **.NET 10**, C#, minimal APIs
- **MediatR** — CQRS, with validation, tracing and resilience pipeline behaviours
- **FluentValidation** — command and query validation
- **Kafka** (Confluent.Kafka) — asynchronous event exchange, retry and dead-letter topics
- **MS SQL Server** + **Dapper** — Stock persistence and event store
- **Entity Framework Core** — Order persistence
- **dbup** — versioned SQL migrations
- **Hangfire** — cron jobs (candles, outbox cleanup)
- **Polly** — whole-transaction retry on transient SQL errors
- **protobuf-net** — binary serialization of events between services
- **OpenTelemetry** — traces, metrics and logs (Aspire dashboard locally)
- **xUnit** + **Testcontainers** — integration tests against real SQL Server and Kafka
- **Docker Compose** — local infrastructure setup

## Stock service

Stock is the first service in the system. It's where the patterns that will be reused across the other services are being worked out.

- **Event sourcing for price only** — `events_store` is an append-only log of price deltas; metadata (name, currency, trading hours, open-to-trade flag) is state-based CRUD with a monotonic `metadata_version`.
- **Transactional outbox** — the change and its outgoing message are written in one transaction through the shared [Messaging library](#messaging-library) (Dapper backend).
- **CQRS with an asynchronous read model** — Stock consumes its own topics to build `stock_data_projection`: per-field last-writer-wins for metadata, version checks for price, with gaps filled inline by replaying `events_store`.
- **OHLC candles** — hourly and daily candles by event time, computed by Hangfire cron jobs and immutable once written.
- **Idempotency without an inbox** — domain versions and natural keys make redelivery safe.
- **Observability** — correlation id alongside W3C trace context across HTTP, outbox, Kafka and Hangfire; health checks at `/health/live` and `/health/ready`.
- **Unit of Work** — optimistic concurrency and retry of the whole transaction on transient SQL errors.
- **HTTP API** under `/api/v1/stocks` — idempotent creation (`PUT /{id}`), name / trading hours / open-to-trade changes, metadata, current price and candles. Requests are in [Stock.http](src/Services/Stock/Stock.http).
- **Versioned migrations** — 15 sequential dbup scripts in [Migrations](src/Services/Stock/Infrastructure/Persistence/Migrations).

## Messaging library

A shared library in `src/Services/Messaging*` that every service uses for reliable Kafka messaging.

- **Messaging** — Kafka producer and consumer host, topic registration, delivery and publish middleware pipelines (logging, tracing, exception handling), retries and dead-letter topics, outbox relay.
- **Messaging.Abstractions** — consumer, middleware and serializer contracts.
- **Messaging.Dapper** — outbox, inbox, Unit of Work with transient-error retry, cleanup jobs and migrations for the `messaging` schema.
- **Messaging.EFCore** — the same outbox / inbox / cleanup on top of EF Core.
- **Messaging.Testing** — SQL Server and Kafka fixtures and helpers for asserting published messages in service tests.

Event contracts are schema-first protobuf files in [src/Contracts](src/Contracts).

## Order service

In progress. The domain model is done and covered by unit tests:

- `Order` aggregate with the lifecycle *PendingNew → New → PartiallyFilled → Filled*, plus reject, cancel request / reject, cancel and expire transitions.
- Value objects `Price`, `Quantity`, `OrderTerms` (market / limit, time in force) and the `Fill` entity.
- `ApplyFillSpecification` — fill validation against approval, limit price, remaining quantity and status.
- Domain events for every transition, with results instead of exceptions.

Persistence, HTTP API and messaging are next.

## What's planned

- [x] **Schema-first protobuf contracts** — event contracts in [src/Contracts](src/Contracts).
- [x] **Shared Messaging library** — outbox, inbox, Kafka consumer host and topology for all services; Stock migrated to it.
- [ ] **Order service** — persistence, HTTP API and messaging on top of the finished domain model, with a matching engine as the future price source.
- [ ] **Portfolio service** — position and portfolio value calculation based on executed orders.
- [ ] **Saga service** — orchestration of the distributed *place order → reserve funds → update portfolio* transaction, with partial-failure handling and compensating actions.
- [ ] **Identity** — user authentication (leaning towards an off-the-shelf solution like Keycloak instead of a custom-built service).
- [ ] End-to-end tests for cross-service scenarios.

## Running locally

Create a `.env` file next to `compose.yaml`:

```dotenv
MSSQL_STOCK_PASSWORD=<sa password>
ConnectionStrings__DefaultConnection=Server=stock-db,1433;Database=StockDb;User Id=sa;Password=<sa password>;TrustServerCertificate=True
```

```bash
docker compose up -d
```

The Stock API is on `http://localhost:8081`. Add `--profile observability` to also start the Aspire dashboard on `http://localhost:18888`.

## Tests

Tests start SQL Server and Kafka in containers, so Docker must be running.

```bash
dotnet test TradingSystem.slnx
```

# Implementation Plan

## Requirement Analysis

The assessment requires an end-to-end wager ingestion and analytics system:

1. `POST /api/player/casinowager` accepts the supplied wager JSON and publishes it to a message bus.
2. A separate .NET worker consumes published wagers and persists them to SQL Server.
3. `GET /api/player/{playerId}/casino?pageSize=10&page=1` returns that account's latest wagers with the specified pagination metadata.
4. `GET /api/player/topSpenders?count=10` returns accounts ranked by the sum of their wager amounts.
5. `DatabaseGenerate.sql` must recreate the database objects, and `Notes.md` must explain setup, decisions, test results, and production considerations.
6. Under the supplied tester's workload (7,000 POSTs at 500 every two seconds), all accepted wagers must eventually be stored, both read endpoints must remain usable during ingestion, and stored records must reconcile with submitted records.

## Current Baseline

- The solution targets .NET 10 and has API, Consumer, Application, Domain, Infrastructure, and Shared projects.
- The API still contains the template weather endpoint; the consumer worker only logs periodically.
- Application, Domain, Infrastructure, and Shared contain no implementation yet.
- `DatabaseGenerate.sql` and `Notes.md` are empty.
- `tests/OT.Assessment.Tests` contains a Bogus/NBomber load-test console app and its configuration. It is not a unit-test project.

## Proposed Design

- Use RabbitMQ as the message bus and SQL Server as the database, matching the README's examples and local setup guidance.
- Keep HTTP contracts and shared message definitions in Shared; business models and persistence/bus abstractions in Domain/Application; concrete SQL and RabbitMQ adapters in Infrastructure. Keep API and Consumer as separate hosts.
- Treat `WagerId` as the idempotency key. A repeated delivery must not create a second stored wager.
- Persist an account record keyed by `AccountId` and wager records referencing it. Keep the fields needed for wager history and aggregation: wager ID, account ID, username, game, provider, amount, and event timestamp.
- Use a durable queue and persistent messages. The API should report success only after the broker confirms publication. The consumer should acknowledge a message only after its database transaction commits; configure bounded retry and a dead-letter path for messages that cannot be processed.
- Use deterministic ordering for latest wagers (event time descending, then wager ID) and validate pagination/count inputs with documented limits.
- Aggregate top spenders by account and rank by total amount descending, with a deterministic tie-breaker.

## Implementation Phases

### 1. Contracts and Application Use Cases

- Define the wager request/message and response DTOs, preserving the JSON field names used by the tester.
- Add validation for required IDs/text, amount, and date fields.
- Define application interfaces and use cases for publishing wagers, persisting consumed wagers, listing wager history, and retrieving top spenders.

### 2. Database and Infrastructure

- Design `DatabaseGenerate.sql` to create the database, account and wager tables, constraints, and indexes.
- Add a unique constraint on wager ID for idempotent writes; index account and event timestamp for history queries and include appropriate columns to support the top-spender aggregation.
- Implement SQL Server persistence and transactional account upsert/wager insert behavior.
- Implement RabbitMQ connection, durable queue/exchange configuration, publishing confirmation, consumer acknowledgements, retry policy, and dead-letter handling.
- Add configuration binding and explicit startup errors for missing or invalid database/broker settings.

### 3. Consumer Service

- Replace the template worker with a long-running RabbitMQ consumer.
- Persist each wager (or a bounded batch) transactionally before acknowledging its message(s).
- Ensure duplicate delivery is safe, cancellation shuts down cleanly, and processing failures are logged and routed through retry/dead-letter behavior rather than silently discarded.

### 4. API

- Replace the weather endpoint with the required POST and GET routes.
- Return an accepted response from POST only after confirmed publication; return clear client errors for invalid input and service errors when publication cannot be confirmed.
- Implement paginated history with accurate total/page metadata and top-spender results with the required response shapes.
- Keep database reads asynchronous and ensure parameters are bounded and query results consistently ordered.
- Configure NSwag to generate the API's Swagger document and serve Swagger UI at `/swagger` in development; serve the document at `/swagger/v1/swagger.json`.

### 5. Tests and Documentation

- Add focused tests for request validation, JSON compatibility, pagination boundaries/order, top-spender aggregation/order, and duplicate wager processing.
- Add integration coverage for SQL persistence and the publish/consume flow where local infrastructure is available.
- Document RabbitMQ and SQL Server setup, configuration, startup order, schema recreation, and known trade-offs in `Notes.md`.
- Align README instructions with the actual `tests/OT.Assessment.Tests` load-tester path and required endpoint configuration.

### 6. End-to-End Load Verification

- Start SQL Server, RabbitMQ, API, and consumer using documented configuration.
- Run the supplied 7,000-request tester against the exact POST route.
- Confirm history and top-spender endpoints remain responsive during ingestion.
- Wait for the queue to drain, then reconcile accepted/submitted wager IDs against stored unique wager IDs and verify no loss or duplicate storage.
- Record response failures, consumer/dead-letter errors, processing completion time, and observed endpoint behavior in `Notes.md`.

## Acceptance Criteria

- All three required endpoints match the request and response shapes in the README.
- Swagger UI documents all three endpoints and loads its document from `/swagger/v1/swagger.json`.
- Every successfully accepted wager is eventually stored exactly once, including under duplicate delivery.
- History pagination and top-spender totals/order are correct.
- `DatabaseGenerate.sql` recreates the required schema from scratch.
- The supplied load test completes with no unexplained data loss, and both GET endpoints remain usable during the run.
- Setup, configuration, design decisions, and measured load-test results are documented.

## Confirmation Gate

No application code, schema, tests, or runtime configuration will be implemented until this plan is approved. The plan currently assumes RabbitMQ and SQL Server; confirm the plan as written or specify any changes before implementation.

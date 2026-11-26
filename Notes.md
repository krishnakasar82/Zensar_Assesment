# Design and Setup Notes

## Architecture

- The API publishes wagers to a durable RabbitMQ exchange and returns `202 Accepted` only after broker publisher confirmation.
- The Consumer acknowledges a wager only after the SQL transaction commits. Database writes are idempotent by `WagerId`; transient processing failures are retried through a TTL retry queue and eventually sent to a dead-letter queue.
- SQL Server stores player accounts and wagers separately. Account IDs are the player key, and wager IDs are globally unique. History is ordered by event time and wager ID; top spenders are ranked by summed wager amounts.
- The API, consumer, application use cases, shared message contract, and SQL/RabbitMQ adapters are kept in separate projects.

## Local Setup

1. Run SQL Server and execute `DatabaseGenerate.sql` from a SQL Server client (for example, `sqlcmd -S localhost -E -i .\DatabaseGenerate.sql`). The script creates the database if needed and adds missing tables/indexes without dropping existing data.
2. Run RabbitMQ locally with the `guest` account, or change the `RabbitMq` section in both `src/OT.Assessment.Api/appsettings.json` and `src/OT.Assessment.Consumer/appsettings.json`.
3. Change `ConnectionStrings:DefaultConnection` in both settings files if SQL Server is not using the local integrated-security configuration.
4. Start the API with `dotnet run --project .\src\OT.Assessment.Api\OT.Assessment.Api.csproj`.
5. Start the consumer in another terminal with `dotnet run --project .\src\OT.Assessment.Consumer\OT.Assessment.Consumer.csproj`.
6. Set `Api:EndpointUri` in `tests/OT.Assessment.Tests/appsettings.json` to the full wager POST URL, then run `dotnet run --project .\tests\OT.Assessment.Tests\OT.Assessment.Tests.csproj`.

The API creates the durable RabbitMQ exchange and queues when it first connects. SQL schema creation is explicit and is not run automatically by either service.

## Reliability and Data Decisions

- The API does not claim successful acceptance when RabbitMQ is unavailable; it returns a Problem Details `503`.
- The consumer uses manual acknowledgements, a bounded retry count, and a dead-letter queue. Inspect the dead-letter queue after failures; messages are not silently discarded.
- Duplicate deliveries are safe because `WagerId` is the primary key and writes check for an existing wager inside a SQL transaction.
- `Players.Username` represents the most recently observed username by wager event time. Stored wager history keeps the game, provider, amount, and event timestamp needed by the required queries.
- The SQL amount is stored as `decimal(28, 17)` to retain the precision of the supplied wager amounts without using floating-point arithmetic for aggregation.
- The default SQL connection string uses integrated security and is intended for local development. Use secret management or environment variables for non-local credentials.

## Verification Record

The full solution build passed with zero warnings and errors, and the focused unit tests passed. On 2026-11-26, the schema script was applied to the configured local SQL Server (`localhost`); verification found both required tables and both supporting indexes, with no wager rows present. A subsequent repeatability check could not reconnect because the local SQL client reported a pre-login handshake/resource error. The 7,000-request load test has not been run; run it with SQL Server and RabbitMQ available, then record measured results here:

Additional runtime verification: the API started successfully in Development, and both Swagger UI (`/swagger`) and the Swagger document (`/swagger/v1/swagger.json`) returned HTTP 200.

- Load-test date/environment: Not run
- Accepted submissions:
- Stored unique wagers after the queue drained:
- Dead-lettered or failed messages:
- Time from tester completion to queue drain:
- History/top-spender availability during ingestion:

## Production Considerations

- Configure RabbitMQ authentication/TLS, SQL credentials through secret management, health checks, and operational alerts for queue depth and dead-letter volume.
- Consider batching database writes if profiling shows persistence is the throughput bottleneck; retain commit-before-ack semantics.
- Define retention and privacy policies for player data and ensure a monitoring/replay procedure exists for dead-lettered messages.
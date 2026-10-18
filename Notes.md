# Notes

## Overview

Players' casino bets are captured by an API, queued in RabbitMQ, and stored in SQL Server by a background Consumer. Two read endpoints return a player's bets and the top spenders.

```
Tester -> POST api/player/casinowager -> API -> RabbitMQ (casino-bets) -> Consumer -> SQL Server
                                                                                         ^
                       GET api/player/{playerId}/casino, GET api/player/topSpenders -----+
```

| Project | Purpose |
|---|---|
| `OT.Assessment.App` | The Web API: accepts a bet, publishes it, serves the two GET endpoints |
| `OT.Assessment.Consumer` | Background service: reads the queue, writes batches to SQL Server |
| `OT.Assessment.Common` | Shared message, response and settings classes |
| `OT.Assessment.UnitTests` | xUnit tests for the controller and message contract |
| `OT.Assessment.Tester` | The provided load tester (not changed) |

## Setup

Requirements: .NET 8 SDK, Docker (for RabbitMQ), SQL Server (local instance, Windows authentication).

1. **RabbitMQ:** run `docker\RabbitMQ\docker-up.bat` (it runs `docker compose up -d`). Management UI at http://localhost:15672, login `guest` / `guest` (local defaults only); metrics on port 15692.
2. **Database:** run `DatabaseGenerate.sql` (for example `sqlcmd -S localhost -E -C -b -i DatabaseGenerate.sql`). It creates `OT_Assessment_DB` with the tables, indexes, table type and stored procedures. It is safe to re-run: every object is guarded, and nothing is dropped or overwritten except the stored procedures, which are replaced with the script's version. To start from empty data, drop the database first.
3. **Connection strings:** both `appsettings.json` files use `localhost` with Windows authentication and `TrustServerCertificate=True` (local development). Change them if your SQL Server is elsewhere. No passwords are stored in the repository.
4. **Start the API:** `dotnet run --launch-profile https` in `src/OT.Assessment.App` (https://localhost:7120, Swagger at `/swagger`).
5. **Start the Consumer:** `dotnet run` in `src/OT.Assessment.Consumer`.
6. **Run the Tester:** `dotnet run -c Release` in `test/OT.Assessment.Tester`.
7. **Unit tests:** `dotnet test` in the repository root.

## Design choices

- **Queue between the API and the database.** The API only validates and publishes, so a slow or unavailable database never slows or breaks bet capture. The Consumer can be restarted or scaled on its own.
- **Reliable publishing.** The queue is durable, messages are persistent, and the API waits for the broker's publisher confirm before returning 200. So 200 means the broker has the bet. If RabbitMQ is down the API returns 503, not a false success.
- **Batched, idempotent writes.** The Consumer collects up to 500 messages or 200 ms and writes them with one stored procedure call (table-valued parameter, one transaction), then acknowledges the whole batch. Bets with a BetId that already exists are skipped, so redelivery after a failure cannot create duplicates.
- **Dead-letter queue.** Messages that can never succeed (bad JSON, data that violates a constraint) go to `casino-bets.dead` instead of blocking the queue. Temporary failures (for example the database being down) are retried and the messages are requeued.
- **Database design.** Tables: `Player`, `Game`, `CasinoBet`, `PlayerSpendTotal`. `PlayerSpendTotal` keeps a running total per player, so top spenders reads a small table instead of summing all bets. Money is `DECIMAL(19,4)`, time is `DATETIMEOFFSET`, keys are GUIDs. An index on `CasinoBet (AccountId, CreatedDateTime DESC)` serves the player's-bets query, and one on the total serves top spenders. Paging uses `OFFSET/FETCH` with `COUNT(*) OVER()` for the total.
- **Data access.** Dapper with stored procedures: little code, parameterised, no ORM overhead.
- **Ids and time come from the payload (enhancement made with AI assistance).** My first version created the bet id and the created time on the server. After reviewing the design with AI assistance I changed it so the caller supplies both, and the API only validates them. This makes retries detectable (the same id is recognised and stored once) and keeps the original time of the bet. The change was done test-first (three new unit tests).
- **Logging to files.** The API and the Consumer log to the console and to daily files under `logs/` in their own project folder, with information, warning and error (including fatal) entries in separate folders (`logs/info/info-20261018.log`, `logs/warning/...`, `logs/error/...`). The setup is one small shared class (`FileLogging` in the Common project) using Serilog; framework chatter below warning level is left out so the info files stay readable. The `logs/` folders are git-ignored. The behaviour is covered by unit tests.
- **Structure.** Interfaces for the publisher, reader and writer, registered through dependency injection. Settings use the Options pattern. Naming follows one style throughout (`usp_` for procedures, `bet` rather than `wager`).

## Bonus: monitoring, alerts and searchable logs

Everything below is optional: the assessment runs without it. Each part has its own folder and compose file, so start only what you need (Elasticsearch and Kibana need about 2 GB of free memory).

| Part | Start | Open |
|---|---|---|
| RabbitMQ (with metrics) | `docker\RabbitMQ\docker-up.bat` | http://localhost:15672 (guest / guest) |
| Prometheus and Grafana | `docker\monitoring\docker-up.bat` | Grafana http://localhost:3000 (admin / admin, local only), Prometheus http://localhost:9090/targets |
| Elasticsearch, Kibana, Filebeat | `docker\elk\docker-up.bat`, then once `docker\elk\kibana-setup.bat` | Kibana http://localhost:5601/app/discover |

- **Metrics.** The API (`https://localhost:7120/metrics`) and the Consumer (`http://localhost:9101/metrics`) expose Prometheus metrics: process CPU, memory and garbage collection, requests per endpoint, and our own counters (`ot_api_bets_accepted_total`, `ot_api_publish_duration_seconds`, `ot_consumer_bets_saved_total`, `ot_consumer_batch_duration_seconds`, `ot_consumer_bets_dead_lettered_total`, and more). RabbitMQ's own Prometheus plugin adds queue depth per queue (including the dead-letter queue), connections and memory. The apps create their metrics at startup, so dashboards show 0 instead of "No data" before the first bet.
- **Dashboard.** "OT Assessment - bets pipeline" in Grafana (16 panels): status of RabbitMQ, API and Consumer, queue depth, bets per second, time to queue and to save, CPU, memory, requests by status code, dead-lettered, requeued and retried messages. It is provisioned from files, so no clicking is needed.
- **Alerts** (Grafana, evaluated every 30 seconds, shown in Grafana's alert list): dead-letter queue has messages; queue backlog above 5000 for 2 minutes; API, Consumer or RabbitMQ not reporting for 1 minute; the API could not queue bets; the Consumer is retrying database saves; queueing a bet slower than 10 seconds (p95) for 2 minutes; an app above 1.5 GB of memory for 5 minutes. I checked them by putting a test message into the dead-letter queue (the alert fired and cleared after I removed it) and by stopping the apps (the "down" alerts fired). No notification channel is configured, because email or chat needs real credentials that must not be committed.
- **Logs in Kibana.** Filebeat reads the daily files in `src/OT.Assessment.App/logs` and `src/OT.Assessment.Consumer/logs` (info, warning and error folders), turns each line into time, level and text (a stack trace stays with its error line) and stores it in Elasticsearch as `ot-logs-<date>`. In Kibana the data view `ot-logs-*` can be searched by service (`service`), level (`log_level`: INF, WRN, ERR) and folder (`log_folder`). I checked that all 74 lines from my test runs arrived with the correct counts per service and level.
- **Local-only choices (not for production):** security is off in Elasticsearch, Grafana uses admin / admin, Elasticsearch has a 512 MB heap, and Prometheus reaches the apps through `host.docker.internal` (the apps run on the host).

## Endpoints

| Endpoint | Behaviour |
|---|---|
| `POST api/player/casinowager` | Validates the bet and queues it. 200 OK, 400 if fields are missing or invalid, 503 if the broker is unavailable. |
| `GET api/player/{playerId}/casino?page=1&pageSize=10` | A player's bets, newest first, paged. `pageSize` 1-100, 400 otherwise. A non-GUID id returns 404. |
| `GET api/player/topSpenders?count=10` | Top players by total amount spent, highest first. `count` 1-100, 400 otherwise. |

## Test results

Provided Tester, one run on a freshly created database (7,000 requests, 500 every 2 seconds):

- Tester: 7,000 OK, 0 failed, about 30 s, about 233 requests per second.
- RabbitMQ: queue fully drained, dead-letter queue empty.
- Database: **6,772 bets stored**, 998 players, 979 games, 998 spend totals.
- GET endpoints: checked after the run and **during** a second run. Every call returned 200; response time rose from about 25 ms to at most about 0.7 s under load and then recovered.
- Unit tests: 51 passing (controller and message contract, written test-first).

**Why 6,772 and not 7,000:** the Tester prepares 10,000 payloads with unique ids but sends `total[context.InvocationNumber]`, and under its load pattern some payloads are sent twice and others never. Logging every Consumer batch showed that the batches contained repeated BetIds, and that every distinct BetId was stored exactly once. The Consumer is idempotent by design, so the repeats are stored once. No acknowledged bet was lost. The Tester itself was not changed. Every Tester run creates new bet ids, so reset the database before a clean measurement.

## How I tested, step by step

**A. Unit tests**
1. `dotnet test` in the repository root runs 51 xUnit tests.
2. They cover the POST endpoint (valid bet, optional fields missing, tiny amounts, every required field missing or invalid, broker failure returns 503, client cancellation is not turned into a 503), both GET endpoints (paging limits, empty results, bad input) and the message contract.
3. The tests use small hand-written fakes for the publisher and the reader, so they need no RabbitMQ or database. They were written test-first: each new test was seen failing before the code was changed.

**B. Manual checks of the API**
1. Start RabbitMQ, SQL Server, the API and the Consumer.
2. In Swagger (`https://localhost:7120/swagger`) send `POST api/player/casinowager` with a sample bet. Expect 200, the message visible in the RabbitMQ management UI, and the row in `CasinoBet` a moment later.
3. Send a bet with a missing field. Expect 400 and nothing queued.
4. Stop RabbitMQ and send a bet. Expect 503 (not a false 200). Start RabbitMQ again and send another bet. Expect 200 (the API reconnects on its own).
5. Call both GET endpoints with the stored data: a known player id with `page` and `pageSize`, and `topSpenders` with a `count`. Check the order (newest first, highest total first). Try invalid input (`pageSize=0`, a non-GUID id) and check the 400 and 404 responses.

**C. Consumer behaviour (tested in a separate scratch copy, so the real database stayed clean)**
1. Publish bad JSON and a message that violates a database constraint. Expect both in `casino-bets.dead`, with the main queue still moving.
2. Stop SQL Server while messages are queued. Expect retries, the messages put back on the queue, and nothing lost. Start SQL Server again and check that every bet is stored.
3. Check idempotency: after the redelivery above, the number of distinct bet ids equals the number of rows, with no duplicates.

**D. Load test with the provided Tester (clean run)**
1. Stop all running projects.
2. Reset the database: drop `OT_Assessment_DB` and re-run `DatabaseGenerate.sql`. Check that `SELECT COUNT(*) FROM CasinoBet` is 0.
3. Start the API, then the Consumer (wait for "Connected to RabbitMQ, reading queue casino-bets").
4. Run the Tester once (`dotnet run -c Release` in `test/OT.Assessment.Tester`). Record the OK and failed counts, the duration and the latency.
5. Wait until the queue is empty: in the RabbitMQ management API (`/api/queues/%2F/casino-bets`) both `messages_ready` and `messages_unacknowledged` must be 0. Check that `casino-bets.dead` has 0 messages.
6. Only then count the rows: `SELECT COUNT(*) FROM CasinoBet` (a count taken while the Consumer is still working is too low). Also count players, games and spend totals.

**E. Finding where bets go missing (6,772 stored of 7,000 sent)**
1. Checked the usual suspects: nothing in the dead-letter queue, no failed requests, the queue fully acknowledged.
2. Ran a separate copy of the stack (own queues, own database, own API port) so my real data was not touched.
3. Added a temporary log of every Consumer batch: messages in the batch, distinct BetIds in it, and rows the database actually gained.
4. Result: the batch sizes added up to exactly 7,000 messages, and the rows gained added up to the final table count, so every distinct BetId was stored once. Many batches contained repeated BetIds.
5. Read the Tester's code: it sends `total[context.InvocationNumber]` and some payloads are sent twice and others never. The idempotent insert correctly stores a repeated bet once.
6. Proved it directly: in the separate copy I logged every BetId at the API for one Tester run. The API received 7,000 requests with 6,782 distinct BetIds (206 ids arrived more than once, one of them 5 times), every repeat was an identical payload, and the database held exactly 6,782 rows. So the stored rows equal the distinct bets received, and the Consumer lost nothing.

**F. GET endpoints during load**
1. Start the Tester (clean setup as in D, or a second run on the same database).
2. At the same time run a one-second loop that calls both GET endpoints with `curl` and prints the status code and response time.
3. Result: every call returned 200. Response time was about 25 ms before the load, at most about 0.7 s at the peak, and recovered afterwards.

**G. Re-running the database script**
1. Run `DatabaseGenerate.sql` a second time on a database that already has every object. Expect exit code 0, no errors, no data lost.
2. Do it twice, then check that all tables, indexes, the table type and the procedures are still there.

## Challenges

- **Stored count lower than the Tester's count.** Investigated with a separate copy of the stack (own queues and database) and a per-batch log of messages versus rows gained. Cause: repeated payloads in the Tester, handled correctly by the idempotent insert (see above).
- **SQL Server certificate error on the GET endpoints.** The current SQL client encrypts by default; local development uses `TrustServerCertificate=True`.
- **Windows locking the running API/Consumer files** when rebuilding while they ran. Stop the running copy before building.
- **Docker Desktop not starting** after a WSL problem; fixed by restarting WSL and Docker.
- **A 404 from the Tester at first**: the route name did not match the Tester's `casinowager`. Restored the route.

## Known limits and production improvements

- **Throughput of the publish path.** The API publishes one message at a time and waits for each confirm, so under 500 requests per second requests queue up and latency reaches about 30 s. For production: a pool of channels, or batched confirms, plus a bounded in-flight limit. I kept the simple version because it keeps "200 means safely queued" easy to reason about.
- **Validation:** add length limits on text fields (database column sizes) and return 400 with details.
- **Operations:** health checks (API, RabbitMQ, database), correlation ids in the logs, central log collection (instead of local files) with retention and clean-up of old files, metrics and alerts (queue depth, dead-letter count), rate limiting.
- **Security:** no default `guest` credentials, secrets from a vault or environment variables, TLS to RabbitMQ and SQL Server with a trusted certificate, authentication on the API.
- **Dead-letter handling:** a tool or job to inspect and replay dead-lettered messages.
- **Scale:** several Consumer instances on the same queue; partitioning or archiving old bets; read replicas for the GET endpoints.
- **Schema changes:** versioned migrations instead of a re-runnable script (the script checks that objects exist, not that they match).
- **Delivery:** Docker Compose for SQL Server and for the two applications themselves (RabbitMQ and the monitoring and log stacks already have one), a CI pipeline running the build and the tests.
- **Monitoring in production:** a managed Prometheus/Grafana (or the cloud provider's equivalent), alert notifications to email, chat or an on-call tool (the local setup only shows alerts in Grafana), Elasticsearch with security, replicas and retention, instead of the single-node local one.


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

## Bonus: extras added beyond the brief

The brief's last item asks us to "surprise" with valuable additions. These were added on top of the required work. The required part (API, Consumer, database) does not depend on any of them.

- **Monitoring system (Prometheus and Grafana).** The API and the Consumer expose metrics, RabbitMQ exposes queue metrics, and a ready-made Grafana dashboard (17 panels) shows: whether RabbitMQ, the API and the Consumer are up, queue depth and the dead-letter queue, bets per second, time to queue and to save, **CPU and memory of the API, the Consumer and RabbitMQ**, request counts, and saved versus repeated bets.
- **Alerts.** Nine Grafana alert rules, for example dead-letter queue not empty, queue backlog, API / Consumer / RabbitMQ down, publish failures, slow publishing and high memory. I tested them by triggering the dead-letter alert and by stopping the apps.
- **Searchable logs (ELK: Elasticsearch, Kibana and Filebeat).** The logs of both apps can be searched by service, level and folder in Kibana.
- **File logging.** Both apps write daily log files, with information, warning and error entries in separate folders.
- **One-command RabbitMQ (Docker Compose)** with its metrics plugin on. The README already linked to `docker/RabbitMQ/docker-up.bat`; that file now exists.
- **Reliability.** Publisher confirms, a dead-letter queue for messages that can never be saved, retries for temporary database problems, and idempotent inserts (a repeated bet is stored once and counted as "skipped").
- **Numbers that add up.** The dashboard and the Consumer log show how many messages were processed, how many became new rows (equal to the table) and how many were repeats skipped. So Tester count = API accepted = RabbitMQ received = saved + skipped, and saved = rows in the table. Checked in a full run: 7,000 = 6,599 saved + 401 skipped, and 6,599 rows.
- **A database script that is safe to re-run**, 107 unit tests written test-first, and a start script for RabbitMQ (`docker\RabbitMQ\docker-up.bat`) plus a start and a stop script for each of the monitoring and log parts (`docker-up.bat` and `docker-down.bat` in `docker\monitoring` and `docker\elk`).

How to start and use the monitoring and log parts is described in "Bonus: monitoring, alerts and searchable logs" below.

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
- **Publishing over several channels.** One channel sends one bet at a time (it waits for the broker's confirm for each bet), so with a single channel the API was the slow part. The API now publishes over 16 channels (setting `RabbitMq:PublishChannels`), each with its own lock, so many bets are in flight at once and every 200 still means the broker has the bet. I found this by measuring: the same Tester run on a scratch copy with 1, 8 and 16 channels gave a mean latency of about 30 s, 18 s and under 1 s. The channels are handed to requests only after all of them exist and the queues are declared, so a request cannot publish before the queue exists.
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

- **Metrics.** The API (`https://localhost:7120/metrics`) and the Consumer (`http://localhost:9101/metrics`) expose Prometheus metrics: process CPU, memory and garbage collection, requests per endpoint, and our own counters (`ot_api_bets_accepted_total`, `ot_api_publish_duration_seconds`, `ot_consumer_bets_saved_total`, `ot_consumer_bets_skipped_total`, `ot_consumer_batch_duration_seconds`, `ot_consumer_bets_dead_lettered_total`, and more). `bets_saved` counts rows really added to the table and `bets_skipped` counts repeated bets (a BetId that is already stored), so after a run: messages processed = saved + skipped, and saved equals the rows in `CasinoBet`. RabbitMQ's own Prometheus plugin adds queue depth per queue (including the dead-letter queue), connections and memory. The apps create their metrics at startup, so dashboards show 0 instead of "No data" before the first bet.
- **Dashboard.** "OT Assessment - bets pipeline" in Grafana (17 panels): status of RabbitMQ, API and Consumer, queue depth, bets per second, time to queue and to save, CPU, memory, requests by status code, dead-lettered, requeued and retried messages. It is provisioned from files, so no clicking is needed.
- **Alerts** (Grafana, evaluated every 30 seconds, shown in Grafana's alert list): dead-letter queue has messages; queue backlog above 5000 for 2 minutes; API, Consumer or RabbitMQ not reporting for 1 minute; the API could not queue bets; the Consumer is retrying database saves; queueing a bet slower than 10 seconds (p95) for 2 minutes; an app above 1.5 GB of memory for 5 minutes. I checked them by putting a test message into the dead-letter queue (the alert fired and cleared after I removed it) and by stopping the apps (the "down" alerts fired). No notification channel is configured, because email or chat needs real credentials that must not be committed.
- **Logs in Kibana.** Filebeat reads the daily files in `src/OT.Assessment.App/logs` and `src/OT.Assessment.Consumer/logs` (info, warning and error folders), turns each line into time, level and text (a stack trace stays with its error line) and stores it in Elasticsearch as `ot-logs-<date>`. In Kibana the data view `ot-logs-*` can be searched by service (`service`), level (`log_level`: INF, WRN, ERR) and folder (`log_folder`). I checked that all 74 lines from my test runs arrived with the correct counts per service and level.
- **Local-only choices (not for production):** security is off in Elasticsearch, Grafana uses admin / admin, Elasticsearch has a 512 MB heap, and Prometheus reaches the apps through `host.docker.internal` (the apps run on the host).

## Endpoints

| Endpoint | Behaviour |
|---|---|
| `POST api/player/casinowager` | Validates the bet and queues it. 200 OK, 400 if fields are missing or invalid, 503 if the broker is unavailable. |
| `GET api/player/{playerId}/casino?page=1&pageSize=10` | A player's bets, newest first, paged. `pageSize` 1-100, 400 otherwise. A non-GUID id returns 404. |
| `GET api/player/topSpenders?count=10` | Top players by total amount spent, highest first. `count` 1-100, 400 otherwise. |

## Bonus: web UI, login and admin portal

A small React UI was added on top of the finished API, on its own branch (`feature/ui-login-admin-endpoints`). **Nothing that existed before was changed:** `PlayerController`, the three endpoints, the Consumer, the existing stored procedures and `DatabaseGenerate.sql` are as they were. The additions are new files, new SQL objects in a separate script, and a few lines in `Program.cs` and `appsettings.json` (service registration, CORS, the admin login).

**What the user sees**

- **Login page.** Players log in or create an account; the admin logs in on the same form.
- **Player portal.** Choose a game (real radio inputs, shown as tiles with a search box and paging), pick a stake with chips or type an amount, place the bet (the existing `POST api/player/casinowager`), and see "My bets" (the existing `GET api/player/{id}/casino`).
- **Admin portal.** *Players* (every account that can log in, with a search box and "View bets"), *Top spenders* (the existing `GET api/player/topSpenders`, with "View bets" per player) and *Tests* (runs the unit tests or the provided load Tester and shows the output).

**New endpoints** (each in its own controller, none touches the old ones)

| Endpoint | Controller | Behaviour |
|---|---|---|
| `POST api/auth/register` | `AuthController` | Creates a player with a login name and password. 400 for missing fields or a password under 4 characters, 409 if the name is taken. |
| `POST api/auth/login` | `AuthController` | Returns the role (`player` or `admin`) and the account id. The admin also gets a token. 401 for a wrong login. |
| `GET api/games` | `GamesController` | The games a player can choose from. |
| `GET api/admin/players?search=&count=` | `AdminController` | Players that have a login, with their total spent. Needs the admin token (`X-Admin-Token`). 401 without it, 400 if `count` is not 1-100. |
| `POST api/admin/tests/{unit\|load}` | `AdminController` | Runs `dotnet test` or the Tester as a child process and returns pass/fail, seconds and output. Needs the admin token. 409 if a run is already going. |

**Database changes** (all in `DatabaseUiSupport.sql`, run after `DatabaseGenerate.sql`, safe to re-run)

- Two **nullable** columns on `Player`: `LoginName` and `PasswordHash`. `Username` already existed and is the display name, so the login name is a separate column. Existing rows and the Consumer's inserts are unaffected, because the Consumer lists its columns explicitly and never updates an existing player.
- Passwords are stored as a salted PBKDF2 hash (`PasswordHasher`), never as plain text.
- Five real games are added so the player has something to choose before any bet has been saved, and four small stored procedures (`usp_RegisterPlayer`, `usp_GetPlayerLogin`, `usp_GetGames`, `usp_GetRegisteredPlayers`) serve the new endpoints.
- **A mistake found and fixed while testing:** the first version of the script put a *unique filtered index* on `LoginName`. SQL Server needs `QUOTED_IDENTIFIER ON` for any write to a table with a filtered index, and the Consumer's existing procedure does not use it, so saving bets failed with error 1934. This showed up in a smoke test (a bet was accepted but never saved). The index was replaced by a plain one, and uniqueness of the login name is now checked inside `usp_RegisterPlayer` in one locked transaction. The script also drops the old index if it is there.

**Simple on purpose**

- The admin logs in with a name and password from `appsettings.json` (`Admin` section, `admin` / `admin`, **local development only**). The login returns a random token that is kept in memory (lost on restart) and checked by the admin endpoints. There are no JWTs, roles or refresh tokens; the existing GET endpoints stay open, as before.
- The test runner builds into a separate temporary folder (`--artifacts-path`), so running the tests never overwrites the files of the API that is running. Only one run at a time.
- For the demo, every player that the Tester created earlier was given a login: login name = the existing username, password = the first name followed by `$1234` (for example `Eloise_Glover` / `Eloise$1234`). This was done once with a throw-away tool that is not part of the repository. These passwords are guessable and are for a local test database only.

**What to install (once)**

Everything in "Setup" (.NET 8 SDK, Docker, SQL Server) plus:

| Needed | Why | Check |
|---|---|---|
| **Node.js 18 or newer** (built and tested with Node 22; includes `npm`) from https://nodejs.org | Runs the React dev server and installs the UI's packages | `node -v` and `npm -v` |
| A modern browser (Chrome, Edge or Firefox) | The UI uses current CSS | |
| The .NET HTTPS development certificate, trusted | The UI calls the API on https://localhost:7120 | `dotnet dev-certs https --trust` |

The UI's own packages are installed by `npm install` (React 18.3.1, React DOM, Vite 5.4.11 and the Vite React plugin; versions are pinned in `ui/package.json`). There are no other libraries and nothing to install globally.

**SQL steps for the UI (run once, before starting the API)**

The UI needs the objects from `DatabaseUiSupport.sql`. Without them login, register, the games list and the admin Players tab fail. SQL Server must be running and `OT_Assessment_DB` must already exist (`DatabaseGenerate.sql`, see "Setup").

1. **Run `DatabaseGenerate.sql` first** if you have not already:
   ```
   sqlcmd -S localhost -E -C -b -i DatabaseGenerate.sql
   ```
2. **Run `DatabaseUiSupport.sql`**, from the repository root. Use either way:
   - **Command line:**
     ```
     sqlcmd -S localhost -E -C -b -i DatabaseUiSupport.sql
     ```
     `-S localhost` is the server, `-E` uses your Windows login, `-C` trusts the local certificate, `-b` stops on the first error, `-i` is the script file. No output and no error means success. To check the exit code, run `echo %ERRORLEVEL%` (cmd) or `$LASTEXITCODE` (PowerShell); it must be `0`.
   - **SQL Server Management Studio (SSMS):** File > Open > File, choose `DatabaseUiSupport.sql`, make sure the database drop-down shows `OT_Assessment_DB` (the script also starts with `USE OT_Assessment_DB`), then press **Execute** (F5). The Messages tab must show no red errors.
3. **Check that it worked.** Run this (in SSMS, or with `sqlcmd -S localhost -E -C -d OT_Assessment_DB -Q "..."`):
   ```sql
   SELECT COLUMN_NAME, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_NAME = 'Player' AND COLUMN_NAME IN ('LoginName', 'PasswordHash');   -- 2 rows, both YES

   SELECT name FROM sys.procedures
   WHERE name IN ('usp_RegisterPlayer', 'usp_GetPlayerLogin', 'usp_GetGames', 'usp_GetRegisteredPlayers');   -- 4 rows

   SELECT COUNT(*) AS RealGames FROM Game
   WHERE Name IN ('Starburst', 'Book of Dead', 'Gonzo''s Quest', 'Sweet Bonanza', 'Mega Moolah');   -- 5
   ```
   Then start the API and open https://localhost:7120/api/games: it must return a list of games.
4. **What the script does (and does not do).** It adds two nullable columns (`LoginName`, `PasswordHash`) and a plain index on `Player`, five real games, and four stored procedures (`usp_RegisterPlayer`, `usp_GetPlayerLogin`, `usp_GetGames`, `usp_GetRegisteredPlayers`). It never drops data, never edits an existing row, and does not touch `DatabaseGenerate.sql`'s tables, procedures or the Consumer's insert. It is safe to run any number of times: every step checks first, and the procedures are replaced with the script's version.
5. **Look at the logins** (optional): `SELECT TOP 10 LoginName, LEFT(PasswordHash, 12) AS HashStart FROM Player WHERE LoginName IS NOT NULL;`. A new player created with "Create an account" appears here with a hash, never the plain password.
6. **Starting again from empty data.** Dropping `OT_Assessment_DB` and re-running `DatabaseGenerate.sql` removes the login columns, the five games and every login. Run `DatabaseUiSupport.sql` again afterwards, then register players through the UI. Players created by the Tester have no login, so they cannot log in and are not listed on the admin Players tab (they still show in Top spenders).

| SQL problem | Cause and fix |
|---|---|
| `Cannot open database "OT_Assessment_DB"` or `Invalid object name 'Player'` | `DatabaseGenerate.sql` was not run. Run it first. |
| `Login timeout expired` or `A network-related error` | SQL Server is not running. Start the service `MSSQLSERVER` (Services app) or the SQL Server instance you use, and check the `-S` name. |
| `certificate chain was issued by an authority that is not trusted` | Add `-C` to `sqlcmd` (local development only). |
| `'sqlcmd' is not recognized` | The SQL command-line tools are not installed. Install "sqlcmd" or use SSMS to run the script. |
| `Error 1934 ... QUOTED_IDENTIFIER` when a bet is saved | An old version of the script created a filtered index. Run the current `DatabaseUiSupport.sql`: it drops that index. |

**How to run**

1. Start RabbitMQ (`docker\RabbitMQ\docker-up.bat`), then the API (`dotnet run --launch-profile https` in `src\OT.Assessment.App`) and the Consumer (`dotnet run` in `src\OT.Assessment.Consumer`), as described in "Setup". Without the Consumer a bet is accepted but never appears in "My bets".
2. Run `DatabaseUiSupport.sql` once, after `DatabaseGenerate.sql`: `sqlcmd -S localhost -E -C -b -i DatabaseUiSupport.sql`.
3. Install and start the UI. In a new terminal:
   ```
   cd ui
   npm install
   npm run dev
   ```
   `npm install` is needed only the first time (or after `package.json` changes).
4. Open **http://localhost:5173**. The port is fixed on purpose: the API only allows calls from http://localhost:5173 and http://localhost:3000 (`Cors:AllowedOrigins` in `appsettings.json`).
5. Log in. Admin: `admin` / `admin`. Player: create one with "New player? Create an account", or use any seeded player, for example `Walter33` / `Walter$1234` (password = first name + `$1234`).

The API address defaults to https://localhost:7120. To point the UI elsewhere, set `VITE_API_URL` (for example `set VITE_API_URL=https://localhost:7120` before `npm run dev`) and add the UI's address to `Cors:AllowedOrigins`.

`npm run build` makes a production build in `ui/dist` (it checks that the UI compiles); the dev server is the supported way to run it for this assessment.

**If something does not work**

| What you see | Cause and fix |
|---|---|
| "Failed to fetch" on the login page | The API is not running, or the browser does not trust its certificate. Start the API, run `dotnet dev-certs https --trust`, and open https://localhost:7120/swagger once. |
| `npm: command not found` | Node.js is not installed, or the terminal was opened before installing it. Install Node.js and open a new terminal. |
| "Port 5173 is already in use" | Another copy of the UI is running. Close it. The port must stay 5173 for CORS. |
| The games list is empty | `DatabaseUiSupport.sql` was not run, or the API cannot reach SQL Server. |
| A bet is accepted but not in "My bets" | The Consumer is not running, or RabbitMQ is down (the API answers 503 in that case). Start both, then press Refresh. |
| The admin pages show an error after the API restarted | The admin token lives in memory and is lost on restart. Log out and log in again. |
| The admin test buttons fail | The API must run from `src\OT.Assessment.App` (it finds the solution two folders up), or set `Admin:SolutionPath` in `appsettings.json`. The load test also needs RabbitMQ, the Consumer and the database. |

**Testing**

- 40 new unit tests (hashing, register and login, the games list, the admin endpoints and their 401 / 400 / 409 cases); the full suite is **147 passing**.
- Checked by hand against the real stack: register, duplicate register (409), login, wrong password (401), admin login, admin endpoints without a token (401), the unit tests started from the admin portal, CORS from the UI's address, a bet placed as a player and saved by the Consumer, and the player's "My bets" and the admin pages showing it.
- **Not covered:** the React screens have no automated tests (they were checked by eye and with screenshots), and the admin "Run load test" button was not run from the portal because it adds about 7,000 bets to the database.

## Test results

Provided Tester, final runs on a freshly created database, with the API, the Consumer, RabbitMQ and the monitoring stack running (7,000 requests, injected by the Tester at 500 every 2 seconds, which takes about 30 seconds). Latest run:

| Stage | Count |
|---|---|
| Tester: requests answered 200 / failed | 7,000 / 0 |
| API: accepted / rejected / could not queue | 7,000 / 0 / 0 |
| RabbitMQ: received / acknowledged | 7,000 / 7,000 |
| Consumer: **saved as new rows** + **repeats skipped** | **6,837** + **163** = 7,000 |
| Rows in `CasinoBet` | **6,837** (equals "saved") |
| Players / games / spend totals | 998 / 979 / 998 |
| Queue and dead-letter queue at the end | empty, nothing requeued |

- **Speed:** the Tester run finished in about 42 s, with a mean request latency of 3.0 s (min 0.2 s, max 6.8 s). The run before it gave 7.9 s mean and 10.6 s max. Before the API published over several channels the same test took 71 to 103 s with a mean latency of 20 to 50 s. I also checked that the per-request logging and the metrics are not the cause of any slowness: the same test against the code from before they were added gave the same latency (40 s and 44 s, against 39 s for the current code at that time).
- **Reading the Tester's summary:** the "RPS = 233 / 250" in it is its injection schedule (7,000 requests over about 30 s), not the speed of the API; the latency figures are the useful ones.
- **CPU and memory at the peak of the load:** API about 130 to 170 percent of one core (it now uses several cores) and about 270 to 290 MB, Consumer about 17 to 21 percent and 85 to 91 MB, RabbitMQ about 17 to 23 percent and about 200 to 211 MB. Up to 4,000 to 6,000 bets wait in the queue at the peak before the Consumer catches up.
- **GET endpoints:** checked after the run and **during** an earlier run. Every call returned 200; response time rose from about 25 ms to at most about 0.7 s under load and then recovered. The responses use exactly the field names of the brief (`wagerId, game, provider, amount, createdDate`; `data, page, pageSize, total, totalPages`; `accountId, username, totalAmountSpend`).
- **Unit tests:** 107 passing (controller, message and response contracts, SQL error rule, channel selection, logging and metrics, written test-first).
- **Alerts:** "Consumer is retrying database saves" lit up once, as designed, after a run in which the Consumer had to retry a database save one time; nothing else fired. The dead-letter and "down" alerts were tested separately.
- **Checked on the real stack for the channel pool:** a cold start with no queues and 300 requests arriving at once (all 300 messages arrived in the queue), and a RabbitMQ outage (the API answered 503 while it was down and recovered about 36 s after it was back, rebuilding all 16 channels).

**Why the table has fewer rows than the Tester sends:** the Tester prepares 10,000 payloads with unique ids but sends `total[context.InvocationNumber]`, and under its load pattern some payloads are sent twice and others never (the share differs from run to run: 217 and 401 repeats in two runs). The Consumer is idempotent by design, so a repeated BetId is stored once and counted as "skipped"; the dashboard and the Consumer log show both numbers, so saved + skipped always equals what the API accepted. No acknowledged bet was lost. The Tester itself was not changed. Every Tester run creates new bet ids, so reset the database before a clean measurement.

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

**E. Finding where bets go missing (an earlier run: 6,772 stored of 7,000 sent)**
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

**H. Speed of the publish path**
1. The Tester's mean latency was 20 to 50 s. I first suspected the new logging and metrics, so I ran the same Tester run on a scratch copy of the code from before they existed: same latency, so they were not the cause.
2. I then changed a scratch copy to publish over 1, 8 and 16 channels (a setting read from an environment variable) and ran the same Tester run each time: mean latency about 30 s, 18 s and under 1 s. The few failures in those runs came from a start-up race in my experiment code (a request could use the channels before they were all created), not from the real code, and were fixed by only handing out the channels once they are all ready.
3. The real code now uses a pool of 16 channels (setting `PublishChannels`), checked with two full runs (all counts reconcile), a cold start with no queues, and an outage with recovery.

## Challenges

- **Stored count lower than the Tester's count.** Investigated with a separate copy of the stack (own queues and database) and a per-batch log of messages versus rows gained. Cause: repeated payloads in the Tester, handled correctly by the idempotent insert (see above).
- **SQL Server certificate error on the GET endpoints.** The current SQL client encrypts by default; local development uses `TrustServerCertificate=True`.
- **Windows locking the running API/Consumer files** when rebuilding while they ran. Stop the running copy before building.
- **Docker Desktop not starting** after a WSL problem; fixed by restarting WSL and Docker.
- **A 404 from the Tester at first**: the route name did not match the Tester's `casinowager`. Restored the route.
- **The API was the slow part, not the Consumer.** Found by measuring (see H above), after ruling out my own first guess (logging and metrics).
- **The response field `betId` instead of `wagerId`** in the player-bets endpoint was found when re-reading the brief; fixed and covered by tests that pin the exact field names.
- **A start-up race in the publisher:** the channel could be used by other requests before the queues were declared. Fixed by handing out the channels only when everything is ready.

## Known limits and production improvements

- **Throughput of the publish path.** The API now publishes over 16 channels, which cut the mean latency under the Tester's load from 20-50 s to about 3-8 s on this laptop. The API still uses a lot of CPU and up to a few thousand bets wait in the queue at the peak. For production: tune the number of channels to the machine, batch the confirms, put a limit on bets in flight, and run several Consumer instances on the same queue.
- **Validation:** add length limits on text fields (database column sizes) and return 400 with details.
- **Operations:** health checks (API, RabbitMQ, database), correlation ids in the logs, central log collection (instead of local files) with retention and clean-up of old files, metrics and alerts (queue depth, dead-letter count), rate limiting.
- **Security:** no default `guest` credentials, secrets from a vault or environment variables, TLS to RabbitMQ and SQL Server with a trusted certificate, authentication on the API.
- **Dead-letter handling:** a tool or job to inspect and replay dead-lettered messages.
- **Scale:** several Consumer instances on the same queue; partitioning or archiving old bets; read replicas for the GET endpoints.
- **Schema changes:** versioned migrations instead of a re-runnable script (the script checks that objects exist, not that they match).
- **Delivery:** Docker Compose for SQL Server and for the two applications themselves (RabbitMQ and the monitoring and log stacks already have one), a CI pipeline running the build and the tests.
- **Monitoring in production:** a managed Prometheus/Grafana (or the cloud provider's equivalent), alert notifications to email, chat or an on-call tool (the local setup only shows alerts in Grafana), Elasticsearch with security, replicas and retention, instead of the single-node local one.


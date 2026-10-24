# OT Casino UI

A small React UI (hooks, state, props and plain CSS files, no UI libraries) for the OT Assessment API.
The full description, including the new endpoints and the database changes, is in the "Bonus: web UI,
login and admin portal" section of the repository's `Notes.md`.

## Install (once)

- **Node.js 18 or newer** (built with Node 22), which includes `npm`: https://nodejs.org. Check with `node -v`.
- The backend requirements from `Notes.md` ("Setup"): .NET 8 SDK, Docker (RabbitMQ) and SQL Server.
- Trust the API's development certificate: `dotnet dev-certs https --trust`.

## Run

1. Start RabbitMQ, the API (`dotnet run --launch-profile https` in `src/OT.Assessment.App`) and the Consumer.
2. Run `DatabaseUiSupport.sql` once, after `DatabaseGenerate.sql`.
3. In this folder:

   ```
   npm install
   npm run dev
   ```

4. Open http://localhost:5173 (the port is fixed, the API only allows this address through CORS).

The API address defaults to `https://localhost:7120`. Set `VITE_API_URL` to change it, and add the UI's
address to `Cors:AllowedOrigins` in the API's `appsettings.json`.

## Logins

- Admin: `admin` / `admin` (the `Admin` section of the API's `appsettings.json`, local development only).
- Player: click "New player? Create an account", or use a seeded player such as `Walter33` / `Walter$1234`
  (password = first name + `$1234`).

## Screens

- Player: choose a game (radio inputs shown as tiles, with search and paging), pick a stake with the chips or
  type an amount, place the bet, and see "My bets".
- Admin: Players (search, "View bets"), Top spenders, and Tests (runs the unit tests or the load test).

## Other commands

- `npm run build` creates a production build in `dist/` (also a quick check that the UI compiles).
- `npm run preview` serves that build on port 4173, which the API does not allow by default.

## Troubleshooting

- **"Failed to fetch"**: the API is not running or its certificate is not trusted. Start the API, run
  `dotnet dev-certs https --trust`, and open https://localhost:7120/swagger once.
- **A bet is not in "My bets"**: the Consumer or RabbitMQ is not running. Start them and press Refresh.
- **Empty games list**: `DatabaseUiSupport.sql` has not been run.
- **Admin pages fail after the API restarted**: log out and log in again, the admin token is kept in memory.

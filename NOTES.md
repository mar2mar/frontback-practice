# RateWatch Notes

Status markers: **Implemented** = in the code and verified. **Planned** = agreed, not yet in the code.

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Git
- VS Code (recommended)

## How to Run Locally

1. From the repository root, start the backend:

   ```powershell
   dotnet run --project src/RateWatch.Api
   ```

   On startup, the backend creates the SQLite database if it doesn't exist and applies any pending migrations. No separate database step is needed.

   - Backend URL: http://localhost:5080
   - Health endpoint: http://localhost:5080/api/health

2. In a second terminal, start the frontend:

   ```powershell
   cd frontend; npm install; npm run dev
   ```

   - Frontend URL: http://localhost:5173

Keep the backend running while you use the frontend. The Vite dev server proxies all `/api` requests to the backend at http://localhost:5080.

## Database Setup

Database initialization is automatic:
- When the backend starts, `Database.Migrate()` creates `src/RateWatch.Api/ratewatch.db` if it's missing and applies any pending migrations.
- Existing data is kept across restarts.

The `dotnet-ef` CLI is only needed to create new migrations. Run these commands from the repository root:

```powershell
dotnet tool restore   # installs the pinned dotnet-ef (.config/dotnet-tools.json)
```

- Add a migration after changing the model:

  ```powershell
  dotnet ef migrations add <Name> --project src/RateWatch.Api --output-dir Data/Migrations
  ```

- Reset the local database: stop the backend, delete `src/RateWatch.Api/ratewatch.db` (and any `ratewatch.db-wal` / `ratewatch.db-shm` files), then start the backend again.
- Optional: `dotnet ef database update --project src/RateWatch.Api` applies migrations without starting the app.
- SQLite writes recent changes to a `-wal` file next to the database (WAL journaling), so the main `.db` file can look small. Those files are git-ignored too (`*.db-wal`, `*.db-shm`).
- The connection string (`ConnectionStrings:RateWatch` in `appsettings.json`) is `Data Source=ratewatch.db`, a path relative to the API project folder. Both `dotnet run --project` and `dotnet ef --project` use that folder, so they open the same file.
- The database file is git-ignored (`*.db`).

## Business Rules and Assumptions

- Alerts are **above-threshold only**. There are no "below" alerts.
- An alert triggers when `currentRate > TargetRate` (strict comparison). A rate equal to the target does not trigger.
- Alerts are **one-shot**. After the first trigger, `TriggeredAt` stays set and the alert is never evaluated again.
- Evaluation runs only when requested through `POST /api/alerts/evaluate`. There is no background polling.
- There are no users or authentication. All alerts are global.
- There is no email or other notification. "Triggered" means `TriggeredAt` is set, and the alert is returned from the evaluation.
- The current rate for an alert is the rate for `QuoteCurrency` in the provider's snapshot for `BaseCurrency`.
- Rates come from `FakeExchangeRateProvider`, which simulates an external API.
- All times are UTC.

## Entity Contract: `RateAlert`

**Implemented:** the properties below, and constructor validation (see the next table).

| Property | Type | Notes |
|---|---|---|
| `Id` | `int` | Identity, assigned by the database |
| `BaseCurrency` | `string` | 3-letter ISO code, e.g. `USD` |
| `QuoteCurrency` | `string` | 3-letter ISO code, e.g. `EUR` |
| `TargetRate` | `decimal` | Triggers when the current rate is greater than this |
| `CreatedAt` | `DateTimeOffset` | UTC |
| `TriggeredAt` | `DateTimeOffset?` | `null` = active. Set once and never cleared. |

All setters are private.

**Constructor validation (implemented and covered by `RateAlertTests`):**

| Rule | On violation |
|---|---|
| Currencies are exactly 3 ASCII letters, normalized to uppercase | `ArgumentException` (`ParamName` = `baseCurrency` or `quoteCurrency`) |
| Base and quote currencies differ (compared after normalization, so `USD`/`usd` is rejected) | `ArgumentException` (`ParamName` = `quoteCurrency`) |
| `TargetRate > 0` | `ArgumentOutOfRangeException` (`ParamName` = `targetRate`) |
| `CreatedAt` is converted to UTC; the moment in time is unchanged | normalized, no exception |
| `TriggeredAt` is `null` when created | — |

- **Null currencies** throw `ArgumentException`, not `ArgumentNullException`, so callers can handle every invalid currency the same way.
- **Whitespace is not trimmed**, so `" US"` is rejected.
- **Non-ASCII letters** are rejected (e.g. `ÜSD`).

The entity only checks the format. The service checks that a currency is supported (see below).

## Service Behavior: `AlertService`

**`CreateAsync` (implemented and covered by `AlertServiceTests`):**
1. Builds a `RateAlert` with `CreatedAt = TimeProvider.GetUtcNow()`. The entity's validation runs first, so invalid input throws before the provider is called.
2. Calls `IExchangeRateProvider.GetLatestRatesAsync(base)`. If it returns no snapshot, the base currency is unsupported: `ArgumentException` (`ParamName` = `baseCurrency`).
3. If the quote currency isn't in that snapshot's rates, it is unsupported: `ArgumentException` (`ParamName` = `quoteCurrency`).
4. Saves the alert with `IAlertRepository.AddAsync` and returns it.

On any error, nothing is saved.

**`ListAsync` (implemented):** returns `IAlertRepository.ListAsync()` unchanged. The service doesn't sort again: ordering is the repository's job.

**`DeleteAsync` (implemented):** returns `IAlertRepository.DeleteAsync(id)` unchanged: `true` if the alert was deleted, `false` if no alert has that ID. Alerts are deleted permanently.

**`EvaluateAsync`:** not implemented. It throws `NotImplementedException`.

`AlertService` and `AlertRepository` are registered as scoped services, and `TimeProvider.System` as a singleton.

## API Contracts

### Implemented

**`GET /api/health`** returns `200 {"status":"ok"}`.

**`GET /api/rates?base=USD`**
- `base` is optional, defaults to `USD`, and is case-insensitive.
- `200` response:
  ```json
  {
    "baseCurrency": "USD",
    "timestamp": "2026-10-21T10:15:00+00:00",
    "rates": [{ "currency": "EUR", "rate": 0.921843 }]
  }
  ```
- `400` (ProblemDetails): unsupported base currency.

**Alert JSON** (returned by the alert endpoints; serialized from `RateAlert`):

```json
{
  "id": 1,
  "baseCurrency": "USD",
  "quoteCurrency": "EUR",
  "targetRate": 0.95,
  "createdAt": "2026-10-21T17:56:38.0182181+00:00",
  "triggeredAt": null
}
```

**`POST /api/alerts`**: create an alert.
- Request body:
  ```json
  { "baseCurrency": "usd", "quoteCurrency": "EUR", "targetRate": 0.95 }
  ```
  Currencies are case-insensitive and stored in uppercase.
- `201 Created`: the alert JSON, with a `Location: /api/alerts/{id}` header.
- `400 Bad Request` (`application/problem+json`): invalid currency format, base and quote the same, unsupported currency, or `targetRate <= 0`. The error message is in `detail`, for example:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Bad Request",
    "status": 400,
    "detail": "Unsupported quote currency 'XYZ'. (Parameter 'quoteCurrency')"
  }
  ```
- `400 Bad Request` for a malformed JSON body. ASP.NET Core returns this before the handler runs, so it isn't ProblemDetails (see Known Limitations).

**`GET /api/alerts`**: list all saved alerts.
- `200 OK`: an array of alert JSON, newest first (by `id`). `[]` when there are no alerts.
- Doesn't fetch rates or evaluate alerts.

**`DELETE /api/alerts/{id}`**: delete an alert permanently.
- `204 No Content`: the alert was deleted.
- `404 Not Found` (empty body): no alert with that ID. A non-integer ID also gets `404`, because the route only matches integers (`{id:int}`).

### Planned (not implemented)

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/alerts/evaluate` | Evaluate active alerts and return those triggered by this run |

The status codes and response body will be defined when the endpoint is implemented.

## Architecture

- **Backend:** one ASP.NET Core Minimal API project (`RateWatch.Api`) organized by feature folder (`Rates/`, `Alerts/`). There are no separate Domain or Infrastructure projects.
- **Rates (implemented):** `IExchangeRateProvider` represents the external rate API. The app registers `FakeExchangeRateProvider`, and tests use `StubExchangeRateProvider`, which returns rates set explicitly by each test.
- **Alerts:**
  - `RateAlert` is implemented, with constructor validation. Evaluation is not implemented yet.
  - `AlertService` implements `IAlertService`. `CreateAsync`, `ListAsync` and `DeleteAsync` are implemented so far.
  - `AlertRepository` (in `Data/`) implements `IAlertRepository` with EF Core:
    - `AddAsync` is implemented.
    - `ListAsync` is implemented. It uses `AsNoTracking()` and orders by `Id` descending, newest first. Sorting by `Id` works the same on every database, whereas SQLite can't sort `CreatedAt` in SQL.
    - `DeleteAsync` is implemented. It uses `ExecuteDeleteAsync`: one `DELETE ... WHERE Id = @id` statement, without loading the alert first. It returns `true` if a row was deleted.
    - The other methods throw `NotImplementedException`. `AlertService` tests still use `FakeAlertRepository`.
  - `RateAlert` and `AlertService` don't reference EF Core or SQLite.
  - `IAlertService` will create, list and delete alerts, and run evaluation.
  - `IAlertRepository` is specific to alerts, not generic. Each method saves its own changes.
- **Alert endpoints:**
  - The three handlers are Minimal API handlers written inline in `Program.cs`, next to `/api/health` and `/api/rates`. They call `IAlertService` only.
  - `POST` binds a `CreateAlertRequest` record.
  - Responses return `RateAlert` directly, with no response DTO, which matches how `/api/rates` returns `RateSnapshot`.
  - The `POST` handler catches `ArgumentException`, which also covers `ArgumentOutOfRangeException`, and returns a `400` ProblemDetails. This reuses the existing exception behavior; there's no result-type framework.
- **Not used:** CQRS, MediatR, controllers or mapping libraries.
- **Frontend:** Vue 3 and Vite (JavaScript). The Vite dev proxy forwards `/api` to the backend, so no CORS setup is needed.

### Persistence: SQLite and EF Core

- **Implemented:**
  - `AppDbContext` with one `RateAlerts` table. The mapping is configured in `OnModelCreating`: currencies have a maximum length of 3, and `TargetRate` uses `HasPrecision(18, 6)`.
  - The `InitialCreate` migration, in `Data/Migrations`.
  - `AppDbContext` registered with `UseSqlite`.
- **Registered (scoped):** `AlertRepository` and `AlertService`, sharing the request's `AppDbContext`.
- **Packages:**
  - `Microsoft.EntityFrameworkCore.Sqlite` 10.0.13.
  - `Microsoft.EntityFrameworkCore.Design` 10.0.13, design-time only.
  - The `dotnet-ef` 10.0.13 local tool.
- **Loading alerts:** EF Core loads a `RateAlert` through its constructor, so the validation also runs when reading. Rows in the database are always valid, because they can only be written through the entity.
- **Portability:** there's no provider-specific business logic. Moving to PostgreSQL or SQL Server should only need a different provider package and `Use...()` call, plus regenerated migrations.
- **Number and time columns:**
  - SQLite stores `decimal` and `DateTimeOffset` as text, so EF Core can't translate comparisons or ordering on them into SQL.
  - Therefore the threshold comparison runs in C#, and lists are ordered by `Id`, not `CreatedAt`.
  - `HasPrecision(18, 6)` on `TargetRate` gives proper numeric columns on PostgreSQL and SQL Server. SQLite ignores it.
- **Migrations:** applied automatically at startup with `Database.Migrate()` in `Program.cs`, inline, with no extra service. `EnsureCreated()` is not used.
- **Trade-off:**
  - Automatic migrations are convenient for this exercise: one command starts a working backend.
  - In production, migrations are usually applied separately, as a reviewed SQL script or a migration bundle in the deployment pipeline. That avoids several instances racing to migrate, gives the app no need for permission to change the schema, and makes failures visible before release.
- **Design-time tooling:** `dotnet ef` stops the app after `Build()`, so the startup migration doesn't run when creating migrations.

### Evaluation Behavior (planned)

`POST /api/alerts/evaluate` calls `IAlertService.EvaluateAsync`, which will:
1. Load the active alerts (`TriggeredAt` is null).
2. Fetch the current rates from `IExchangeRateProvider` once for each base currency.
3. For each alert, if `rate(QuoteCurrency) > TargetRate`, set `TriggeredAt` to the current UTC time from `TimeProvider`.
4. Save the triggered alerts and return them.

Alerts that are already triggered are skipped, which is what makes them one-shot.

## AI Collaboration Decisions

| # | AI suggestion | Outcome | Reason |
|---|---|---|---|
| 1 | Minimal APIs instead of controllers | Accepted | Less code. It is also the .NET 10 default. |
| 2 | Single API project with feature folders | Accepted | Simplest structure for the size of the app |
| 3 | Vite dev proxy instead of CORS | Accepted | No CORS configuration. The frontend uses relative URLs. |
| 4 | Remove the template's OpenAPI package | Accepted | It pulled in a dependency with a known high-severity vulnerability, and nothing used it |
| 5 | Use the Vite `vue` template instead of `create-vue` | Accepted | In this version, `create-vue`'s defaults produced TypeScript; JavaScript was required |
| 6 | Integration tests with `WebApplicationFactory` | Accepted | Tests the real HTTP pipeline in memory |
| 7 | `IExchangeRateProvider` to represent the external API | Modified | User added the requirement that it be easy to replace with a deterministic stub, for testing threshold crossings |
| 8 | Fake rates fluctuate within ±0.5% instead of a random walk | Accepted | Simple and keeps no state. A random walk was deferred. |
| 9 | `decimal` for rates | Accepted | Avoids binary floating-point errors |
| 10 | `AlertDirection` enum (Above/Below) | Rejected | User chose above-threshold alerts only |
| 11 | Optional `Email` field on alerts | Rejected | No notification requirement |
| 12 | First alert checkpoint: entity, DbContext and migration | Modified | User split it: model and interfaces first, persistence later |
| 13 | No repository layer (use the DbContext directly) | Rejected | User chose a minimal, alert-specific `IAlertRepository` |
| 14 | Defer startup migration to the endpoints checkpoint | Modified | User chose explicit migrations only, with no startup migration at all |
| 15 | Add constructor validation test-first, in a later change (option B) | Accepted | Strict TDD: no validation code until there are tests for it |
| 16 | Null currency throws `ArgumentException`, not `ArgumentNullException` | Accepted | Callers handle every invalid currency the same way |
| 17 | Don't trim whitespace in currency codes | Accepted | Strict input; `" US"` is rejected |
| 18 | The "currencies must differ" error names `quoteCurrency` | Accepted | The quote is the value that conflicts with the base |
| 19 | Check currency support with the existing `GetLatestRatesAsync(base)` instead of adding a "supported currencies" method to the provider | Accepted | Keeps the provider interface unchanged. One extra provider call per create is acceptable. |
| 20 | In `CreateAsync`, run entity validation before calling the provider | Accepted | Invalid input fails fast, and the provider only receives normalized codes |
| 21 | Use EF Core 10.0.13 instead of 10.0.1 | Accepted | 10.0.1 pulled in packages with known high-severity vulnerabilities (`SQLitePCLRaw.lib.e_sqlite3`, `System.Security.Cryptography.Pkcs`). 10.0.13 has none. |
| 22 | Persistence tests use in-memory SQLite and apply the real migrations with `Migrate()`, not `EnsureCreated()` | Accepted | Tests the actual migration, and fails if the model has changes no migration covers |
| 23 | Register `AppDbContext` now, and register the repository and service later with the endpoints | Accepted | `dotnet ef` needs the context registered. Nothing uses the repository or service yet, so registering them can wait. |
| 24 | Isolate integration tests with `RateWatchApiFactory`: each instance gets its own temporary SQLite file, deleted on dispose | Accepted | Integration tests never touch the development database. In-memory SQLite was rejected: the database disappears when the startup migration closes its connection. |
| 25 | `AlertRepository.DeleteAsync` uses `ExecuteDeleteAsync` instead of find, remove, then `SaveChanges` | Accepted | One SQL statement with no extra query, and the affected-row count gives the true/false result directly. Translated the same way on every EF Core relational provider. |
| 26 | The fake repository's `DeleteAsync` records requested IDs and returns a value set by the test | Accepted | Alerts in the fake have no IDs (no reflection, `Id` unchanged), so it can't match by ID. The service test only needs to prove the call is passed through. |
| 27 | Endpoint tests create a new `RateWatchApiFactory` in each test, instead of sharing one class fixture | Pending review | Each test starts with an empty database, so the list and delete assertions don't depend on other tests. Costs a short app startup per test. |
| 28 | Map `ArgumentException` to a `400` ProblemDetails with try/catch inside the `POST` handler | Pending review | Only one endpoint needs it, so a global exception handler would be more than needed. Matches how `/api/rates` already returns `400`. |
| 29 | Return `RateAlert` directly, with `Location: /api/alerts/{id}`, and constrain the delete route to `{id:int}` | Pending review | No DTO, consistent with `RateSnapshot`. `{id:int}` makes non-numeric IDs a plain `404`. |

## Testing Strategy

- Behavior is written test-first (TDD). Tests and implementation are committed together once they pass.
- Tests focus on edge cases plus one or two representative happy-path tests. Overlapping cases are not added.
- **Unit tests:** domain behavior (`RateAlertTests`), `AlertService` (`AlertServiceTests`, using `FakeAlertRepository` and `StubExchangeRateProvider`) and the fake provider, using a deterministic `Random`, `FixedTimeProvider` and zero volatility where needed.
- **Integration tests:** `RateWatchApiFactory`, a `WebApplicationFactory<Program>` that overrides `ConnectionStrings:RateWatch` with a temporary SQLite file for each factory instance. `StubExchangeRateProvider` is swapped in where exact rates matter. `DatabaseStartupTests` checks that startup migrates a fresh database and that a restart keeps existing alerts. `AlertsEndpointTests` creates a new factory in each test, so each test starts with an empty database. It checks only HTTP behavior: status codes, `Location`, JSON shape, and that changes are saved. The validation rules themselves are covered by the unit tests.
- **Persistence tests:** `AlertRepositoryTests`. Each test class instance gets its own in-memory SQLite database, built with the real migrations. Data is read back through a separate `AppDbContext`, so the test checks what was actually stored.
- **Planned:**
  - `AlertService` tests for evaluate. They will include boundary cases: rate equal to the target (no trigger), just above it (trigger), and an already-triggered alert (skipped).
  - Persistence tests for the remaining repository methods.

## Verification Log

| Date | Change | Result |
|---|---|---|
| 2026-10-21 | Skeleton | `dotnet build` passed. 1 of 1 tests passed. Frontend build passed. Health checked through the Vite proxy with HTTP requests. |
| 2026-10-21 | Simulated starter app | 12 of 12 tests passed. Frontend build passed. `/api/rates` checked through the proxy with HTTP requests. The UI was not checked in a browser. |
| 2026-10-21 | `RateAlert` model and alert interfaces | `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 12 of 12 passed. No new tests, by design. |
| 2026-10-21 | `RateAlert` constructor validation (TDD) | Red: 18 of 21 new tests failed for the expected reasons (no exception thrown, not uppercase, offset not UTC). The 3 that passed check behavior plain assignment already gave. Green: 33 of 33 passed. Then trimmed to edge cases and one happy-path test (14 cases): `dotnet build` 0 warnings, 0 errors. `dotnet test`: 26 of 26 passed. |
| 2026-10-21 | `AlertService.CreateAsync` (TDD) | Red: 4 of 4 new tests failed with `NotImplementedException`. Green: `dotnet build` 0 warnings, 0 errors. `dotnet test`: 30 of 30 passed. |
| 2026-10-21 | SQLite persistence: `AlertRepository.AddAsync` (TDD) | `dotnet list package --vulnerable`: none found. Red: 1 of 1 new test failed with `NotImplementedException`, after `Migrate()` had succeeded. Green: `dotnet build` 0 warnings, 0 errors. `dotnet test`: 31 of 31 passed. Ran `dotnet ef database update` and it created `src/RateWatch.Api/ratewatch.db`, which git ignores. Confirmed the app's content root is `src/RateWatch.Api`. |
| 2026-10-21 | Automatic database initialization at startup (TDD) | Red: 2 of 2 new tests failed for the expected reasons (no database file created; `no such table: RateAlerts`). Green: `dotnet build` 0 warnings, 0 errors. `dotnet test`: 33 of 33 passed, run twice. The development database's last-write time was unchanged after the test runs, and no temporary test files were left behind. Manual check: started the app against a fresh file twice. `/api/health` returned ok both times. `dotnet ef migrations list` showed `InitialCreate` as applied in that file and `(Pending)` in an empty one. The app also started against the existing development database. |
| 2026-10-21 | Alert listing: repository and service (TDD) | Red: 3 of 3 new tests failed with `NotImplementedException`. Green: `dotnet build` 0 warnings, 0 errors. `dotnet test`: 36 of 36 passed. The development database's last-write time was unchanged by a test run. It had changed since the previous check only because of that checkpoint's manual app start, since `Migrate()` writes EF's migration-lock table on each start. |
| 2026-10-21 | Alert deletion: repository and service (TDD) | Red: 4 of 4 new test cases failed with `NotImplementedException`. Green: `dotnet build` 0 warnings, 0 errors. `dotnet test`: 40 of 40 passed. The development database's last-write time was unchanged by the test run. |
| 2026-10-21 | Alert CRUD endpoints (TDD) | Red: 6 of 6 new test cases failed with `404 NotFound`, because the routes didn't exist yet. Green: `dotnet build` 0 warnings, 0 errors. `dotnet test`: 46 of 46 passed. The development database was unchanged and no temporary test files were left behind. Manual check against a throwaway database: `GET` → 200 `[]`; `POST` → 201 with `Location: /api/alerts/1`; unsupported currency, zero target and invalid code → 400 ProblemDetails; malformed JSON → 400 plain text; `DELETE /1` → 204, then 404; `DELETE /abc` → 404. |

## Known Limitations

- Exchange rates are simulated. There's no real provider.
- Alert evaluation is not implemented: `AlertService.EvaluateAsync`, `AlertRepository.ListActiveAsync` and `UpdateAsync` throw `NotImplementedException`, and there is no `POST /api/alerts/evaluate`.
- Error responses are not fully consistent:
  - A malformed JSON body gets a framework `400` (in Development, a plain-text developer exception page) instead of ProblemDetails.
  - `404` responses have an empty body.
  - The `detail` text includes .NET's `(Parameter '…')` suffix.
- `Location` points to `/api/alerts/{id}`, but there is no `GET` for a single alert at that URL; only `DELETE` exists there.
- There is no frontend for alerts yet.
- Listing returns every alert, with no paging or filtering.
- The SQLite file path is relative to the API project folder, so run `dotnet run` and `dotnet ef` with `--project` from the repository root, as documented. Running the app from another folder would open a different, empty database.
- SQLite stores `decimal` and `DateTimeOffset` as text, so EF Core can't filter or sort by `TargetRate` or `CreatedAt` in SQL.
- Migrations run on every app start. That's fine for one local instance, but not suitable for several instances or controlled production releases.
- `FakeAlertRepository` doesn't assign alert IDs, because `Id` has a private setter and is meant to come from the database. Its `ListAsync` returns alerts in insertion order. Its `DeleteAsync` records the requested IDs and returns a value the test sets, without matching alerts by ID. That's enough for the service's delegation tests; real deletion by ID is tested against SQLite.
- `RateAlert` checks the format of currency codes, but not whether a code is a real ISO 4217 currency (e.g. `ABC` is accepted). The service will check that currencies are supported.
- Evaluation only happens when requested. Alerts don't trigger on their own.
- There are no users, authentication or notifications.
- There are no frontend tests. The UI is checked by hand.

## Future Improvements

- Below-threshold and repeating alerts.
- Background polling, for example with a hosted service.
- Notifications, such as email or webhooks.
- A real exchange-rate provider with caching and retries.
- PostgreSQL or SQL Server for production: switch the provider package and the `Use...()` call, regenerate the migrations, and use connection strings per environment.
- In production, apply migrations in the deployment pipeline (SQL script or migration bundle) instead of at startup.
- Consistent error responses: `AddProblemDetails()` would give malformed-JSON and `404` responses ProblemDetails bodies, and clean messages could be returned without the .NET parameter suffix.
- `GET /api/alerts/{id}`, so the `Location` header points to a readable resource.
- User accounts so each user has their own alerts.
- Frontend tests with Vitest.

## Time Tracking

To be filled in manually. Claude does not estimate or record time.

| Date | Checkpoint | Time spent |
|---|---|---|
| 2026-10-21 | Skeleton | |
| 2026-10-21 | Simulated starter app | |
| 2026-10-21 | `RateAlert` model and alert interfaces | |
| 2026-10-21 | `RateAlert` constructor validation | |
| 2026-10-21 | `AlertService.CreateAsync` | |
| 2026-10-21 | SQLite persistence for creating alerts | |
| 2026-10-21 | Automatic database initialization at startup | |
| 2026-10-21 | Alert listing: repository and service | |
| 2026-10-21 | Alert deletion: repository and service | |
| 2026-10-21 | Alert CRUD endpoints | |

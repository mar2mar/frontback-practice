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

   - Backend URL: http://localhost:5080
   - Health endpoint: http://localhost:5080/api/health

2. In a second terminal, start the frontend:

   ```powershell
   cd frontend; npm install; npm run dev
   ```

   - Frontend URL: http://localhost:5173

Keep the backend running while you use the frontend. The Vite dev server proxies all `/api` requests to the backend at http://localhost:5080.

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

The service will check that a currency is supported by the rate provider. The entity only checks the format.

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

### Planned (not implemented)

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/alerts` | Create an alert (`baseCurrency`, `quoteCurrency`, `targetRate`) |
| `GET` | `/api/alerts` | List all alerts, newest first |
| `DELETE` | `/api/alerts/{id}` | Delete an alert. `404` if not found. |
| `POST` | `/api/alerts/evaluate` | Evaluate active alerts and return those triggered by this run |

Status codes and response bodies will be defined when each endpoint is implemented. Validation errors will return `400` (ProblemDetails).

## Architecture

- **Backend:** one ASP.NET Core Minimal API project (`RateWatch.Api`) organized by feature folder (`Rates/`, `Alerts/`). There are no separate Domain or Infrastructure projects.
- **Rates (implemented):** `IExchangeRateProvider` represents the external rate API. The app registers `FakeExchangeRateProvider`, and tests use `StubExchangeRateProvider`, which returns rates set explicitly by each test.
- **Alerts:**
  - `RateAlert` is implemented, with constructor validation. Evaluation is not implemented yet.
  - `IAlertService` and `IAlertRepository`: interfaces defined, not implemented yet.
  - `IAlertService` will create, list and delete alerts, and run evaluation.
  - `IAlertRepository` is specific to alerts, not generic. Each method saves its own changes.
- **Not used:** CQRS and MediatR.
- **Frontend:** Vue 3 and Vite (JavaScript). The Vite dev proxy forwards `/api` to the backend, so no CORS setup is needed.

### Persistence: SQLite and EF Core (planned)

- **Setup:** EF Core with the SQLite provider, and one `RateAlerts` table.
- **Portability:** there's no provider-specific business logic. Moving to PostgreSQL or SQL Server should only need a different provider package and `Use...()` call, plus regenerated migrations.
- **Number and time columns:**
  - SQLite stores `decimal` and `DateTimeOffset` as text, so EF Core can't translate comparisons or ordering on them into SQL.
  - Therefore the threshold comparison runs in C#, and lists are ordered by `Id`, not `CreatedAt`.
  - `TargetRate` will be mapped with `HasPrecision(18, 6)`, which gives proper numeric columns on PostgreSQL and SQL Server.
- **Migrations:** applied explicitly with `dotnet ef database update`, not at application startup.
- **Database file:** the local SQLite file is git-ignored (`*.db`).

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

## Testing Strategy

- Behavior is written test-first (TDD). Tests and implementation are committed together once they pass.
- Tests focus on edge cases plus one or two representative happy-path tests. Overlapping cases are not added.
- **Unit tests:** domain behavior (`RateAlertTests`) and the fake provider, using a deterministic `Random`, `FixedTimeProvider` and zero volatility where needed.
- **Integration tests:** `WebApplicationFactory`, with `StubExchangeRateProvider` swapped in where exact rates matter.
- **Planned:**
  - `AlertService` tests against an in-memory fake repository and the stub provider. These include boundary cases: rate equal to the target (no trigger), just above it (trigger), and an already-triggered alert (skipped).
  - Persistence tests with in-memory SQLite.

## Verification Log

| Date | Change | Result |
|---|---|---|
| 2026-10-21 | Skeleton | `dotnet build` passed. 1 of 1 tests passed. Frontend build passed. Health checked through the Vite proxy with HTTP requests. |
| 2026-10-21 | Simulated starter app | 12 of 12 tests passed. Frontend build passed. `/api/rates` checked through the proxy with HTTP requests. The UI was not checked in a browser. |
| 2026-10-21 | `RateAlert` model and alert interfaces | `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 12 of 12 passed. No new tests, by design. |
| 2026-10-21 | `RateAlert` constructor validation (TDD) | Red: 18 of 21 new tests failed for the expected reasons (no exception thrown, not uppercase, offset not UTC). The 3 that passed check behavior plain assignment already gave. Green: 33 of 33 passed. Then trimmed to edge cases and one happy-path test (14 cases): `dotnet build` 0 warnings, 0 errors. `dotnet test`: 26 of 26 passed. |

## Known Limitations

- Exchange rates are simulated. There's no real provider.
- There's no persistence yet. Alert endpoints, the service and the repository are not implemented.
- `RateAlert` checks the format of currency codes, but not whether a code is a real ISO 4217 currency (e.g. `ABC` is accepted). The service will check that currencies are supported.
- Evaluation only happens when requested. Alerts don't trigger on their own.
- There are no users, authentication or notifications.
- There are no frontend tests. The UI is checked by hand.

## Future Improvements

- Below-threshold and repeating alerts.
- Background polling, for example with a hosted service.
- Notifications, such as email or webhooks.
- A real exchange-rate provider with caching and retries.
- PostgreSQL or SQL Server for production.
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

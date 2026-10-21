# Decisions

Important architectural decisions for RateWatch, and AI suggestions that were accepted or rejected.

## 2026-10-21 — Checkpoint 1: Project skeleton

### Minimal APIs instead of controllers
- **Decision:** The backend uses ASP.NET Core Minimal APIs.
- **Reason:** Less code for a small API. It is also the .NET 10 template default.
- **Source:** AI suggestion, accepted.

### Single API project with feature folders
- **Decision:** One `RateWatch.Api` project plus one test project. No separate Domain or Infrastructure projects.
- **Reason:** Keeps the architecture simple. We will split it out only if the domain grows enough to need it.
- **Source:** AI suggestion, accepted.

### Vite dev proxy instead of CORS
- **Decision:** In development, the Vue app calls relative `/api/...` URLs, and Vite proxies them to `http://localhost:5080`.
- **Reason:** No CORS configuration is needed. The frontend code does not depend on the backend's port.
- **Source:** AI suggestion, accepted.

### OpenAPI package removed
- **Decision:** Removed `Microsoft.AspNetCore.OpenApi` from the API template.
- **Reason:** It pulled in `Microsoft.OpenApi` 2.0.0, which has a known high-severity vulnerability (GHSA-v5pm-xwqc-g5wc), and nothing uses it yet.
- **Source:** AI change during Checkpoint 1. Reported as a deviation and kept in the committed skeleton.

## 2026-10-21 — Checkpoint 2: Simulated starter application

### `IExchangeRateProvider` represents the external rate API
- **Decision:** All rate access goes through `IExchangeRateProvider`. The app registers `FakeExchangeRateProvider`.
- **Reason:** Isolates the external dependency, so a real provider can replace the fake one later.
- **Source:** AI suggestion, accepted. The user added a requirement: the interface must be easy to replace with a deterministic test fake that returns explicitly configured rates, for later threshold-crossing tests. This is implemented as `StubExchangeRateProvider` in the test project.

### Bounded random fluctuation instead of a random walk
- **Decision:** Each call returns USD mid-market rates multiplied by a random factor within ±0.5%. Rates for other bases are cross rates derived from the USD rates. The provider keeps no state between calls.
- **Reason:** Simple and thread-safe, and rates cannot drift without limit. A random walk was considered and deferred until alert work shows a need for it.
- **Source:** AI suggestion, accepted.

### `decimal` for rates
- **Decision:** Exchange rates are `decimal`, rounded to 6 decimal places.
- **Reason:** Avoids binary floating-point errors in money-related values.
- **Source:** AI suggestion, accepted.

### Domain records returned directly from the endpoint
- **Decision:** `GET /api/rates` returns `RateSnapshot` directly, with no separate DTOs.
- **Reason:** The API response and the domain types have the same shape. A DTO would be an unnecessary abstraction for now.
- **Source:** AI suggestion, accepted.

### Time and randomness are injected
- **Decision:** `FakeExchangeRateProvider` takes a `TimeProvider`, a `Random` and a volatility value.
- **Reason:** Tests can use a fixed clock, a seeded random number generator and zero volatility, so their results are deterministic.
- **Source:** AI suggestion, accepted.

### No simulation endpoint yet
- **Decision:** There is no endpoint to set or control rates at runtime.
- **Reason:** Not needed yet. Tests control rates through the stub provider instead.
- **Source:** User decision.

## 2026-10-21 — Checkpoint 3: Rate alert model and interfaces

### Above-threshold, strict, one-shot alerts
- **Decision:** An alert triggers when `currentRate > TargetRate`. There are no below-threshold alerts. Once triggered, `TriggeredAt` stays set and the alert is never evaluated again.
- **Reason:** Keeps the domain minimal. The strict comparison means a rate equal to the target does not trigger.
- **Source:** User decision. The AI's `AlertDirection` (Above/Below) enum was rejected.

### No email or notification
- **Decision:** `RateAlert` has no email field, and there is no notification infrastructure.
- **Reason:** Not required. "Triggered" means `TriggeredAt` is set and the alert is returned from evaluation.
- **Source:** User decision. The AI's question about an optional `Email` field was answered no.

### Evaluation only when explicitly requested
- **Decision:** Alerts are evaluated only when a client calls `POST /api/alerts/evaluate`. There is no background polling.
- **Reason:** Easy to reason about and to test.
- **Source:** User decision.

### Alert-specific repository and service
- **Decision:** Add `IAlertService` (create, list, delete, evaluate) and a minimal `IAlertRepository` that is specific to alerts. Each repository method saves its own changes. There is no generic repository, CQRS or MediatR.
- **Reason:** Keeps EF Core out of the service and lets `AlertService` be tested with an in-memory fake.
- **Source:** User decision. It replaces the AI's proposal to use `AppDbContext` directly.

### Model and interfaces before persistence
- **Decision:** This checkpoint adds only the entity and the interfaces. The DbContext, EF Core packages and migrations come later.
- **Reason:** Smaller checkpoints.
- **Source:** User decision. It modifies the AI's proposal to start with the entity, DbContext and migration together.

### Constructor validation written test-first
- **Decision:** For now, `RateAlert`'s constructor only assigns values. The validation rules are documented in NOTES.md as planned, and will be implemented together with their tests.
- **Reason:** Strict TDD: no validation code without tests.
- **Source:** AI offered two options (A and B). The user chose B.

### SQLite with EF Core, portable, migrations applied explicitly (planned)
- **Decision:** Persistence will use EF Core with SQLite and no provider-specific business logic. Threshold comparisons will run in C#, lists will be ordered by `Id`, and migrations will be applied with `dotnet ef database update`, never at startup.
- **Reason:** SQLite stores `decimal` and `DateTimeOffset` as text, so EF Core can't translate comparisons or ordering on them into SQL. Applying migrations explicitly keeps integration tests away from the local database file.
- **Source:** AI proposal, modified by the user: explicit migrations only, with no startup migration at all.

## 2026-10-21 — SQLite persistence for creating alerts

### EF Core persistence in `Data/`
- **Decision:**
  - `AppDbContext` and `AlertRepository` live in `src/RateWatch.Api/Data/`, and the migrations in `Data/Migrations/`.
  - The mapping is configured in `OnModelCreating`, with no separate configuration classes.
  - `RateAlert` and `AlertService` don't reference EF Core.
- **Reason:** Keeps the domain and the service independent of the database, without adding projects or layers.
- **Source:** User requirement. The AI implemented it.

### Pinned, patched EF Core tooling
- **Decision:** Use EF Core packages 10.0.13, and pin `dotnet-ef` 10.0.13 in a local tool manifest (`.config/dotnet-tools.json`).
- **Reason:**
  - 10.0.1 pulled in packages with known high-severity vulnerabilities, and 10.0.13 reports none.
  - A local tool manifest means anyone can install the same CLI version with `dotnet tool restore`.
- **Source:** AI change during implementation, accepted by the user.

## 2026-10-21 — Automatic database initialization

### Apply migrations at startup (supersedes "migrations applied explicitly")
- **Decision:** `Program.cs` calls `Database.Migrate()` once after `Build()`. It creates the SQLite file if it's missing and applies pending migrations. Existing data is kept, and `EnsureCreated()` is not used.
- **Reason:** One command starts a working backend, which suits a take-home exercise. Production deployments would apply migrations separately.
- **Source:** User decision. It replaces the earlier "explicit migrations only, no startup migration" decision from Checkpoint 3.

### Integration tests use an isolated temporary SQLite file
- **Decision:** `RateWatchApiFactory` overrides `ConnectionStrings:RateWatch` with a unique file in the temp folder for each factory instance, and deletes it on dispose.
- **Reason:** Startup migration would otherwise make the integration tests create or change the development database. A file is used instead of in-memory SQLite, because an in-memory database disappears when the startup migration closes its connection.
- **Source:** AI proposal, accepted by the user.

## 2026-10-21 — Alert CRUD endpoints

### Inline Minimal API endpoints over `IAlertService`
- **Decision:**
  - `POST /api/alerts`, `GET /api/alerts` and `DELETE /api/alerts/{id:int}` are Minimal API handlers inline in `Program.cs`. They call only `IAlertService`.
  - `AlertService` and `AlertRepository` are registered as scoped, and `TimeProvider.System` as a singleton.
  - `ArgumentException` from the entity or the service becomes a `400` ProblemDetails.
  - Responses return `RateAlert` directly.
- **Reason:** Three small endpoints don't justify controllers, endpoint classes, a result framework or DTO mapping. Scoped lifetimes match `AppDbContext`.
- **Source:** User requirements (Minimal APIs, scoped DI, existing exception behavior). The handler details are AI choices, accepted by the user.

## 2026-10-21 — Alert evaluation

### The trigger rule lives in the entity
- **Decision:** `RateAlert.Trigger(currentRate, now)` holds the whole rule: strict `currentRate > TargetRate`, one-shot (returns `false` once triggered and keeps the original `TriggeredAt`), and `TriggeredAt` stored in UTC. The service will only supply the rate and the time.
- **Reason:** One place for the business rule, testable without the database or HTTP.
- **Source:** User decision (rule, method signature, UTC). The AI suggested putting the rule in the entity during the evaluation analysis.

### Evaluation semantics and contract (decided; service and endpoint not yet implemented)
- **Decision:**
  - Alerts with no available rate (unsupported base, or quote missing from the snapshot) are skipped, not treated as errors.
  - `POST /api/alerts/evaluate` returns `200 OK` with an array of the alerts triggered by this run.
  - Two evaluations at the same time will be handled with a conditional database update (`WHERE Id = @id AND TriggeredAt IS NULL`), not with a concurrency token.
- **Reason:**
  - The plain array matches `IAlertService.EvaluateAsync`'s return type.
  - The conditional update needs no migration and makes sure an alert is triggered and reported only once, even when two evaluations overlap.
- **Source:** User decisions. These were options (a) to (c) and the open questions from the AI's evaluation analysis.

### Conditional update in the repository (implemented)
- **Decision:**
  - `IAlertRepository.TryMarkTriggeredAsync(alertId, triggeredAt)` runs one `ExecuteUpdateAsync`: `UPDATE RateAlerts SET TriggeredAt = @utc WHERE Id = @id AND TriggeredAt IS NULL`. It returns `true` only if exactly one row changed.
  - `ListActiveAsync` uses `AsNoTracking()` and orders by `Id` ascending, so evaluation order and response order are predictable.
  - The unused `UpdateAsync` was removed.
  - The test fake's `ListActiveAsync` returns detached copies, built with the `RateAlert` constructor, to match `AsNoTracking`.
- **Reason:**
  - The database settles races between overlapping evaluations: only one request can change a row from `NULL`. So an alert is reported at most once and `TriggeredAt` is never overwritten.
  - It needs no migration, no concurrency token and no locking, and it's portable to PostgreSQL and SQL Server.
- **Trade-offs:**
  - "Trigger only once" is checked twice: in `RateAlert.Trigger`, which decides in memory, and in the SQL condition, which settles races. `ExecuteUpdateAsync` doesn't go through the entity.
  - The service must therefore return only alerts for which `TryMarkTriggeredAsync` returned `true`, not every alert for which `Trigger` returned `true`. If a concurrent request wins the race, the in-memory `TriggeredAt` on the losing request's (untracked) alert is never saved and must not be reported.
- **Source:** User design (method name, `ExecuteUpdateAsync`, `AsNoTracking`). Removing `UpdateAsync`, converting to UTC inside the repository, and the fake's behavior were AI choices, accepted by the user. The user asked for the ordering by `Id` and the detached copies during review.

### Evaluation orchestration in `AlertService.EvaluateAsync` (implemented)
- **Decision:** two phases.
  - **Phase 1:** fetch one snapshot per distinct base currency, one base at a time.
  - **Phase 2:**
    - Read one `now`.
    - Then, in active-alert order: skip alerts with no rate, let `Trigger` decide in memory, and call `TryMarkTriggeredAsync` to confirm in the database.
    - Only confirmed alerts are returned, in active-alert order.
  - Provider exceptions are not caught.
- **Reason:**
  - The service only coordinates: the rule stays in the entity, and the race is settled by the repository.
  - Fetching every snapshot before saving anything means a provider failure makes the whole run fail with nothing saved, so no trigger is lost without being reported.
  - Returning only confirmed alerts keeps the response truthful when evaluations overlap.
- **Trade-offs:**
  - Calling the provider one base at a time would be slow with a real remote provider and many base currencies.
  - A database error in phase 2 could still save some triggers without reporting them, because there's no transaction around the run.
- **Source:** User specification (steps 1–8). The AI proposed a single loop with lazy fetching. The user adjusted it to two phases after the AI pointed out the partial-progress risk. Not catching provider exceptions is an AI choice, accepted by the user.

### `POST /api/alerts/evaluate` endpoint (implemented)
- **Decision:**
  - An inline Minimal API handler in `Program.cs`: `Results.Ok(await alerts.EvaluateAsync(cancellationToken))`.
  - No request body, no response DTO, no try/catch.
  - It returns `200 OK` with the alerts triggered by this request, oldest first, or `[]`.
- **Reason:**
  - The service already returns exactly the response contract (only confirmed alerts, in order), so the handler just passes it through.
  - Provider failures are left unhandled on purpose, as decided for the service.
- **Trade-off:** a provider failure surfaces as an unhandled `500` without a ProblemDetails body. A global exception handler is left as a future improvement.
- **Source:** User specification. The AI implemented it with no extra choices beyond moving the test-only `AlertResponse` record into a shared file.

## 2026-10-21 — Frontend integration

### Frontend testing with Vitest
- **Decision:**
  - Add `vitest`, `@vue/test-utils` and `jsdom` as dev dependencies. The `test` block in `vite.config.js` sets `environment: 'jsdom'`, and `npm test` runs `vitest run` (no watch mode).
  - Tests stub `fetch` with `vi.stubGlobal`.
  - Test files sit next to their component.
- **Reason:**
  - Vitest reuses the existing Vite and Vue setup, so there's no separate build or test configuration.
  - Stubbing `fetch` matches how the components already make requests, so there's no mock server and no API client abstraction.
- **Source:**
  - Adding Vitest, Vue Test Utils and jsdom was the user's decision (frontend plan, Commit 1).
  - Stubbing `fetch` and putting tests next to components are AI choices, accepted by the user.

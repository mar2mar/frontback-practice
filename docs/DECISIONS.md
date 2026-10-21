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

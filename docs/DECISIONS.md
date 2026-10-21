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

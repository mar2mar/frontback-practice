# RateWatch Notes

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

Both applications have been started locally, and the frontend-to-backend connection through the Vite proxy was verified successfully.

## Running Tests

From the repository root:

```powershell
dotnet test
```

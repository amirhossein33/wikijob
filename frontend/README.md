# InterviewHub frontend

React + Vite + TypeScript frontend for the .NET 10 InterviewHub API. The interface is Persian and RTL. It lives in this repository's `frontend/` directory; the backend remains in `src/`.

## Local run

1. Start SQL Server LocalDB and apply backend migrations as described in the root `README.md`.
2. Start the API on the port expected by the Vite proxy:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = 'Development'
   dotnet run --project src/InterviewHub.Api --no-launch-profile --urls http://127.0.0.1:5199
   ```

3. In another terminal:

   ```powershell
   cd frontend
   pnpm install
   pnpm dev --host 127.0.0.1
   ```

4. Open `http://127.0.0.1:5173`. The Vite proxy forwards `/api` and `/health` to port 5199, so local CORS changes are unnecessary. For another API origin, copy `.env.example` to `.env.local` and set `VITE_API_BASE_URL`; configure backend CORS for that origin.

## Checks

```powershell
cd frontend
pnpm run typecheck
pnpm run lint
pnpm run test
pnpm run build
```

The JWT is kept in memory. Reloading the page requires login again. This avoids persistent browser token storage while the backend has no refresh-cookie endpoint. Registration supports Candidate or Interviewer; the second role can be added from the dashboard and then requires another login to receive a new token.

See `docs/api-inventory.md`, `docs/design-system.md`, `docs/frontend-progress.md` and `docs/api-health-report.md` for contracts, design decisions, limitations and verification status. This is a local development workflow; no hosting or deployment is configured.

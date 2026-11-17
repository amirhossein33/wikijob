# Local API health — 2026-11-17

- `dotnet build InterviewHub.slnx --verbosity quiet`: passed after stopping the running API process; zero warnings and errors.
- `GET /health`: 200 `{"status":"healthy"}` on local development SQL Server.
- `GET /api/experiences?page=1&pageSize=2`, `/api/companies`, `/api/questions?page=1&pageSize=2`, `/api/tags`, `/api/reports/market`: all executed with 200.
- Nine PowerShell integration scripts in `work/test-*.ps1` were rerun successfully during frontend integration against the local development database, including authentication, submissions, moderation, questions, profiles, filters, reports, and content flags.
- CORS for `http://localhost:5173` is not configured; local frontend integration should use a development proxy or configure the origin.
- No Swagger/OpenAPI endpoint is registered. Contracts were inspected directly in controllers and DTOs.
- Backend build initially failed because the API process held DLL files open; it passed after that process was stopped. This was an environment lock, not a source-code defect.

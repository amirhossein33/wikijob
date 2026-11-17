# InterviewHub MVP API

InterviewHub MVP with an ASP.NET Core backend and a Persian RTL React frontend. The API targets .NET 10 and SQL Server with EF Core and ASP.NET Core Identity.

## Local setup

1. Install .NET SDK 10, SQL Server LocalDB (or point `ConnectionStrings:DefaultConnection` at another SQL Server), and `dotnet-ef` 10.
2. Configure local secrets for `src/InterviewHub.Api`:

   ```powershell
   dotnet user-secrets set "Jwt:SigningKey" "<random string of at least 32 UTF-8 bytes>" --project src/InterviewHub.Api
   dotnet user-secrets set "Admin:UserName" "admin" --project src/InterviewHub.Api
   dotnet user-secrets set "Admin:Password" "<strong unique password>" --project src/InterviewHub.Api
   ```

3. Apply migrations, then run the API:

   ```powershell
   dotnet ef database update --project src/InterviewHub.Data --startup-project src/InterviewHub.Api
   dotnet run --project src/InterviewHub.Api
   ```

`appsettings.Development.json` points to `(localdb)\MSSQLLocalDB` and supplies local JWT issuer/audience. In other environments set `ConnectionStrings__DefaultConnection`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`, `Admin__UserName`, and `Admin__Password` through secure configuration. No admin password or signing key is stored in the repository. Startup creates roles and the configured admin, seeds the 100 General .NET reference questions once, links legacy interviewer questions, and refreshes SQL summaries. Migrations are applied explicitly, not automatically.

For a React deployment, configure `Cors__AllowedOrigins__0` with its exact HTTPS origin. `GET /health` checks the API and SQL connection. Account and content submission endpoints have basic rate limits; production traffic still needs monitoring and stronger abuse controls.

## API flow

| Area | Main routes |
| --- | --- |
| Identity | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/roles/{role}`, `GET /api/auth/me` |
| Candidate experiences | `POST /api/candidate-experiences`, `GET /api/candidate-experiences/{id}`, `GET /api/candidate-experiences/{id}/mine` |
| Interviewer experiences | `POST /api/interviewer-experiences`, `GET /api/interviewer-experiences/{id}`, `GET /api/interviewer-experiences/{id}/mine` |
| Interviewer advice | `PUT /api/interviewer-profiles/me`, `GET /api/interviewer-profiles/me`, `GET /api/interviewer-profiles/{publicId}` |
| Questions | `GET/POST /api/questions`, `GET /api/questions/{id}`, `GET/POST /api/tags` |
| Discovery | `GET /api/experiences`, `GET /api/companies` |
| Reports | `GET /api/reports/market`, `GET /api/reports/companies/{id}`, `GET /api/reports/companies/compare`, `GET /api/reports/interviewers/priorities` |
| Content flags | `POST /api/content-reports` |
| Admin | `/api/admin/experiences`, `/api/admin/interviewer-profiles`, `/api/admin/content-reports`, `POST /api/admin/reports/refresh`, `PUT /api/questions/{id}/tags`, `POST /api/questions/merge` |

Register as `Candidate` or `Interviewer`; one account may gain the other member role. Login returns a JWT. `401` means authentication is missing or invalid; `403` means the token is valid but lacks the required role. Admin is provisioned from trusted configuration. Submitted experiences and interviewer profiles remain pending until an admin publishes them. Anonymous fields are retained privately for ownership and moderation, but omitted from public DTOs. Interviewer ratings remain visible even when name or company is hidden.

Candidate experiences support multiple dated interview stages, selected canonical questions, optional job ad text or URL, outcome, and separate conduct and ad-alignment ratings. Similar questions are merged manually by an admin. General seed questions are references and do not count as interview occurrences until a published experience uses them.

Reports use physical SQL summary tables refreshed after publication, rejection, editing, deletion, and question merging. Company reports and interviewer priority groups require at least five qualifying contributions. Trend counts question mentions, not unique interviews. A company rating is derived from submitted conduct and job-ad-alignment ratings; it is not a mutable `Company.Rate` field.

## Verification

Build with `dotnet build InterviewHub.slnx`. Run the API on `http://127.0.0.1:5199` and execute the PowerShell integration scripts in `work/test-*.ps1` with `INTERVIEWHUB_TEST_PORT=5199`. These scripts create data in the configured development database and cover auth, both experience types, moderation, catalog, profiles, search, reports, and content flags. `work/test-interviewer-profiles.ps1` and `work/test-content-reports.ps1` currently use port 5199 directly.

For the frontend, follow `frontend/README.md`: install dependencies with `pnpm install`, then run `pnpm dev --host 127.0.0.1`. Vite serves the app at `http://127.0.0.1:5173` and proxies `/api` to the local backend. Frontend checks are `pnpm run typecheck`, `pnpm run lint`, `pnpm run test`, and `pnpm run build`.

## Current limits

The summary rebuild recalculates all published data; it is suitable for an MVP dataset but should become incremental or background work as volume grows. Near-duplicate question matching remains manual. AI interview practice and analysis, email verification, and production abuse controls are future work. The frontend reflects missing API capabilities instead of simulating them; see `docs/frontend-progress.md`.

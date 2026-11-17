# Frontend architecture proposal

The repository currently contains only the ASP.NET Core backend, solution files, tests and seed data. A `frontend/` directory at repository root preserves the existing `src/` backend layout.

Recommended default: React + TypeScript + Vite, TanStack Query, React Hook Form + Zod, Tailwind CSS and selected shadcn/ui components. Vite gives simple local API proxying and low setup cost. Next.js App Router would provide stronger server-rendered SEO and metadata, but adds server deployment and auth integration complexity. The framework decision awaits user approval.

Use a centralized typed API client with one source of endpoint paths, query key helpers, cancellation, error normalization, and mutation invalidation. Keep access token in memory during the tab session; a refresh will require login until a backend refresh-token or secure cookie design exists. Do not place a long-lived bearer token in `localStorage` by default.

The UI must reflect real backend data. Feed cards use `ExperienceListItem`, and detail screens dispatch by `kind` to candidate or interviewer endpoints. Company pages use the public company list, company-filtered experiences and threshold-protected reports. Question pages use the catalog. The submission wizard uses the candidate request DTO and displays pending moderation after a successful response.

The current API cannot support job title, answers, free-text experience search, outcome filter, owner submission list/edit/delete, or publication date. These must be omitted, clearly marked unavailable, or introduced through a separately approved backend contract change. Frontend authorization only controls presentation; backend roles remain authoritative.

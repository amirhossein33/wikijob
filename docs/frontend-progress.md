# Frontend progress — local MVP

## Implemented

- Approved React + Vite + TypeScript structure under `frontend/` in the same repository.
- Persian RTL community visual direction, responsive navigation and reusable UI primitives.
- Home, experience feed and detail, company list and detail, question list and detail, market reports, login, registration, candidate five-step submission, interviewer submission, interviewer advice editor, account area, private submission receipt, admin moderation queue, report-content dialog, privacy draft and 404 page.
- Typed API boundary for actual backend DTOs, bearer-token handling in memory, query caching/invalidation, URL filters and Persian error states.
- Backend API inventory and health report in `docs/`.
- Type check, lint, nine Vitest tests, all nine backend API integration suites, and production build completed. Browser-based visual/E2E testing is currently blocked by a saved computer-use origin permission for localhost; no visual verification is claimed.

## Backend contract limits reflected in UI

- No job title or answer fields are shown, since the API does not store them.
- Experience feed cannot perform backend free-text search, outcome filtering or arbitrary sorting.
- Own submissions have private detail by ID but no server-provided list or owner edit/delete API. The receipt explains this limitation.
- Public interviewer profiles can be opened by an opaque ID, but the API has no public profile listing.
- Browser refresh clears the in-memory JWT; users must log in again. A secure refresh-cookie flow would require a separately approved backend change.
- Vite's client-rendered pages have static HTML metadata. Full per-page server rendering and sitemap support would require a framework or rendering change.
- Privacy and terms text is a draft pending product/legal approval.

## Testing limits

The backend is local with test data in `InterviewHubDev`. Existing API integration scripts were rerun and passed. Frontend unit tests validate error handling, request authorization/cancellation, public privacy labels, safe links, and form boundaries. E2E and responsive browser inspection remain unverified because browser access to localhost was denied by a saved permission setting even after user approval in chat.

# Frontend API inventory

Source: ASP.NET Core controllers and DTOs in `src/InterviewHub.Api` and `src/InterviewHub.Business`, inspected 2026-11-17. JSON uses camelCase and string enums. There is no OpenAPI/Swagger registration. Runtime checks used the local development SQL Server database only.

| Endpoint | Request / response contract | Access | Main statuses | Frontend use / verification |
| --- | --- | --- | --- | --- |
| `GET /health` | `{status}` | Public | 200, 503 | Connection probe; executed 200 |
| `POST /api/auth/register` | `RegisterRequest` → `TokenResponse` | Public | 200, 400, 429 | Registration; integration test passed |
| `POST /api/auth/login` | `LoginRequest` → `TokenResponse` | Public | 200, 401, 429 | Login; integration test passed |
| `POST /api/auth/roles/{role}` | Role path → empty | Member/Admin | 204, 400, 401, 403 | Dual-role account; integration test passed |
| `GET /api/auth/me` | `AccountResponse` | Authenticated | 200, 401 | Account display; integration test passed |
| `GET /api/experiences` | `ExperienceSearchFilter` → `ExperienceSearchResponse` | Public | 200, 400 | Feed, pagination and supported filters; executed 200, integration test passed |
| `GET /api/companies` | `CompanyListItem[]` | Public | 200 | Company filter/list; executed 200 |
| `GET /api/candidate-experiences/{id}` | `PublicCandidateExperienceResponse` | Public | 200, 404 | Candidate detail; integration test passed |
| `GET /api/interviewer-experiences/{id}` | `PublicInterviewerExperienceResponse` | Public | 200, 404 | Interviewer detail; integration test passed |
| `GET /api/candidate-experiences/{id}/mine` | `PrivateCandidateExperienceResponse` | Owner/Admin | 200, 401, 404 | Private detail after submission; integration test passed |
| `GET /api/interviewer-experiences/{id}/mine` | `PrivateInterviewerExperienceResponse` | Owner/Admin | 200, 401, 404 | Private detail after submission; integration test passed |
| `POST /api/candidate-experiences` | `CreateCandidateExperienceRequest` → private detail | Candidate/Admin | 200, 400, 401, 403, 429 | Multi-stage candidate form; integration test passed |
| `POST /api/interviewer-experiences` | `CreateInterviewerExperienceRequest` → private detail | Interviewer/Admin | 200, 400, 401, 403, 429 | Interviewer form; integration test passed |
| `GET /api/questions` | `q`, `type`, `tagId`, `page`, `pageSize` → `QuestionSearchResponse` | Public | 200, 400 | Question library; executed 200 |
| `GET /api/questions/{id}` | `QuestionResponse` | Public | 200, 404 | Question detail; integration test passed |
| `POST /api/questions` | `CreateQuestionRequest` → `QuestionCreatedResponse` | Member/Admin | 200, 400, 401, 403 | Suggest question; integration test passed |
| `GET /api/tags` | `TagResponse[]` | Public | 200 | Filter choices; executed 200 |
| `PUT /api/interviewer-profiles/me` | `SaveInterviewerProfileRequest` → private profile | Interviewer/Admin | 200, 400, 401, 403 | Advice and priorities editor; integration test passed |
| `GET /api/interviewer-profiles/me` | `PrivateInterviewerProfileResponse` | Interviewer/Admin | 200, 401, 403, 404 | Own profile; integration test passed |
| `GET /api/interviewer-profiles/{publicId}` | `PublicInterviewerProfileResponse` | Public | 200, 404 | Public advice; integration test passed |
| `GET /api/reports/market` | `ReportFilterDto` → `MarketReportResponse` | Public | 200, 400 | Market dashboard; integration test passed |
| `GET /api/reports/companies/{id}` | `ReportFilterDto` → `CompanyReportResponse` | Public | 200, 400 | Company statistics; integration test passed |
| `GET /api/reports/companies/compare` | `left`, `right`, `ReportFilterDto` → comparison | Public | 200, 400 | Company comparison; integration test passed |
| `GET /api/reports/interviewers/priorities` | `PriorityReportRow[]` | Public | 200 | Aggregated interviewer advice; integration test passed |
| `POST /api/content-reports` | `CreateContentReportRequest` → `{id,status}` | Authenticated | 200, 400, 401, 404, 409, 429 | Flag published candidate experience; integration test passed |
| `/api/admin/*` | Moderation, tags, merge, report refresh contracts | Admin | 200/204, 400, 401, 403, 404, 409 | Admin interface if included; integration scripts passed |

## Filter and validation details

- Experience search supports `companyId`, `level`, `questionType`, `tagId`, `from`, `to`, `kind`, `page`, `pageSize`. `kind` is `candidate` or `interviewer`; page starts at 1 and page size is 1–100. The API orders by interview date; no free-text search, job-title filter, outcome filter, or sort parameter exists.
- Question search supports `q`, `type`, `tagId`, `page`, `pageSize`. Question types are `Algorithm`, `LiveCode`, `Conceptual`, `SystemDesign`.
- Candidate submission requires 1–20 stages. Each stage needs a nonfuture date, type, description and 1–50 questions. A question can supply a canonical `questionId` or `text` and `type`. Ratings and perceived difficulty are 1–5.
- Interviewer submission requires name and company even when visibility is off, a nonfuture interview date, a known level, a description, a 1–5 rating and at least one question.
- Public detail endpoints return only published content; private `/mine` endpoints require the exact ID. There is no own-experience listing, owner editing/deletion, job title, answer, or publication timestamp contract.
- Company reports suppress aggregates below five qualifying published candidate experiences. Seeded General questions have zero occurrences until published experiences reference them.
- Local CORS check from `http://localhost:5173` returned no allow-origin header because that origin is not configured. A Vite proxy or explicit `Cors__AllowedOrigins__0` is needed. This is a configuration matter, not a failing GET endpoint.

# InterviewHub project instructions

- Target .NET 10, ASP.NET Core Web API, SQL Server, EF Core Code First, and ASP.NET Core Identity.
- Keep the project dependency direction `InterviewHub.Api` → `InterviewHub.Business` → `InterviewHub.Data`. Services may use `DbContext` directly; do not add a repository layer without discussion.
- A user may participate as both candidate and interviewer. Do not model these as mutually exclusive Identity subclasses.
- Preserve the distinction between authenticated ownership and public anonymity. Public APIs and aggregate reports must respect visibility settings.
- Count question occurrences and calculate company ratings from published interview experiences. Seeded reference questions are not interview occurrences.
- Do not create or apply EF Core migrations, change a database, or seed runtime data until the user reviews and explicitly approves the model.
- Work in small stages. After each stage, summarize changes and validation, then wait for the user's approval before starting the next stage.
- Explain DTOs and mapping briefly when introducing them.

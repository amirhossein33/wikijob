# MonsterASP.NET deployment

The React build and .NET 10 API run from the same IIS website. Build the release ZIP with `pwsh work/package-host.ps1`, then extract its contents into the site's `/wwwroot` directory. The archive contains `web.config`, `InterviewHub.Api.dll`, and the React files under `wwwroot/`.

Apply the reviewed EF Core migration script to SQL Server before starting the app. Configure `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes`, and `ASPNETCORE_ENVIRONMENT` as site environment variables. Do not put secrets in Git or the ZIP. Set `Hosting__EnableHttpsRedirection=false` only for the temporary HTTP test deployment; enable it when HTTPS is active.

Check `/health`, public catalog and report endpoints, and Candidate/Interviewer registration and login using disposable test accounts. Verify a valid bearer token gets 200 from `/api/auth/me`, an unauthenticated request gets 401, and a non-admin gets 403 from `/api/auth/admin-check`. Disable ASP.NET Core stdout capture after diagnosing startup errors.

On the temporary HTTP deployment, do not use real credentials or the Admin account. The Admin account is created only when trusted `Admin:UserName` and `Admin:Password` configuration is supplied; provision it after HTTPS is active.

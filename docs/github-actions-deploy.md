# GitHub Actions deployment

The workflow at `.github/workflows/build-and-deploy.yml` runs on pushes to `main` and can also be started manually. It builds the .NET 10 solution, runs frontend lint and unit tests, runs the backend ownership/moderation acceptance script, then runs Playwright E2E scenarios against a disposable SQL Server and the real API/UI. Only after those checks pass does it package the API with the React build and publish it with MonsterASP.NET Web Deploy. After deployment it checks `http://karnama.runasp.net/health` for HTTP 200.

## Configure repository secrets

In the MonsterASP.NET control panel, open the website's **Deploy** page and enable **WebDeploy** on the Visual Studio access card. Add these repository secrets in GitHub under **Settings → Secrets and variables → Actions**:

| Secret                            | Value                                                              |
| --------------------------------- | ------------------------------------------------------------------ |
| `MONSTERASP_WEBSITE_NAME`         | The website name shown on the WebDeploy card, such as `site108713` |
| `MONSTERASP_SERVER_COMPUTER_NAME` | The full server URL, including port, shown on the card             |
| `MONSTERASP_SERVER_USERNAME`      | The WebDeploy login shown on the card                              |
| `MONSTERASP_SERVER_PASSWORD`      | The WebDeploy password                                             |

Until all four secrets exist, CI still runs and deployment is skipped. Do not put WebDeploy, database, JWT, or Admin credentials in workflow files or commit them to the repository.

## Database changes

The E2E job applies the already-reviewed EF Core migrations only to its disposable SQL Server container. The deploy job does not create or apply migrations to production. Review and apply a database migration through the separately approved database process before deploying application code that depends on a schema change. Production connection strings and JWT settings remain configured as hosting environment variables.

The workflow deploys without deleting files that are not in the build output, so runtime uploads and host-managed files are preserved.

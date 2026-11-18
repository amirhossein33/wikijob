$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'outputs/hosting'
$publish = Join-Path $output 'publish'
$archive = Join-Path $output 'interviewhub-site.zip'
$outputFull = [IO.Path]::GetFullPath($output)
$publishFull = [IO.Path]::GetFullPath($publish)
if (-not $publishFull.StartsWith($outputFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish directory must stay inside outputs/hosting.'
}

# Build the React app separately, then place its dist files under the API's web root.
pnpm --dir (Join-Path $root 'frontend') run build
if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }

New-Item -ItemType Directory -Force -Path $output | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
dotnet publish (Join-Path $root 'src/InterviewHub.Api/InterviewHub.Api.csproj') -c Release --no-restore -o $publish
if ($LASTEXITCODE -ne 0) { throw 'API publish failed.' }

$webRoot = Join-Path $publish 'wwwroot'
New-Item -ItemType Directory -Force -Path $webRoot | Out-Null
Copy-Item -Path (Join-Path $root 'frontend/dist/*') -Destination $webRoot -Recurse -Force

# Development settings refer to LocalDB and must never ship with the hosted package.
$developmentSettings = Join-Path $publish 'appsettings.Development.json'
if (Test-Path -LiteralPath $developmentSettings) { Remove-Item -LiteralPath $developmentSettings }
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive
Write-Output "Package: $archive"

$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5199/api'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Exercise authenticated reporting, duplicate protection, admin review, and role boundaries.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $args = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $args.Body = ($body | ConvertTo-Json -Depth 12 -Compress); $args.ContentType = 'application/json' }
    $response = Invoke-WebRequest @args
    $data = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}
function Expect($name, $result, $status) {
    if ($result.Status -ne $status) { throw "$name returned $($result.Status), expected $status" }
    Write-Output "$name : $status"
}
$candidate = Request POST auth/register @{ userName="reporter_$suffix"; email="reporter_$suffix@example.test"; password='CandidateTest!4826'; role='Candidate' }
Expect 'candidate registration' $candidate 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{ userNameOrEmail='admin'; password=$adminLine.Substring('Admin:Password = '.Length) }
Expect 'admin login' $admin 200
$question = Request POST questions @{ text="Content report question $suffix"; type='Conceptual' } $candidate.Data.accessToken
Expect 'question created' $question 200
$experience = Request POST candidate-experiences @{
    companyName="Report Abuse $suffix"; isAuthorAnonymous=$true; level='Mid'; outcome='Unknown'; description='Fixture'
    stages=@(@{type='Technical';date=(Get-Date).ToString('yyyy-MM-dd');description='Round';questions=@(@{questionId=$question.Data.id})})
} $candidate.Data.accessToken
Expect 'experience created' $experience 200
$body = @{ experienceId=$experience.Data.id; reason='Contains incorrect and misleading information' }
Expect 'pending content cannot be reported' (Request POST content-reports $body $candidate.Data.accessToken) 404
Expect 'experience published' (Request POST "admin/experiences/candidate/$($experience.Data.id)/publish" @{ reason='Reviewed' } $admin.Data.accessToken) 204
Expect 'anonymous report denied' (Request POST content-reports $body) 401
$report = Request POST content-reports $body $candidate.Data.accessToken
Expect 'report created' $report 200
Expect 'duplicate report rejected' (Request POST content-reports $body $candidate.Data.accessToken) 409
Expect 'member report list denied' (Request GET admin/content-reports $null $candidate.Data.accessToken) 403
$open = Request GET admin/content-reports $null $admin.Data.accessToken
Expect 'admin open reports' $open 200
if (@($open.Data | Where-Object id -eq $report.Data.id).Count -ne 1) { throw 'New report missing from moderation list' }
Expect 'report dismissed' (Request POST "admin/content-reports/$($report.Data.id)/dismiss" $null $admin.Data.accessToken) 204
Expect 'report cannot be reviewed twice' (Request POST "admin/content-reports/$($report.Data.id)/resolve" $null $admin.Data.accessToken) 409
$dismissed = Request GET 'admin/content-reports?status=Dismissed' $null $admin.Data.accessToken
Expect 'admin dismissed reports' $dismissed 200
if (@($dismissed.Data | Where-Object id -eq $report.Data.id).Count -ne 1) { throw 'Dismissed report missing' }
$another = Request POST auth/register @{ userName="reporter2_$suffix"; email="reporter2_$suffix@example.test"; password='CandidateTest!4826'; role='Candidate' }
Expect 'second reporter registration' $another 200
$secondReport = Request POST content-reports $body $another.Data.accessToken
Expect 'second report created' $secondReport 200
Expect 'second report resolved' (Request POST "admin/content-reports/$($secondReport.Data.id)/resolve" $null $admin.Data.accessToken) 204
$resolved = Request GET 'admin/content-reports?status=Resolved' $null $admin.Data.accessToken
Expect 'admin resolved reports' $resolved 200
if (@($resolved.Data | Where-Object id -eq $secondReport.Data.id).Count -ne 1) { throw 'Resolved report missing' }
Write-Output 'content reporting API : passed'

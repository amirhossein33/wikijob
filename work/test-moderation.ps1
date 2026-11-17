$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5187' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Exercise moderation over HTTP with real Identity roles and SQL Server persistence.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 10 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = $null
    if ($response.Content) {
        try { $data = $response.Content | ConvertFrom-Json }
        catch { throw "HTTP $([int]$response.StatusCode) at $path : $($response.Content.Substring(0, [Math]::Min(1000, $response.Content.Length)))" }
    }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}
function Expect($label, $result, $status) {
    if ($result.Status -ne $status) { throw "$label returned $($result.Status), expected $status. $($result.Data | ConvertTo-Json -Depth 6)" }
    Write-Output "$label : $status"
}

$member = Request POST auth/register @{userName="moderator_test_$suffix";email="moderator_$suffix@example.test";password='InterviewerTest!4826';role='Interviewer'}
Expect 'interviewer registration' $member 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'admin login' $admin 200
$submission = @{
    interviewerName='Hidden Name';companyName='Moderation Company';showInterviewerName=$false;showCompanyName=$false
    interviewDate=(Get-Date).ToString('yyyy-MM-dd');level='Senior';description='Original description.'
    interviewRating=4;questions=@(@{text='Original question?';type='Conceptual'})
}
$created = Request POST interviewer-experiences $submission $member.Data.accessToken
Expect 'create pending submission' $created 200
$id = $created.Data.id
$pending = Request GET 'admin/experiences/pending' $null $admin.Data.accessToken
Expect 'admin pending queue' $pending 200
if (@($pending.Data | Where-Object id -eq $id).Count -ne 1) { throw 'Submission missing from pending queue' }
Expect 'member moderation denied' (Request POST "admin/experiences/interviewer/$id/publish" @{reason='Reviewed'} $member.Data.accessToken) 403
Expect 'anonymous moderation denied' (Request POST "admin/experiences/interviewer/$id/publish" @{reason='Reviewed'}) 401
Expect 'pending not public' (Request GET "interviewer-experiences/$id") 404
Expect 'admin rejects' (Request POST "admin/experiences/interviewer/$id/reject" @{reason='Needs clearer wording'} $admin.Data.accessToken) 204
Expect 'rejected not public' (Request GET "interviewer-experiences/$id") 404
$submission.description='Edited and reviewed description.'
$submission.showCompanyName=$true
$edit = @{ experience=$submission; reason='Removed identifying details' }
Expect 'admin edits full submission' (Request PUT "admin/experiences/interviewer/$id" $edit $admin.Data.accessToken) 204
Expect 'admin publishes' (Request POST "admin/experiences/interviewer/$id/publish" @{reason='Safe to publish'} $admin.Data.accessToken) 204
$public = Request GET "interviewer-experiences/$id"
Expect 'edited submission public' $public 200
if ($null -ne $public.Data.interviewerName -or $public.Data.companyName -ne 'Moderation Company' -or $public.Data.description -ne 'Edited and reviewed description.') { throw 'Published edit/visibility incorrect' }
Expect 'admin soft deletes' (Request DELETE "admin/experiences/interviewer/$id" @{reason='Author removal request'} $admin.Data.accessToken) 204
Expect 'deleted not public' (Request GET "interviewer-experiences/$id") 404
Expect 'deleted cannot republish' (Request POST "admin/experiences/interviewer/$id/publish" @{reason='Retry'} $admin.Data.accessToken) 404
$history = Request GET "admin/experiences/interviewer/$id/history" $null $admin.Data.accessToken
Expect 'admin audit history' $history 200
if (@($history.Data).Count -ne 4) { throw "Expected 4 audit entries, got $(@($history.Data).Count)" }
Write-Output 'interviewer moderation and audit : passed'

# Candidate submission API arrives next; insert a minimal existing-model row to verify this stage's admin path.
$candidate = Request POST auth/register @{userName="candidate_mod_$suffix";email="candidate_mod_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
Expect 'candidate registration' $candidate 200
$candidateMe = Request GET auth/me $null $candidate.Data.accessToken
$candidateId = [Guid]::NewGuid().ToString()
$authorId = $candidateMe.Data.id
$sql = "INSERT INTO InterviewExperiences (Id,AuthorId,IsAuthorAnonymous,InterviewDate,CreatedAt,Level,Outcome,Status) VALUES ('$candidateId','$authorId',1,CONVERT(date,GETUTCDATE()),SYSDATETIMEOFFSET(),'Senior','Unknown','Pending')"
& sqlcmd -S '(localdb)\MSSQLLocalDB' -d InterviewHubDev -b -Q $sql | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not prepare candidate moderation fixture' }
Expect 'admin rejects candidate' (Request POST "admin/experiences/candidate/$candidateId/reject" @{reason='Needs explanation'} $admin.Data.accessToken) 204
$candidateEdit = @{
    experience=@{
        level='Senior';outcome='Passed';isAuthorAnonymous=$true;description='Reviewed candidate experience.'
        stages=@(@{type='Technical';date=(Get-Date).ToString('yyyy-MM-dd');description='Technical round';questions=@(@{text='Explain dependency injection';type='Conceptual'})})
    }
    reason='Completed missing explanation'
}
Expect 'admin edits candidate' (Request PUT "admin/experiences/candidate/$candidateId" $candidateEdit $admin.Data.accessToken) 204
Expect 'admin publishes candidate' (Request POST "admin/experiences/candidate/$candidateId/publish" @{reason='Approved'} $admin.Data.accessToken) 204
Expect 'admin deletes candidate' (Request DELETE "admin/experiences/candidate/$candidateId" @{reason='Removal request'} $admin.Data.accessToken) 204
$candidateHistory = Request GET "admin/experiences/candidate/$candidateId/history" $null $admin.Data.accessToken
Expect 'candidate audit history' $candidateHistory 200
if (@($candidateHistory.Data).Count -ne 4) { throw 'Candidate history missing actions' }
Write-Output 'candidate moderation and audit : passed'

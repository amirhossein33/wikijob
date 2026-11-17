$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5187' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Exercise the real HTTP API and assert status codes and privacy projections.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($body) { $params.Body = ($body | ConvertTo-Json -Depth 10 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    return @{ Status = [int]$response.StatusCode; Data = $(if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }) }
}
function Expect($label, $result, $status) {
    if ($result.Status -ne $status) { throw "$label returned $($result.Status), expected $status. $($result.Data | ConvertTo-Json -Depth 6)" }
    Write-Output "$label : $status"
}

$candidate = Request POST auth/register @{userName="cand_$suffix";email="cand_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
$interviewer = Request POST auth/register @{userName="inter_$suffix";email="inter_$suffix@example.test";password='InterviewerTest!4826';role='Interviewer'}
Expect 'candidate registration' $candidate 200
Expect 'interviewer registration' $interviewer 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'admin login' $admin 200

$body = @{
    interviewerName='Private Interviewer';companyName='Hidden Company';showInterviewerName=$false;showCompanyName=$false
    interviewDate=(Get-Date).ToString('yyyy-MM-dd');level='Senior';description='Discussed architecture and teamwork.'
    jobAdText='Backend role at Hidden Company';interviewRating=4
    questions=@(@{text='How would you design a cache?';type='SystemDesign'})
}
Expect 'anonymous creation blocked' (Request POST interviewer-experiences $body) 401
Expect 'candidate creation blocked' (Request POST interviewer-experiences $body $candidate.Data.accessToken) 403
$invalid = $body.Clone(); $invalid.Remove('questions')
Expect 'required questions enforced' (Request POST interviewer-experiences $invalid $interviewer.Data.accessToken) 400
$created = Request POST interviewer-experiences $body $interviewer.Data.accessToken
Expect 'interviewer creation' $created 200
$id = $created.Data.id
if ($created.Data.status -ne 'Pending' -or $created.Data.companyName -ne 'Hidden Company') { throw 'Owner response missing private submission' }
Expect 'pending not public' (Request GET "interviewer-experiences/$id") 404
Expect 'other user cannot read private' (Request GET "interviewer-experiences/$id/mine" $null $candidate.Data.accessToken) 404
$mine = Request GET "interviewer-experiences/$id/mine" $null $interviewer.Data.accessToken
Expect 'owner sees full submission' $mine 200
if ($mine.Data.interviewerName -ne 'Private Interviewer' -or $mine.Data.companyName -ne 'Hidden Company') { throw 'Owner private fields missing' }
Expect 'member cannot publish' (Request POST "admin/experiences/interviewer/$id/publish" @{reason='Reviewed'} $interviewer.Data.accessToken) 403
Expect 'admin publishes' (Request POST "admin/experiences/interviewer/$id/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$public = Request GET "interviewer-experiences/$id"
Expect 'published is public' $public 200
if ($null -ne $public.Data.interviewerName -or $null -ne $public.Data.companyName -or $null -ne $public.Data.jobAdText) { throw 'Hidden fields leaked' }
if ($public.Data.interviewRating -ne 4 -or $public.Data.questions.Count -ne 1) { throw 'Public rating or questions missing' }
Write-Output 'hidden name/company; public rating : passed'

$visible = $body.Clone(); $visible.interviewerName='Visible Interviewer'; $visible.companyName='Visible Company'; $visible.showInterviewerName=$true; $visible.showCompanyName=$true; $visible.jobAdText='Backend role'
$second = Request POST interviewer-experiences $visible $interviewer.Data.accessToken
Expect 'visible creation' $second 200
Expect 'admin publishes visible' (Request POST "admin/experiences/interviewer/$($second.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$visiblePublic = Request GET "interviewer-experiences/$($second.Data.id)"
Expect 'visible published' $visiblePublic 200
if ($visiblePublic.Data.interviewerName -ne 'Visible Interviewer' -or $visiblePublic.Data.companyName -ne 'Visible Company' -or $visiblePublic.Data.interviewRating -ne 4) { throw 'Visible fields or rating incorrect' }
Write-Output 'visible name/company; public rating : passed'

# Verify that each visibility switch operates independently.
foreach ($visibility in @(@($false, $true), @($true, $false))) {
    $mixed = $body.Clone()
    $mixed.interviewerName = 'Mixed Interviewer'
    $mixed.companyName = 'Mixed Company'
    $mixed.showInterviewerName = $visibility[0]
    $mixed.showCompanyName = $visibility[1]
    $mixed.jobAdText = 'Backend role'
    $item = Request POST interviewer-experiences $mixed $interviewer.Data.accessToken
    Expect 'mixed creation' $item 200
    Expect 'mixed publication' (Request POST "admin/experiences/interviewer/$($item.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
    $result = Request GET "interviewer-experiences/$($item.Data.id)"
    Expect 'mixed public read' $result 200
    if (($null -ne $result.Data.interviewerName) -ne $visibility[0] -or ($null -ne $result.Data.companyName) -ne $visibility[1]) { throw 'Independent visibility switches failed' }
    if ($result.Data.interviewRating -ne 4) { throw 'Rating was hidden by a visibility switch' }
}
Write-Output 'independent visibility switches : passed'

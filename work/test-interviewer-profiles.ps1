$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:5199/api'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Exercise every new profile endpoint with roles, moderation, privacy, and priority validation.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 12 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = $null
    if ($response.Content) {
        try { $data = $response.Content | ConvertFrom-Json }
        catch { throw "HTTP $([int]$response.StatusCode) at $path : $($response.Content.Substring(0, [Math]::Min(800, $response.Content.Length)))" }
    }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}
function Expect($label, $result, $status) {
    if ($result.Status -ne $status) { throw "$label returned $($result.Status), expected $status. $($result.Data | ConvertTo-Json -Depth 7)" }
    Write-Output "$label : $status"
}

$interviewer = Request POST auth/register @{userName="profile_inter_$suffix";email="profile_inter_$suffix@example.test";password='InterviewerTest!4826';role='Interviewer'}
$candidate = Request POST auth/register @{userName="profile_cand_$suffix";email="profile_cand_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
Expect 'interviewer registered' $interviewer 200
Expect 'candidate registered' $candidate 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'admin login' $admin 200
Expect 'missing profile' (Request GET interviewer-profiles/me $null $interviewer.Data.accessToken) 404
$body = @{
    displayName='Private Interviewer';companyName='Profile Company';bio='Private biography'
    adviceText='Focus on architecture and explain tradeoffs.';showName=$false;showCompany=$false;showBio=$false
    priorities=@(@{skill='Architecture';importance=5;note='Explain decisions'},@{skill='Syntax';importance=1;note='Lookups are fine'})
}
Expect 'anonymous save denied' (Request PUT interviewer-profiles/me $body) 401
Expect 'candidate save denied' (Request PUT interviewer-profiles/me $body $candidate.Data.accessToken) 403
$invalid = $body.Clone(); $invalid.priorities=@()
Expect 'empty priorities rejected' (Request PUT interviewer-profiles/me $invalid $interviewer.Data.accessToken) 400
$invalid = $body.Clone(); $invalid.priorities=@(@{skill='Architecture';importance=7})
Expect 'importance range enforced' (Request PUT interviewer-profiles/me $invalid $interviewer.Data.accessToken) 400
$invalid = $body.Clone(); $invalid.priorities=@(@{skill=$null;importance=4})
Expect 'missing skill rejected' (Request PUT interviewer-profiles/me $invalid $interviewer.Data.accessToken) 400
$invalid = $body.Clone(); $invalid.priorities=@(@{skill='Architecture';importance=5},@{skill='architecture';importance=3})
Expect 'duplicate skill rejected' (Request PUT interviewer-profiles/me $invalid $interviewer.Data.accessToken) 400
$saved = Request PUT interviewer-profiles/me $body $interviewer.Data.accessToken
Expect 'owner saves profile' $saved 200
$userId = $saved.Data.userId
$publicId = $saved.Data.publicId
if ($userId -eq $publicId -or $saved.Data.status -ne 'Pending' -or $saved.Data.priorities.Count -ne 2) { throw 'Private profile or public ID invalid' }
Expect 'owner reads private profile' (Request GET interviewer-profiles/me $null $interviewer.Data.accessToken) 200
Expect 'pending profile hidden' (Request GET "interviewer-profiles/$publicId") 404
Expect 'candidate admin preview denied' (Request GET "admin/interviewer-profiles/$userId" $null $candidate.Data.accessToken) 403
$pending = Request GET admin/interviewer-profiles/pending $null $admin.Data.accessToken
Expect 'admin pending list' $pending 200
if (@($pending.Data | Where-Object userId -eq $userId).Count -ne 1) { throw 'Profile missing from pending list' }
$preview = Request GET "admin/interviewer-profiles/$userId" $null $admin.Data.accessToken
Expect 'admin private preview' $preview 200
if ($preview.Data.displayName -ne 'Private Interviewer' -or $preview.Data.companyName -ne 'Profile Company') { throw 'Admin preview missing stored fields' }
Expect 'member cannot publish' (Request POST "admin/interviewer-profiles/$userId/publish" @{reason='Reviewed'} $interviewer.Data.accessToken) 403
Expect 'admin rejects profile' (Request POST "admin/interviewer-profiles/$userId/reject" @{reason='Clarify biography'} $admin.Data.accessToken) 204
Expect 'rejected profile hidden' (Request GET "interviewer-profiles/$publicId") 404
$body.bio='Reviewed biography'
$body.priorities=@(@{skill='Architecture';importance=4;note='Explain decisions'},@{skill='Communication';importance=5;note='Talk through choices'})
$updated = Request PUT interviewer-profiles/me $body $interviewer.Data.accessToken
Expect 'owner revises priorities' $updated 200
if ($updated.Data.status -ne 'Pending' -or @($updated.Data.priorities | Where-Object skill -eq 'Syntax').Count -ne 0) { throw 'Priority replacement failed' }
Expect 'admin publishes profile' (Request POST "admin/interviewer-profiles/$userId/publish" @{reason='Reviewed revision'} $admin.Data.accessToken) 204
$public = Request GET "interviewer-profiles/$publicId"
Expect 'published profile public' $public 200
if ($null -ne $public.Data.displayName -or $null -ne $public.Data.companyName -or $null -ne $public.Data.bio -or $public.Data.PSObject.Properties.Name -contains 'userId') { throw 'Private identity leaked' }
if ($public.Data.adviceText -ne 'Focus on architecture and explain tradeoffs.' -or $public.Data.priorities.Count -ne 2) { throw 'Advice or priorities missing' }
Write-Output 'hidden fields and public advice/priorities : passed'

# Each visibility flag is independently tested through owner edits and admin approval.
foreach ($visibility in @(@($true,$false,$false),@($false,$true,$false),@($false,$false,$true),@($true,$true,$true))) {
    $body.showName=$visibility[0]; $body.showCompany=$visibility[1]; $body.showBio=$visibility[2]
    $revision = Request PUT interviewer-profiles/me $body $interviewer.Data.accessToken
    Expect 'owner updates visibility' $revision 200
    Expect 'revision hidden pending review' (Request GET "interviewer-profiles/$publicId") 404
    Expect 'admin approves visibility' (Request POST "admin/interviewer-profiles/$userId/publish" @{reason='Privacy checked'} $admin.Data.accessToken) 204
    $shown = Request GET "interviewer-profiles/$publicId"
    Expect 'visibility public read' $shown 200
    if (($null -ne $shown.Data.displayName) -ne $visibility[0] -or ($null -ne $shown.Data.companyName) -ne $visibility[1] -or ($null -ne $shown.Data.bio) -ne $visibility[2]) { throw 'Independent visibility failed' }
}
Write-Output 'independent name/company/bio controls : passed'
$history = Request GET "admin/interviewer-profiles/$userId/history" $null $admin.Data.accessToken
Expect 'admin profile history' $history 200
if (@($history.Data).Count -ne 6) { throw 'Expected reject plus five approvals in history' }
Expect 'admin deletes profile' (Request DELETE "admin/interviewer-profiles/$userId" @{reason='Removal request'} $admin.Data.accessToken) 204
Expect 'deleted profile hidden' (Request GET "interviewer-profiles/$publicId") 404
Expect 'deleted owner update rejected' (Request PUT interviewer-profiles/me $body $interviewer.Data.accessToken) 409
Expect 'deleted cannot republish' (Request POST "admin/interviewer-profiles/$userId/publish" @{reason='Retry'} $admin.Data.accessToken) 404
Write-Output 'profile moderation and deletion : passed'

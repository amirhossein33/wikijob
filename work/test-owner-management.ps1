$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5187' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# This acceptance script uses disposable local accounts and validates API ownership and moderation.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 15 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = $null
    if ($response.Content) { $data = $response.Content | ConvertFrom-Json }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}

function Expect($label, $result, $status) {
    if ($result.Status -ne $status) {
        $payload = if ($result.Data) { $result.Data | ConvertTo-Json -Depth 8 -Compress } else { '' }
        throw "$label returned $($result.Status), expected $status. $payload"
    }
    Write-Output "$label : $status"
}

function Read-Secret($key) {
    $line = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like "$key = *" } | Select-Object -First 1
    if (-not $line) { throw "Required local user-secret '$key' is not configured." }
    return $line.Substring("$key = ".Length)
}

$candidateName = "owner_candidate_$suffix"
$candidatePassword = 'OwnerCandidate!4826'
$interviewerName = "owner_interviewer_$suffix"
$interviewerPassword = 'OwnerInterviewer!4826'
$otherCandidateName = "owner_other_candidate_$suffix"
$otherCandidatePassword = 'OtherCandidate!4826'
$candidate = Request POST auth/register @{userName=$candidateName;email="$candidateName@example.test";password=$candidatePassword;role='Candidate'}
$interviewer = Request POST auth/register @{userName=$interviewerName;email="$interviewerName@example.test";password=$interviewerPassword;role='Interviewer'}
$otherCandidate = Request POST auth/register @{userName=$otherCandidateName;email="$otherCandidateName@example.test";password=$otherCandidatePassword;role='Candidate'}
Expect 'candidate registration' $candidate 200
Expect 'interviewer registration' $interviewer 200
Expect 'second Candidate registration' $otherCandidate 200
Expect 'public Admin registration blocked' (Request POST auth/register @{userName="blocked_admin_$suffix";email="blocked_admin_$suffix@example.test";password='BlockedAdmin!4826';role='Admin'}) 400

# Admin identity is bootstrapped only from trusted backend User Secrets in this local test run.
$adminUserName = Read-Secret 'Admin:UserName'
$adminPassword = Read-Secret 'Admin:Password'
$admin = Request POST auth/login @{userNameOrEmail=$adminUserName;password=$adminPassword}
Expect 'backend-provisioned Admin login' $admin 200
Expect 'anonymous admin endpoint denied' (Request GET auth/admin-check) 401
Expect 'Candidate blocked from admin endpoint' (Request GET auth/admin-check $null $candidate.Data.accessToken) 403
Expect 'Admin can access admin endpoint' (Request GET auth/admin-check $null $admin.Data.accessToken) 200

$today = (Get-Date).ToString('yyyy-MM-dd')
$candidateBody = @{
    companyName="Candidate Test Company $suffix";isAuthorAnonymous=$true;level='Senior';outcome='Passed'
    description='Architecture discussion and practical experience.';jobAdText='Backend .NET role';
    jobAdUrl='https://example.test/jobs/backend';interviewerConductRating=4;jobAdAlignmentRating=5;ratingReason='Questions matched the backend role.'
    stages=@(@{type='Technical';date=$today;description='Technical interview';questions=@(@{text="Explain dependency injection $suffix";type='Conceptual';perceivedDifficulty=3})})
}
Expect 'anonymous Candidate submission denied' (Request POST candidate-experiences $candidateBody) 401
Expect 'Interviewer blocked from Candidate submission' (Request POST candidate-experiences $candidateBody $interviewer.Data.accessToken) 403
$candidateCreated = Request POST candidate-experiences $candidateBody $candidate.Data.accessToken
Expect 'Candidate creates experience' $candidateCreated 200
$candidateId = $candidateCreated.Data.id
Expect 'Candidate sees own list' (Request GET candidate-experiences/mine $null $candidate.Data.accessToken) 200
Expect 'Interviewer cannot access Candidate owner detail' (Request GET "candidate-experiences/$candidateId/mine" $null $interviewer.Data.accessToken) 404
Expect 'another Candidate cannot edit this experience' (Request PUT "candidate-experiences/$candidateId/mine" $candidateBody $otherCandidate.Data.accessToken) 404
Expect 'another Candidate cannot delete this experience' (Request DELETE "candidate-experiences/$candidateId/mine" $null $otherCandidate.Data.accessToken) 404
Expect 'Admin cannot use Candidate owner edit route without Candidate role' (Request PUT "candidate-experiences/$candidateId/mine" $candidateBody $admin.Data.accessToken) 403
Expect 'Pending Candidate experience is private' (Request GET "candidate-experiences/$candidateId") 404
Expect 'Admin rejection requires a reason' (Request POST "admin/experiences/candidate/$candidateId/reject" @{reason=''} $admin.Data.accessToken) 400
Expect 'Admin rejects Candidate experience' (Request POST "admin/experiences/candidate/$candidateId/reject" @{reason='Please clarify the interview stage.'} $admin.Data.accessToken) 204
$candidateHistory = Request GET "admin/experiences/candidate/$candidateId/history" $null $admin.Data.accessToken
Expect 'Admin reads Candidate moderation history' $candidateHistory 200
if (-not (@($candidateHistory.Data | Where-Object { $_.reason -eq 'Please clarify the interview stage.' }).Count)) { throw 'Rejection reason is missing from the moderation audit.' }
$candidatePrivate = Request GET "candidate-experiences/$candidateId/mine" $null $candidate.Data.accessToken
Expect 'Candidate sees rejection state' $candidatePrivate 200
if ($candidatePrivate.Data.status -ne 'Rejected') { throw 'Candidate rejection status was not retained.' }
Expect 'Rejected Candidate content stays private' (Request GET "candidate-experiences/$candidateId") 404
Expect 'Admin approves Candidate experience' (Request POST "admin/experiences/candidate/$candidateId/publish" @{reason='Reviewed and approved.'} $admin.Data.accessToken) 204
Expect 'Approved Candidate content is public' (Request GET "candidate-experiences/$candidateId") 200
Expect 'Market report after Candidate approval' (Request GET reports/market) 200
$candidateCompany = Request GET companies
$company = @($candidateCompany.Data | Where-Object { $_.name -eq $candidateBody.companyName } | Select-Object -First 1)
if ($company.Count -ne 1) { throw 'Published Candidate company is missing from company catalog.' }
$companyReport = Request GET "reports/companies/$($company[0].id)"
Expect 'Company report after Candidate approval' $companyReport 200
$candidateBody.description = 'Owner updated the experience.'
$candidateUpdated = Request PUT "candidate-experiences/$candidateId/mine" $candidateBody $candidate.Data.accessToken
Expect 'Candidate edits own experience' $candidateUpdated 200
if ($candidateUpdated.Data.status -ne 'Pending' -or $candidateUpdated.Data.description -ne $candidateBody.description) { throw 'Candidate edit did not reset moderation or update the description.' }
Expect 'Edited Candidate content returns to review' (Request GET "candidate-experiences/$candidateId") 404
$candidateList = Request GET candidate-experiences/mine $null $candidate.Data.accessToken
if (@($candidateList.Data | Where-Object id -eq $candidateId).Count -ne 1) { throw 'Candidate list omitted the owned record.' }
Expect 'Admin approves edited Candidate experience' (Request POST "admin/experiences/candidate/$candidateId/publish" @{reason='Owner changes reviewed.'} $admin.Data.accessToken) 204
Expect 'Candidate soft deletes own experience' (Request DELETE "candidate-experiences/$candidateId/mine" $null $candidate.Data.accessToken) 204
Expect 'Owner-deleted Candidate content is private' (Request GET "candidate-experiences/$candidateId") 404
Expect 'Owner cannot delete another Candidate experience' (Request DELETE "candidate-experiences/$candidateId/mine" $null $interviewer.Data.accessToken) 403
Write-Output 'Candidate owner listing, edit, deletion and moderation : passed'

$interviewerBody = @{
    interviewerName='Hidden Interviewer';companyName="Interviewer Test Company $suffix";showInterviewerName=$false;showCompanyName=$false
    interviewDate=$today;level='Mid';description='The candidate demonstrated system design skills.';jobAdText='Backend position';
    jobAdUrl='https://example.test/jobs/interviewer';interviewRating=4
    questions=@(@{text="Design a scalable cache $suffix";type='SystemDesign'})
}
Expect 'Candidate blocked from Interviewer submission' (Request POST interviewer-experiences $interviewerBody $candidate.Data.accessToken) 403
$interviewerCreated = Request POST interviewer-experiences $interviewerBody $interviewer.Data.accessToken
Expect 'Interviewer creates experience' $interviewerCreated 200
$interviewerId = $interviewerCreated.Data.id
$interviewerList = Request GET interviewer-experiences/mine $null $interviewer.Data.accessToken
Expect 'Interviewer sees own list' $interviewerList 200
if (@($interviewerList.Data | Where-Object id -eq $interviewerId).Count -ne 1) { throw 'Interviewer list omitted the owned record.' }
Expect 'Candidate cannot access Interviewer owner detail' (Request GET "interviewer-experiences/$interviewerId/mine" $null $candidate.Data.accessToken) 404
Expect 'another account cannot edit Interviewer experience' (Request PUT "interviewer-experiences/$interviewerId/mine" $interviewerBody $candidate.Data.accessToken) 403
Expect 'Pending Interviewer content is private' (Request GET "interviewer-experiences/$interviewerId") 404
$interviewerBody.description = 'Updated interviewer advice about architecture.'
$interviewerUpdated = Request PUT "interviewer-experiences/$interviewerId/mine" $interviewerBody $interviewer.Data.accessToken
Expect 'Interviewer edits own experience' $interviewerUpdated 200
if ($interviewerUpdated.Data.status -ne 'Pending' -or $interviewerUpdated.Data.description -ne $interviewerBody.description) { throw 'Interviewer edit did not reset moderation or update the description.' }
Expect 'Admin rejects Interviewer experience with reason' (Request POST "admin/experiences/interviewer/$interviewerId/reject" @{reason='Clarify this observation.'} $admin.Data.accessToken) 204
Expect 'Admin approves Interviewer experience' (Request POST "admin/experiences/interviewer/$interviewerId/publish" @{reason='Reviewed and approved.'} $admin.Data.accessToken) 204
$interviewerPublic = Request GET "interviewer-experiences/$interviewerId"
Expect 'Approved Interviewer content is public' $interviewerPublic 200
if ($null -ne $interviewerPublic.Data.interviewerName -or $null -ne $interviewerPublic.Data.companyName -or $interviewerPublic.Data.interviewRating -ne 4) { throw 'Interviewer public visibility or mandatory rating is incorrect.' }
$interviewerBody.description = 'A published edit requires renewed review.'
$interviewerUpdated = Request PUT "interviewer-experiences/$interviewerId/mine" $interviewerBody $interviewer.Data.accessToken
Expect 'Interviewer edits a published experience' $interviewerUpdated 200
if ($interviewerUpdated.Data.status -ne 'Pending') { throw 'Published edit did not return to Pending.' }
Expect 'Published Interviewer edit is hidden during review' (Request GET "interviewer-experiences/$interviewerId") 404
Expect 'Admin approves revised Interviewer experience' (Request POST "admin/experiences/interviewer/$interviewerId/publish" @{reason='Revised submission approved.'} $admin.Data.accessToken) 204
Expect 'Interviewer soft deletes own experience' (Request DELETE "interviewer-experiences/$interviewerId/mine" $null $interviewer.Data.accessToken) 204
Expect 'Owner-deleted Interviewer content is private' (Request GET "interviewer-experiences/$interviewerId") 404
Write-Output 'Interviewer owner listing, edit, deletion and moderation : passed'

$prioritySkill = "Architecture $suffix"
$profileBody = @{
    displayName='Hidden Senior Interviewer';companyName="Profile Test Company $suffix";bio='Backend developer';adviceText='Prioritize architecture and communication.'
    showName=$false;showCompany=$false;showBio=$false;priorities=@(@{skill=$prioritySkill;importance=5;note='Discuss tradeoffs.'})
}
Expect 'Candidate cannot save interviewer profile' (Request PUT interviewer-profiles/me $profileBody $candidate.Data.accessToken) 403
$profile = Request PUT interviewer-profiles/me $profileBody $interviewer.Data.accessToken
Expect 'Interviewer saves advice and priorities' $profile 200
Expect 'Interviewer reads own advice' (Request GET interviewer-profiles/me $null $interviewer.Data.accessToken) 200
Expect 'Admin rejects advice profile with reason' (Request POST "admin/interviewer-profiles/$($profile.Data.userId)/reject" @{reason='Add a concrete example.'} $admin.Data.accessToken) 204
Expect 'Rejected advice remains private' (Request GET "interviewer-profiles/$($profile.Data.publicId)") 404
$profile = Request PUT interviewer-profiles/me $profileBody $interviewer.Data.accessToken
Expect 'Interviewer edits rejected advice' $profile 200
Expect 'Admin approves advice profile' (Request POST "admin/interviewer-profiles/$($profile.Data.userId)/publish" @{reason='Advice reviewed.'} $admin.Data.accessToken) 204
$publicProfile = Request GET "interviewer-profiles/$($profile.Data.publicId)"
Expect 'Approved advice is public' $publicProfile 200
if ($publicProfile.Data.adviceText -ne $profileBody.adviceText -or $publicProfile.Data.priorities[0].skill -ne $prioritySkill -or $null -ne $publicProfile.Data.displayName) { throw 'Public advice or visibility controls failed.' }
$priorityReport = Request GET reports/interviewers/priorities
Expect 'Interviewer priorities report' $priorityReport 200
if (@($priorityReport.Data | Where-Object { $_.skill -eq $prioritySkill }).Count) { throw 'A priority from fewer than five profiles escaped the aggregate privacy threshold.' }
$profile = Request PUT interviewer-profiles/me $profileBody $interviewer.Data.accessToken
Expect 'Interviewer can edit advice after publication' $profile 200
if ($profile.Data.status -ne 'Pending') { throw 'Edited public advice did not return to moderation.' }
Expect 'Revised advice is private pending review' (Request GET "interviewer-profiles/$($profile.Data.publicId)") 404
Expect 'Admin republishes revised advice' (Request POST "admin/interviewer-profiles/$($profile.Data.userId)/publish" @{reason='Revised advice reviewed.'} $admin.Data.accessToken) 204
Expect 'Interviewer deletes advice profile' (Request DELETE interviewer-profiles/me $null $interviewer.Data.accessToken) 204
Expect 'Deleted profile is unavailable to owner API' (Request GET interviewer-profiles/me $null $interviewer.Data.accessToken) 404
Expect 'Deleted advice is unavailable publicly' (Request GET "interviewer-profiles/$($profile.Data.publicId)") 404
Expect 'Deleted profile removed from priority aggregates' (Request GET reports/interviewers/priorities) 200
$remainingPriority = Request GET reports/interviewers/priorities
if (@($remainingPriority.Data | Where-Object { $_.skill -eq $prioritySkill }).Count) { throw 'Deleted profile still contributes to interviewer priority aggregates.' }
Expect 'Admin report refresh requires Admin' (Request POST admin/reports/refresh $null $candidate.Data.accessToken) 403
Expect 'Admin refreshes report summaries' (Request POST admin/reports/refresh $null $admin.Data.accessToken) 204
Write-Output 'Interviewer advice view, edit, publication and deletion : passed'

Write-Output 'Admin, owner and moderation acceptance : passed'

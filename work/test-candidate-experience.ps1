$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5187' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Exercise each candidate-experience endpoint and its role, validation, privacy, and moderation paths.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 15 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = $null
    if ($response.Content) {
        try { $data = $response.Content | ConvertFrom-Json }
        catch { throw "HTTP $([int]$response.StatusCode) at $path : $($response.Content.Substring(0, [Math]::Min(800, $response.Content.Length)))" }
    }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}
function Expect($label, $result, $status) {
    if ($result.Status -ne $status) { throw "$label returned $($result.Status), expected $status. $($result.Data | ConvertTo-Json -Depth 8)" }
    Write-Output "$label : $status"
}

$candidate = Request POST auth/register @{userName="candidate_stage_$suffix";email="candidate_stage_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
$other = Request POST auth/register @{userName="other_stage_$suffix";email="other_stage_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
$interviewer = Request POST auth/register @{userName="inter_stage_$suffix";email="inter_stage_$suffix@example.test";password='InterviewerTest!4826';role='Interviewer'}
Expect 'candidate registration' $candidate 200
Expect 'other registration' $other 200
Expect 'interviewer registration' $interviewer 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'admin login' $admin 200

$today = (Get-Date).ToString('yyyy-MM-dd')
$firstStage = @{type='HR';date=$today;description='HR screening';questions=@(@{text='How do you handle feedback?';type='Conceptual';perceivedDifficulty=2})}
$secondStage = @{type='Technical';date=$today;description='Backend technical round';questions=@(@{text='How do you handle feedback?';type='Conceptual';perceivedDifficulty=3},@{text='Design an API cache';type='SystemDesign';perceivedDifficulty=4})}
$body = @{
    isAuthorAnonymous=$true;level='Unknown';outcome='Passed';description='Two round interview'
    jobAdText='Backend position';interviewerConductRating=4;jobAdAlignmentRating=5
    ratingReason='The interview matched the ad';stages=@($firstStage,$secondStage)
}
Expect 'anonymous creation denied' (Request POST candidate-experiences $body) 401
Expect 'interviewer-only creation denied' (Request POST candidate-experiences $body $interviewer.Data.accessToken) 403
$invalid = $body.Clone(); $invalid.stages = @()
Expect 'missing stages rejected' (Request POST candidate-experiences $invalid $candidate.Data.accessToken) 400
$invalidDate = $body.Clone(); $invalidDate.stages = @(@{type='HR';date='2099-01-01';description='Future';questions=@(@{text='Question?';type='Conceptual'})})
Expect 'future stage date rejected' (Request POST candidate-experiences $invalidDate $candidate.Data.accessToken) 400
$created = Request POST candidate-experiences $body $candidate.Data.accessToken
Expect 'multi-stage creation' $created 200
$id = $created.Data.id
if ($created.Data.status -ne 'Pending' -or $created.Data.stages.Count -ne 2 -or $created.Data.authorName -ne "candidate_stage_$suffix") { throw 'Private creation response incomplete' }
if ($created.Data.stages[0].questions[0].questionId -ne $created.Data.stages[1].questions[0].questionId) { throw 'Same question was not reused across stages' }
Expect 'pending not public' (Request GET "candidate-experiences/$id") 404
Expect 'other user cannot inspect private' (Request GET "candidate-experiences/$id/mine" $null $other.Data.accessToken) 404
Expect 'anonymous private read denied' (Request GET "candidate-experiences/$id/mine") 401
$private = Request GET "candidate-experiences/$id/mine" $null $candidate.Data.accessToken
Expect 'owner reads complete submission' $private 200
if ($private.Data.authorName -ne "candidate_stage_$suffix" -or $private.Data.stages.Count -ne 2) { throw 'Owner view incomplete' }
$adminPrivate = Request GET "candidate-experiences/$id/mine" $null $admin.Data.accessToken
Expect 'admin reads complete submission' $adminPrivate 200

$editedStage = $secondStage.Clone()
$editedStage.questions = @($secondStage.questions) + @(@{text='Explain transaction isolation';type='Conceptual';perceivedDifficulty=3})
$edit = $body.Clone(); $edit.description='Admin reviewed description'; $edit.stages=@($editedStage)
Expect 'admin edits stages' (Request PUT "admin/experiences/candidate/$id" @{experience=$edit;reason='Clarified one stage'} $admin.Data.accessToken) 204
Expect 'candidate cannot publish' (Request POST "admin/experiences/candidate/$id/publish" @{reason='Approve'} $candidate.Data.accessToken) 403
Expect 'admin publishes' (Request POST "admin/experiences/candidate/$id/publish" @{reason='Reviewed content'} $admin.Data.accessToken) 204
$public = Request GET "candidate-experiences/$id"
Expect 'published experience public' $public 200
if ($null -ne $public.Data.authorName -or $public.Data.PSObject.Properties.Name -contains 'authorId') { throw 'Anonymous identity leaked' }
if ($public.Data.description -ne 'Admin reviewed description' -or $public.Data.stages.Count -ne 1 -or $public.Data.stages[0].questions.Count -ne 3 -or $public.Data.interviewerConductRating -ne 4) { throw 'Public edit, stages, questions, or rating incorrect' }
Expect 'admin deletes' (Request DELETE "admin/experiences/candidate/$id" @{reason='Removal request'} $admin.Data.accessToken) 204
Expect 'deleted not public' (Request GET "candidate-experiences/$id") 404
Write-Output 'candidate experience API and privacy : passed'

# A non-anonymous submission exposes the username and optional company only after publication.
$visible = $body.Clone()
$visible.isAuthorAnonymous = $false
$visible.companyName = 'Candidate Test Company'
$visible.stages = @($firstStage)
$visibleCreated = Request POST candidate-experiences $visible $candidate.Data.accessToken
Expect 'named submission created' $visibleCreated 200
Expect 'named submission approved' (Request POST "admin/experiences/candidate/$($visibleCreated.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$visiblePublic = Request GET "candidate-experiences/$($visibleCreated.Data.id)"
Expect 'named submission public' $visiblePublic 200
if ($visiblePublic.Data.authorName -ne "candidate_stage_$suffix" -or $visiblePublic.Data.companyName -ne 'Candidate Test Company') { throw 'Visible author or company missing' }
Write-Output 'named author and company visibility : passed'

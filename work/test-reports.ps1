$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5199' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Verify SQL summary refresh, report filters, comparison, and five-sample privacy gates.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 12 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = $null
    if ($response.Content) {
        try { $data = $response.Content | ConvertFrom-Json }
        catch { throw "HTTP $([int]$response.StatusCode) at $path : $($response.Content.Substring(0, [Math]::Min(700, $response.Content.Length)))" }
    }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}
function Expect($label, $result, $status) {
    if ($result.Status -ne $status) { throw "$label returned $($result.Status), expected $status. $($result.Data | ConvertTo-Json -Depth 8)" }
    Write-Output "$label : $status"
}

$candidate = Request POST auth/register @{userName="report_cand_$suffix";email="report_cand_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
Expect 'candidate registered' $candidate 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'admin login' $admin 200
$question = Request POST questions @{text="Explain report isolation ${suffix}?";type='Conceptual'} $candidate.Data.accessToken
$tag = Request POST tags @{name="ReportTag-$suffix"} $admin.Data.accessToken
Expect 'question created' $question 200
Expect 'tag created' $tag 200
Expect 'tag assigned' (Request PUT "questions/$($question.Data.id)/tags" @{tagIds=@($tag.Data.id)} $admin.Data.accessToken) 204
$today = (Get-Date).ToString('yyyy-MM-dd')
$companies = @("Report Left $suffix", "Report Right $suffix")
$ids = @()
$firstExperience = $null
foreach ($companyIndex in 0..1) {
    for ($i=1; $i -le 5; $i++) {
        $outcome = if (($companyIndex -eq 0 -and $i -le 3) -or ($companyIndex -eq 1 -and $i -eq 1)) { 'Passed' } else { 'Rejected' }
        $body = @{
            companyName=$companies[$companyIndex];isAuthorAnonymous=$true;level='Senior';outcome=$outcome
            interviewerConductRating=$i;jobAdAlignmentRating=4;description='Report fixture'
            stages=@(@{type='Technical';date=$today;description='Technical round';questions=@(@{questionId=$question.Data.id;perceivedDifficulty=3})})
        }
        $experience = Request POST candidate-experiences $body $candidate.Data.accessToken
        Expect 'candidate report fixture created' $experience 200
        if ($companyIndex -eq 0 -and $i -eq 1) { $firstExperience = $experience.Data.id }
        Expect 'candidate report fixture published' (Request POST "admin/experiences/candidate/$($experience.Data.id)/publish" @{reason='Reviewed fixture'} $admin.Data.accessToken) 204
        if ($companyIndex -eq 0 -and $i -eq 1) {
            $companyList = Request GET companies
            $ids += (@($companyList.Data | Where-Object name -eq $companies[0])[0].id)
            $small = Request GET "reports/companies/$($ids[0])"
            Expect 'small company report' $small 200
            if ($small.Data.available -or $null -ne $small.Data.experienceCount) { throw 'Sub-five company sample leaked' }
        }
    }
    if ($companyIndex -eq 1) {
        $companyList = Request GET companies
        $ids += (@($companyList.Data | Where-Object name -eq $companies[1])[0].id)
    }
}
$left = Request GET "reports/companies/$($ids[0])?level=Senior&type=Conceptual&tagId=$($tag.Data.id)&from=$today&to=$today"
Expect 'left company report' $left 200
if (!$left.Data.available -or $left.Data.experienceCount -ne 5 -or $left.Data.passRate -ne 0.6 -or $left.Data.conductRating -ne 3 -or $left.Data.adAlignmentRating -ne 4 -or $left.Data.topQuestions[0].experienceCount -ne 5 -or $left.Data.difficultyDistribution.'3' -ne 5) { throw 'Left report aggregates incorrect' }
$right = Request GET "reports/companies/$($ids[1])"
Expect 'right company report' $right 200
if ($right.Data.passRate -ne 0.2) { throw 'Right pass rate incorrect' }
$compare = Request GET "reports/companies/compare?left=$($ids[0])&right=$($ids[1])"
Expect 'company comparison' $compare 200
if ($compare.Data.left.passRate -ne 0.6 -or $compare.Data.right.passRate -ne 0.2) { throw 'Company comparison incorrect' }
Expect 'same-company comparison rejected' (Request GET "reports/companies/compare?left=$($ids[0])&right=$($ids[0])") 400
$market = Request GET "reports/market?type=Conceptual&tagId=$($tag.Data.id)&from=$today&to=$today"
Expect 'filtered market report' $market 200
if (@($market.Data.topQuestions | Where-Object questionId -eq $question.Data.id).Count -ne 1 -or $market.Data.trendMetric -ne 'QuestionMentions') { throw 'Market question or trend metric missing' }
Expect 'bad report date rejected' (Request GET 'reports/market?from=2026-11-18&to=2026-11-17') 400
Expect 'member refresh denied' (Request POST admin/reports/refresh $null $candidate.Data.accessToken) 403
Expect 'admin refresh' (Request POST admin/reports/refresh $null $admin.Data.accessToken) 204
Expect 'first experience rejected' (Request POST "admin/experiences/candidate/$firstExperience/reject" @{reason='Re-review'} $admin.Data.accessToken) 204
$suppressed = Request GET "reports/companies/$($ids[0])"
Expect 'company report after rejection' $suppressed 200
if ($suppressed.Data.available) { throw 'Summary was not refreshed after rejection' }
Expect 'first experience republished' (Request POST "admin/experiences/candidate/$firstExperience/publish" @{reason='Reapproved'} $admin.Data.accessToken) 204

$profileIds = @()
$profileTokens = @()
for ($i=1; $i -le 5; $i++) {
    $interviewer = Request POST auth/register @{userName="report_inter_${suffix}_$i";email="report_inter_${suffix}_$i@example.test";password='InterviewerTest!4826';role='Interviewer'}
    Expect 'interviewer registered' $interviewer 200
    $profileTokens += $interviewer.Data.accessToken
    $profileBody = @{displayName="Profile $i";adviceText='Explain architecture';priorities=@(@{skill="Architecture-$suffix";importance=$i})}
    $profile = Request PUT interviewer-profiles/me $profileBody $interviewer.Data.accessToken
    Expect 'profile saved' $profile 200
    $profileIds += $profile.Data.userId
    Expect 'profile published' (Request POST "admin/interviewer-profiles/$($profile.Data.userId)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
}
$priority = Request GET reports/interviewers/priorities
Expect 'priority aggregate' $priority 200
$row = @($priority.Data | Where-Object skill -eq "Architecture-$suffix")
if ($row.Count -ne 1 -or $row[0].profileCount -ne 5 -or $row[0].averageImportance -ne 3) { throw 'Priority aggregate incorrect' }
$editProfile = @{displayName='Edited Profile';adviceText='Explain architecture';priorities=@(@{skill="Architecture-$suffix";importance=5})}
Expect 'published profile edited' (Request PUT interviewer-profiles/me $editProfile $profileTokens[0]) 200
$priorityPending = Request GET reports/interviewers/priorities
if (@($priorityPending.Data | Where-Object skill -eq "Architecture-$suffix").Count -ne 0) { throw 'Sub-five priority leaked after edit' }
Expect 'edited profile republished' (Request POST "admin/interviewer-profiles/$($profileIds[0])/publish" @{reason='Reviewed edit'} $admin.Data.accessToken) 204
$priorityRestored = Request GET reports/interviewers/priorities
if (@($priorityRestored.Data | Where-Object skill -eq "Architecture-$suffix").Count -ne 1) { throw 'Priority not restored after approval' }
Write-Output 'physical SQL reports, refresh, and privacy thresholds : passed'

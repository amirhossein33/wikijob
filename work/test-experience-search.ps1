$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5199' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Public list tests cover pagination, all agreed filters, and company visibility.
function Request($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $body) { $params.Body = ($body | ConvertTo-Json -Depth 12 -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}
function Expect($label, $result, $status) {
    if ($result.Status -ne $status) { throw "$label returned $($result.Status), expected $status. $($result.Data | ConvertTo-Json -Depth 7)" }
    Write-Output "$label : $status"
}
$candidate = Request POST auth/register @{userName="list_cand_$suffix";email="list_cand_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
$interviewer = Request POST auth/register @{userName="list_inter_$suffix";email="list_inter_$suffix@example.test";password='InterviewerTest!4826';role='Interviewer'}
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'candidate login' $candidate 200
Expect 'interviewer login' $interviewer 200
Expect 'admin login' $admin 200
$company = "List Company $suffix"
$questionText = "What is SQL isolation ${suffix}?"
$q = Request POST questions @{text=$questionText;type='Conceptual'} $candidate.Data.accessToken
$tag = Request POST tags @{name="ListTag-$suffix"} $admin.Data.accessToken
Expect 'question created' $q 200
Expect 'tag created' $tag 200
Expect 'question tagged' (Request PUT "questions/$($q.Data.id)/tags" @{tagIds=@($tag.Data.id)} $admin.Data.accessToken) 204
$today = (Get-Date).ToString('yyyy-MM-dd')
$candBody = @{companyName=$company;isAuthorAnonymous=$true;level='Senior';stages=@(@{type='Technical';date=$today;description='SQL round';questions=@(@{questionId=$q.Data.id})})}
$cand = Request POST candidate-experiences $candBody $candidate.Data.accessToken
Expect 'candidate experience created' $cand 200
Expect 'candidate experience published' (Request POST "admin/experiences/candidate/$($cand.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$interBody = @{interviewerName='Private Interviewer';companyName=$company;showInterviewerName=$false;showCompanyName=$true;interviewDate=$today;level='Senior';description='SQL interview';interviewRating=4;questions=@(@{text=$questionText;type='Conceptual'})}
$interVisible = Request POST interviewer-experiences $interBody $interviewer.Data.accessToken
Expect 'visible-company interview created' $interVisible 200
Expect 'visible-company interview published' (Request POST "admin/experiences/interviewer/$($interVisible.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$interBody.showCompanyName=$false
$interHidden = Request POST interviewer-experiences $interBody $interviewer.Data.accessToken
Expect 'hidden-company interview created' $interHidden 200
Expect 'hidden-company interview published' (Request POST "admin/experiences/interviewer/$($interHidden.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$companies = Request GET companies
Expect 'public company list' $companies 200
$companyRow = @($companies.Data | Where-Object name -eq $company)
if ($companyRow.Count -ne 1) { throw 'Published company not listed exactly once' }
$companyId = $companyRow[0].id
$filtered = Request GET "experiences?companyId=$companyId&level=Senior&questionType=Conceptual&tagId=$($tag.Data.id)&from=$today&to=$today&page=1&pageSize=10"
Expect 'combined filters' $filtered 200
if ($filtered.Data.totalCount -ne 2 -or @($filtered.Data.items | Where-Object kind -eq 'candidate').Count -ne 1 -or @($filtered.Data.items | Where-Object kind -eq 'interviewer').Count -ne 1) { throw 'Combined filters or hidden-company protection failed' }
$tagOnly = Request GET "experiences?tagId=$($tag.Data.id)&page=1&pageSize=1"
Expect 'tag filter and pagination' $tagOnly 200
if ($tagOnly.Data.totalCount -ne 3 -or @($tagOnly.Data.items).Count -ne 1) { throw "Tag filter or pagination failed: total=$($tagOnly.Data.totalCount), items=$(@($tagOnly.Data.items).Count)" }
$hiddenRead = Request GET "interviewer-experiences/$($interHidden.Data.id)"
Expect 'hidden company detail' $hiddenRead 200
if ($null -ne $hiddenRead.Data.companyName) { throw 'Hidden company leaked in detail' }
Expect 'invalid kind rejected' (Request GET 'experiences?kind=other') 400
Expect 'invalid date range rejected' (Request GET 'experiences?from=2026-11-18&to=2026-11-17') 400
Write-Output 'experience filters, pagination, and privacy : passed'

$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5187' }
$base = "http://127.0.0.1:$port/api"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Test every question/tag endpoint using real HTTP requests and published experience counts.
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
function New-Experience($questionId, $token, $twice = $false) {
    $today = (Get-Date).ToString('yyyy-MM-dd')
    $stage = @{type='Technical';date=$today;description='Technical round';questions=@(@{questionId=$questionId;perceivedDifficulty=3})}
    $stages = if ($twice) { @($stage, @{type='SystemDesign';date=$today;description='Second round';questions=@(@{questionId=$questionId;perceivedDifficulty=4})}) } else { @($stage) }
    return Request POST candidate-experiences @{isAuthorAnonymous=$true;level='Senior';stages=@($stages)} $token
}

$candidate = Request POST auth/register @{userName="q_candidate_$suffix";email="q_candidate_$suffix@example.test";password='CandidateTest!4826';role='Candidate'}
$interviewer = Request POST auth/register @{userName="q_inter_$suffix";email="q_inter_$suffix@example.test";password='InterviewerTest!4826';role='Interviewer'}
Expect 'candidate login' $candidate 200
Expect 'interviewer login' $interviewer 200
$adminLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$admin = Request POST auth/login @{userNameOrEmail='admin';password=$adminLine.Substring('Admin:Password = '.Length)}
Expect 'admin login' $admin 200
$questionText = "Explain EF tracking ${suffix}?"
$query = [uri]::EscapeDataString($questionText)
Expect 'public search' (Request GET "questions?q=$query") 200
Expect 'public tag list' (Request GET tags) 200
Expect 'anonymous question creation denied' (Request POST questions @{text=$questionText;type='Conceptual'}) 401
Expect 'invalid question rejected' (Request POST questions @{text='  ';type='Conceptual'} $candidate.Data.accessToken) 400
Expect 'missing type rejected' (Request POST questions @{text="No type $suffix"} $candidate.Data.accessToken) 400
$created = Request POST questions @{text=$questionText;type='Conceptual'} $candidate.Data.accessToken
Expect 'candidate creates question' $created 200
$target = $created.Data.id
$duplicate = Request POST questions @{text=$questionText;type='Conceptual'} $interviewer.Data.accessToken
Expect 'exact duplicate reused' $duplicate 200
if ($duplicate.Data.id -ne $target -or !$duplicate.Data.alreadyExisted) { throw 'Exact duplicate was not reused' }
$hiddenSearch = Request GET "questions?q=$query"
if ($hiddenSearch.Data.totalCount -ne 0) { throw "Unpublished question appeared in search: $($hiddenSearch.Data | ConvertTo-Json -Depth 6)" }
Expect 'unpublished question hidden' (Request GET "questions/$target") 404
Expect 'member tag creation denied' (Request POST tags @{name="EF-$suffix"} $candidate.Data.accessToken) 403
$tag = Request POST tags @{name="EF-$suffix"} $admin.Data.accessToken
Expect 'admin creates tag' $tag 200
$tagAgain = Request POST tags @{name="EF-$suffix"} $admin.Data.accessToken
Expect 'existing tag reused' $tagAgain 200
if ($tagAgain.Data.id -ne $tag.Data.id) { throw 'Tag duplicate was not reused' }
Expect 'member tag assignment denied' (Request PUT "questions/$target/tags" @{tagIds=@($tag.Data.id)} $candidate.Data.accessToken) 403
Expect 'unknown tag rejected' (Request PUT "questions/$target/tags" @{tagIds=@([Guid]::NewGuid().ToString())} $admin.Data.accessToken) 400
Expect 'admin assigns tag' (Request PUT "questions/$target/tags" @{tagIds=@($tag.Data.id)} $admin.Data.accessToken) 204

$experience1 = New-Experience $target $candidate.Data.accessToken
Expect 'selected question used in experience' $experience1 200
if ($experience1.Data.stages[0].questions[0].questionId -ne $target) { throw 'Question selection did not preserve ID' }
if ((Request GET "questions?q=$query").Data.totalCount -ne 0) { throw 'Pending experience affected search' }
Expect 'admin publishes first experience' (Request POST "admin/experiences/candidate/$($experience1.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
$found = Request GET "questions?q=$query&type=Conceptual&tagId=$($tag.Data.id)&page=1&pageSize=10"
Expect 'filtered search' $found 200
if ($found.Data.totalCount -ne 1 -or $found.Data.items[0].askedCount -ne 1) { throw 'First published count or filter failed' }
$details = Request GET "questions/$target"
Expect 'question detail' $details 200
if ($details.Data.tags.Count -ne 1 -or $details.Data.askedCount -ne 1) { throw 'Question detail incorrect' }
$experience2 = New-Experience $target $candidate.Data.accessToken $true
Expect 'same question in two rounds' $experience2 200
Expect 'admin publishes second experience' (Request POST "admin/experiences/candidate/$($experience2.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
if ((Request GET "questions/$target").Data.askedCount -ne 2) { throw 'One experience counted more than once' }

$source = Request POST questions @{text="Explain EF change tracking ${suffix}?";type='Conceptual'} $interviewer.Data.accessToken
Expect 'near duplicate created' $source 200
$sourceTag = Request POST tags @{name="Tracking-$suffix"} $admin.Data.accessToken
Expect 'source tag created' $sourceTag 200
Expect 'source tag assigned' (Request PUT "questions/$($source.Data.id)/tags" @{tagIds=@($sourceTag.Data.id)} $admin.Data.accessToken) 204
$sourceExperience = New-Experience $source.Data.id $candidate.Data.accessToken
Expect 'source question selected' $sourceExperience 200
Expect 'admin publishes source experience' (Request POST "admin/experiences/candidate/$($sourceExperience.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
Expect 'member merge denied' (Request POST admin/questions/merge @{sourceId=$source.Data.id;targetId=$target} $candidate.Data.accessToken) 403
Expect 'same question merge rejected' (Request POST admin/questions/merge @{sourceId=$target;targetId=$target} $admin.Data.accessToken) 400
Expect 'admin merges near duplicate' (Request POST admin/questions/merge @{sourceId=$source.Data.id;targetId=$target} $admin.Data.accessToken) 204
$merged = Request GET "questions/$target"
Expect 'merged question detail' $merged 200
if ($merged.Data.askedCount -ne 3 -or $merged.Data.tags.Count -ne 2) { throw 'Merge did not transfer occurrences and tags' }
$alias = Request GET "questions/$($source.Data.id)"
Expect 'old question ID resolves' $alias 200
if ($alias.Data.id -ne $target) { throw 'Old question ID did not resolve to target' }
if ((Request GET "questions?q=$([uri]::EscapeDataString('Explain EF change tracking'))").Data.totalCount -ne 0) { throw 'Merged source remained in search' }
$aliasExperience = New-Experience $source.Data.id $candidate.Data.accessToken
Expect 'old ID can be selected' $aliasExperience 200
if ($aliasExperience.Data.stages[0].questions[0].questionId -ne $target) { throw 'Old ID was not mapped during submission' }
Expect 'alias experience published' (Request POST "admin/experiences/candidate/$($aliasExperience.Data.id)/publish" @{reason='Reviewed'} $admin.Data.accessToken) 204
if ((Request GET "questions/$target").Data.askedCount -ne 4) { throw 'Alias occurrence not counted' }
Expect 'first experience rejected' (Request POST "admin/experiences/candidate/$($experience1.Data.id)/reject" @{reason='Re-review'} $admin.Data.accessToken) 204
if ((Request GET "questions/$target").Data.askedCount -ne 3) { throw 'Rejected experience still counted' }
Expect 'source experience deleted' (Request DELETE "admin/experiences/candidate/$($sourceExperience.Data.id)" @{reason='Removed'} $admin.Data.accessToken) 204
if ((Request GET "questions/$target").Data.askedCount -ne 2) { throw 'Deleted experience still counted' }
$differentType = Request POST questions @{text="Algorithm $suffix";type='Algorithm'} $candidate.Data.accessToken
Expect 'different type created' $differentType 200
Expect 'cross-type merge rejected' (Request POST admin/questions/merge @{sourceId=$differentType.Data.id;targetId=$target} $admin.Data.accessToken) 400
Expect 'invalid page rejected' (Request GET 'questions?page=0') 400
Write-Output 'question catalog, tags, counts, and merge : passed'

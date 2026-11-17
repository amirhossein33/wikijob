$ErrorActionPreference = 'Stop'
$port = if ($env:INTERVIEWHUB_TEST_PORT) { $env:INTERVIEWHUB_TEST_PORT } else { '5187' }
$base = "http://127.0.0.1:$port/api/auth"
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 8)

# Send only authentication requests and retain status codes for the acceptance check.
function Invoke-Auth($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$base/$path"; Method = $method; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($body) { $params.Body = ($body | ConvertTo-Json -Compress); $params.ContentType = 'application/json' }
    $response = Invoke-WebRequest @params
    $data = if ($response.Content) { $response.Content | ConvertFrom-Json } else { $null }
    return @{ Status = [int]$response.StatusCode; Data = $data }
}

function Assert-Status($label, $actual, $expected) {
    if ($actual -ne $expected) { throw "$label returned $actual, expected $expected" }
    Write-Output "$label : $actual"
}

$candidate = Invoke-Auth POST register @{ userName = "candidate_$suffix"; email = "candidate_$suffix@example.test"; password = 'CandidateTest!4826'; role = 'Candidate' }
Assert-Status 'candidate register' $candidate.Status 200
$interviewer = Invoke-Auth POST register @{ userName = "interviewer_$suffix"; email = "interviewer_$suffix@example.test"; password = 'InterviewerTest!4826'; role = 'Interviewer' }
Assert-Status 'interviewer register' $interviewer.Status 200
$candidateLogin = Invoke-Auth POST login @{ userNameOrEmail = "candidate_$suffix"; password = 'CandidateTest!4826' }
Assert-Status 'candidate login' $candidateLogin.Status 200
$interviewerLogin = Invoke-Auth POST login @{ userNameOrEmail = "interviewer_$suffix"; password = 'InterviewerTest!4826' }
Assert-Status 'interviewer login' $interviewerLogin.Status 200
$adminSecretLine = dotnet user-secrets list --project src/InterviewHub.Api | Where-Object { $_ -like 'Admin:Password = *' }
$adminPassword = $adminSecretLine.Substring('Admin:Password = '.Length)
$adminLogin = Invoke-Auth POST login @{ userNameOrEmail = 'admin'; password = $adminPassword }
Assert-Status 'admin login' $adminLogin.Status 200

foreach ($entry in @(@('candidate', $candidateLogin.Data), @('interviewer', $interviewerLogin.Data), @('admin', $adminLogin.Data))) {
    if ([string]::IsNullOrWhiteSpace($entry[1].accessToken)) { throw "$($entry[0]) has no access token" }
    $me = Invoke-Auth GET me $null $entry[1].accessToken
    Assert-Status "$($entry[0]) token /me" $me.Status 200
    if ($me.Data.roles.Count -lt 1) { throw "$($entry[0]) has no role" }
}

Assert-Status 'missing token' (Invoke-Auth GET me).Status 401
Assert-Status 'invalid token' (Invoke-Auth GET me $null 'invalid.token.value').Status 401
Assert-Status 'wrong password' (Invoke-Auth POST login @{ userNameOrEmail = "candidate_$suffix"; password = 'WrongPassword!4826' }).Status 401
Assert-Status 'member blocked from admin' (Invoke-Auth GET admin-check $null $candidateLogin.Data.accessToken).Status 403
Assert-Status 'admin allowed' (Invoke-Auth GET admin-check $null $adminLogin.Data.accessToken).Status 200
Assert-Status 'add second role' (Invoke-Auth POST roles/Interviewer $null $candidateLogin.Data.accessToken).Status 204
$dualLogin = Invoke-Auth POST login @{ userNameOrEmail = "candidate_$suffix"; password = 'CandidateTest!4826' }
Assert-Status 'dual-role login' $dualLogin.Status 200
if (@($dualLogin.Data.roles | Sort-Object) -join ',' -ne 'Candidate,Interviewer') { throw 'Dual-role token missing a role' }
Write-Output 'dual-role token : Candidate,Interviewer'

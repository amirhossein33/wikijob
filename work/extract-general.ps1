$ErrorActionPreference = 'Stop'
$source = 'C:\Users\Admin\Downloads\questionParsa.docx'
$destination = 'src/InterviewHub.Data/Seed/general-questions.json'

# This curated index list selects 100 question paragraphs, excluding repeated topics and challenge instructions.
$selected = @(
    (0..20), (22..36), (39..43),
    @(47,48,52,54,56,57,59,60,62,64,67),
    @(72,73,74,75,76), @(78,79,81,82),
    @(86,87,89,91,93,95), @(98), (100..115),
    @(118,119,120,122,123),
    @(142,143,144,148,151,153,154,160,165,176,197)
)
$indices = @($selected | ForEach-Object { $_ })
if ($indices.Count -ne 100 -or @($indices | Sort-Object -Unique).Count -ne 100) {
    throw "Expected 100 unique selected indices, got $($indices.Count)."
}

Add-Type -AssemblyName System.IO.Compression
$archive = [IO.Compression.ZipFile]::OpenRead($source)
try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('word/document.xml').Open())
    try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('w', 'http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $allQuestions = @($document.SelectNodes('//w:body/w:p | //w:body/w:tbl//w:p', $ns) |
        ForEach-Object { ($_.SelectNodes('.//w:t', $ns) | ForEach-Object { $_.InnerText }) -join '' } |
        Where-Object { $_ -match '[؟?]' } |
        ForEach-Object { ($_ -replace '\s*\((پاسخ|توضیح)\)\s*$', '').Trim() } |
        Where-Object { $_.Length -ge 25 -and $_.Length -le 260 -and $_ -notmatch 'UI|UX|Responsive|Adaptive|Git|Branch|Merge|Rebase' } |
        Select-Object -Unique)
} finally { $archive.Dispose() }

# The topic tag is based on the source section; types are assigned by the question's task.
function Topic([int]$index) {
    if ($index -le 6 -or $index -in @(39,40,41,42,67,142,143,144)) { return 'ASP.NET Core' }
    if ($index -in @(15,16,17,18,19,20,22,52,54,78,79,81,82,107,108,109,110,111,112,113,160)) { return 'EF Core' }
    if ($index -in @(23,24,25,26,27,28,29,56,57,86,87,114,115,118,119,120,165)) { return 'Architecture' }
    if ($index -in @(30,31,59,60,89)) { return 'SQL Server' }
    if ($index -in @(32,62,91)) { return 'Testing' }
    if ($index -in @(33,34,64,93,176)) { return 'Security' }
    if ($index -in @(35,36,95,122,123)) { return 'Performance' }
    return 'C#'
}
$items = @($indices | ForEach-Object {
    $index = [int]$_
    $value = $allQuestions[$index]
    $value = $value.Replace('ي','ی').Replace('ك','ک')
    $value = ($value -replace '^\s*[0-9۰-۹]+\s*[-–]\s*', '' -replace '\s+', ' ').Trim()
    $topic = Topic $index
    $type = if ($index -eq 197) { 'Algorithm' }
        elseif ($index -in @(10,13,75)) { 'LiveCode' }
        elseif ($topic -eq 'Architecture' -or $index -in @(89,122)) { 'SystemDesign' }
        else { 'Conceptual' }
    [pscustomobject]@{ text = $value; type = $type; tags = @($topic) }
})
if (@($items.text | Sort-Object -Unique).Count -ne 100) { throw 'Curated questions contain an exact duplicate.' }
New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
$items | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $destination -Encoding utf8
Write-Output "Prepared $($items.Count) General questions."
$items | Group-Object type | Select-Object Name,Count

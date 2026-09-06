param(
    [Parameter(Mandatory=$true)] [string]$Source,
    [string]$Output = (Join-Path $PSScriptRoot '..\data\ru_recognition_150k.txt')
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$corePath = Join-Path $project 'data\ru_50k.txt'
$outputPath = [IO.Path]::GetFullPath($Output)

$core = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($line in [IO.File]::ReadLines($corePath)) {
    [void]$core.Add($line.Split(' ')[0])
}

$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$result = [Collections.Generic.List[string]]::new()
foreach ($line in [IO.File]::ReadLines($sourcePath)) {
    $parts = $line.Split(' ')
    if ($parts.Length -lt 2) { continue }
    $word = $parts[0]
    if ($word -cnotmatch '^[а-яё]+$' -or -not $seen.Add($word)) { continue }
    if (-not $core.Contains($word)) { $result.Add("$word $($parts[1])") }
    if ($seen.Count -eq 150000) { break }
}
if ($seen.Count -ne 150000) { throw "Source contains only $($seen.Count) usable unique words." }

[IO.File]::WriteAllLines($outputPath, $result, [Text.UTF8Encoding]::new($false))
Write-Output "Wrote $($result.Count) additional entries to $outputPath"

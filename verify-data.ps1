$ErrorActionPreference = 'Stop'
$data = Join-Path $PSScriptRoot 'data'
$failed = 0
foreach ($line in Get-Content -LiteralPath (Join-Path $data 'SHA256SUMS.txt')) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "Invalid SHA256SUMS line: $line" }
    $expected = $Matches[1]
    $name = $Matches[2]
    $path = Join-Path $data $name
    if (-not (Test-Path -LiteralPath $path)) { Write-Output "FAIL | missing $name"; $failed++; continue }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -eq $expected) { Write-Output "PASS | $name" }
    else { Write-Output "FAIL | $name | expected $expected | actual $actual"; $failed++ }
}
if ($failed -ne 0) { throw "$failed data integrity checks failed." }

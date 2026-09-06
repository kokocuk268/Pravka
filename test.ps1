$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'Pravka.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 first.' }
$folder = Join-Path $PSScriptRoot '.build'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$report = Join-Path $folder 'tests.txt'
$process = Start-Process -FilePath $exe -ArgumentList '--self-test', ('"{0}"' -f $report) -WindowStyle Hidden -Wait -PassThru
if (-not (Test-Path -LiteralPath $report)) { throw 'The test process did not write a report.' }
Get-Content -LiteralPath $report -Encoding UTF8
if ($process.ExitCode -ne 0) { throw "Tests failed (exit $($process.ExitCode))." }

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'verify-data.ps1')
& (Join-Path $PSScriptRoot 'test.ps1')
& (Join-Path $PSScriptRoot 'quality-test.ps1')

$version = '0.4.1'
$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path $PSScriptRoot ('.build\release-' + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $stage 'Pravka'
New-Item -ItemType Directory -Force -Path $dist, $package | Out-Null
foreach ($name in @('Pravka.exe', 'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md', 'verify-data.ps1', 'test.ps1', 'quality-test.ps1', 'integration-test.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $package
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'data') -Destination $package -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $package -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets') -Destination $package -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'tools') -Destination $package -Recurse
$zip = Join-Path $dist "Pravka-$version-win-x64.zip"
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $dist 'SHA256SUMS.txt'), "$hash  $([IO.Path]::GetFileName($zip))`n", [Text.Encoding]::ASCII)
Write-Output "Release: $zip"

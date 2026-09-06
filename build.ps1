$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$build = Join-Path $PSScriptRoot '.build'
$assets = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force -Path $build, $assets | Out-Null
$iconMaker = Join-Path $build 'IconMaker.exe'
$icon = Join-Path $assets 'pravka.ico'
& (Join-Path $framework 'csc.exe') /nologo /target:exe "/out:$iconMaker" /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tools\IconMaker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon build failed' }
& $iconMaker $icon
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed' }
$source = Get-ChildItem (Join-Path $PSScriptRoot 'src\*.cs') | ForEach-Object FullName
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/out:$PSScriptRoot\Pravka.exe" "/win32icon:$icon" "/win32manifest:$PSScriptRoot\app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$framework\WPF\UIAutomationClient.dll" "/reference:$framework\WPF\UIAutomationTypes.dll" "/reference:$framework\WPF\WindowsBase.dll" $source
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

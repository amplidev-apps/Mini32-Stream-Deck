param([switch]$Test)
$ErrorActionPreference='Stop'
& (Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe') -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Prepare-Branding.ps1')
if($LASTEXITCODE -ne 0){throw 'Falha ao preparar a identidade visual.'}
$libraryPath=Join-Path $PSScriptRoot '../prototype/audio-obs/deps/naudio/lib/net35/NAudio.dll'
if (-not (Test-Path -LiteralPath $libraryPath)) { & (Join-Path $PSScriptRoot '../prototype/audio-obs/Build.ps1') }
$outputPath=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Copy-Item -LiteralPath $libraryPath -Destination $outputPath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../prototype/audio-obs/THIRD-PARTY-NOTICES.txt') -Destination $outputPath -Force
$compilerPath=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$programPath=Join-Path $outputPath 'Mini32.Engine.exe'
$sources=@(Get-ChildItem (Join-Path $PSScriptRoot 'src/*.cs') | ForEach-Object FullName)
$sources+=@( (Join-Path $PSScriptRoot '../prototype/midi-diagnostic/MidiDiagnostic.cs'), (Join-Path $PSScriptRoot '../prototype/audio-obs/AudioEngine.cs'), (Join-Path $PSScriptRoot '../prototype/audio-obs/ObsClient.cs') )
& $compilerPath /nologo /target:exe /main:Mini32.Program /platform:x64 /optimize+ "/win32icon:$(Join-Path $PSScriptRoot 'assets/mini32.ico')" "/out:$programPath" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Security.dll "/r:$libraryPath" $sources
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o motor.'}
if($Test){& $programPath --test;if($LASTEXITCODE -ne 0){throw 'Teste do motor falhou.'}}
Write-Output "Motor pronto: $programPath"

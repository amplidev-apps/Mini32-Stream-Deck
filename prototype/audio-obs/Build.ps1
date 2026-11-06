param([switch]$Run, [switch]$Test, [switch]$UiTest, [switch]$DeviceTest)
$ErrorActionPreference = 'Stop'
$packageRoot = Join-Path $PSScriptRoot 'deps/naudio'
$libraryPath = Join-Path $packageRoot 'lib/net35/NAudio.dll'
if (-not (Test-Path -LiteralPath $libraryPath)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'deps') -Force | Out-Null
    $archivePath = Join-Path $PSScriptRoot 'deps/naudio.1.10.0.zip'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/naudio/1.10.0/naudio.1.10.0.nupkg' -OutFile $archivePath
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne 'E4A80EED41DAD794695F9C114432DE4667D20BDFA80825AA94D159708CF29217') { throw 'O pacote baixado nao corresponde ao hash esperado.' }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $packageRoot -Force
}
$outputPath = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Copy-Item -LiteralPath $libraryPath -Destination (Join-Path $outputPath 'NAudio.dll') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.txt') -Destination $outputPath -Force
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$programPath = Join-Path $outputPath 'Mini32Lab.exe'
$sources = @((Join-Path $PSScriptRoot '../midi-diagnostic/MidiDiagnostic.cs'), (Join-Path $PSScriptRoot 'AudioEngine.cs'), (Join-Path $PSScriptRoot 'ObsClient.cs'), (Join-Path $PSScriptRoot 'Lab.cs'), (Join-Path $PSScriptRoot 'Checks.cs'))
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')" "/out:$programPath" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$libraryPath" $sources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o prototipo.' }
if ($Test -or $UiTest -or $DeviceTest) {
    $testMode = if($Test){'--self-test'}elseif($UiTest){'--ui-test'}else{'--device-test'}
    $testProcess = Start-Process -FilePath $programPath -ArgumentList $testMode -PassThru -Wait -WindowStyle Hidden
    if($testProcess.ExitCode -ne 0){throw 'Teste falhou. Consulte logs/test-result.txt ou startup-error.txt.'}
}
if ($Run) { Start-Process -FilePath $programPath -WindowStyle Normal }
Write-Output "Pronto: $programPath"

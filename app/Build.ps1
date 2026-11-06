param([switch]$Run,[switch]$SkipTests,[string]$PackageName='Mini32 Stream Deck 0.3.0-dev.2',[string]$FontSourceDir='')
$ErrorActionPreference='Stop'
if($PackageName -match '[\\/:*?"<>|]' -or [string]::IsNullOrWhiteSpace($PackageName)){throw 'Nome da pasta de distribuição inválido.'}
$fontSource = if ($FontSourceDir -ne '') { $FontSourceDir } else { Join-Path $PSScriptRoot '../tt_interphases_pro' }
$fontSource = (Resolve-Path -LiteralPath $fontSource -ErrorAction Stop).Path
$fontStyles = @('Regular','Italic','Medium','Medium Italic','DemiBold','DemiBold Italic','Bold','Bold Italic')
foreach ($style in $fontStyles) {
    $fontName = "TT Interphases Pro Trial $style.ttf"
    if (-not (Test-Path -LiteralPath (Join-Path $fontSource $fontName))) { throw "Arquivo da fonte ausente: $fontName" }
}
$flutterPath=Join-Path $PSScriptRoot '../.tools/flutter/bin/flutter.bat'
$env:FLUTTER_PREBUILT_ENGINE_VERSION='d8a2d6c768c334d53e16277f837970ffa1e2de81'
if(-not (Test-Path -LiteralPath $flutterPath)) {throw 'Flutter SDK não encontrado em .tools/flutter.'}
& (Join-Path $PSScriptRoot 'Build-Engine.ps1') -Test
Push-Location (Join-Path $PSScriptRoot 'flutter')
try {
    & $flutterPath --suppress-analytics --no-version-check pub get --offline
    if($LASTEXITCODE -ne 0){throw 'Falha ao obter dependências Flutter.'}
    & $flutterPath --suppress-analytics --no-version-check analyze --no-pub
    if($LASTEXITCODE -ne 0){throw 'A análise Flutter encontrou erros.'}
    if(-not $SkipTests){& $flutterPath --suppress-analytics --no-version-check test --no-pub;if($LASTEXITCODE -ne 0){throw 'Testes Flutter falharam.'}}
    & $flutterPath --suppress-analytics --no-version-check build windows --release --no-pub
    if($LASTEXITCODE -ne 0){throw 'Falha na compilação Windows. Confira flutter doctor e as ferramentas C++.'}
} finally {Pop-Location}
$releasePath=Join-Path $PSScriptRoot 'flutter/build/windows/x64/runner/Release'
$packagePath=Join-Path (Join-Path $PSScriptRoot 'dist') $PackageName
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
Copy-Item -Path (Join-Path $releasePath '*') -Destination $packagePath -Recurse -Force
$enginePath=Join-Path $packagePath 'engine'
New-Item -ItemType Directory -Path $enginePath -Force | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'bin/*') -Destination $enginePath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEIA-ME.txt') -Destination $packagePath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../.tools/flutter/LICENSE') -Destination (Join-Path $packagePath 'FLUTTER-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'flutter/assets/fonts/LICENSE-Roboto.txt') -Destination $packagePath -Force
$fontTarget = Join-Path $packagePath 'fonts/tt-interphases-pro'
New-Item -ItemType Directory -Path $fontTarget -Force | Out-Null
foreach ($style in $fontStyles) {
    $fontName = "TT Interphases Pro Trial $style.ttf"
    $source = Join-Path $fontSource $fontName
    Copy-Item -LiteralPath $source -Destination (Join-Path $fontTarget $fontName) -Force
}
$programPath=Join-Path $packagePath 'mini32_stream_deck.exe'
Write-Output "Aplicativo pronto: $programPath"
if($Run){Start-Process -FilePath $programPath -WindowStyle Normal}

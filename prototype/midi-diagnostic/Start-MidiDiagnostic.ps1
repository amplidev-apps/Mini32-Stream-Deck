param([switch]$SelfTest, [switch]$ListDevices, [switch]$SmokeTest, [switch]$UiSmokeTest)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    Add-Type -AssemblyName System.Web.Extensions
    Add-Type -Path (Join-Path $PSScriptRoot 'MidiDiagnostic.cs') -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Web.Extensions
    if ($SelfTest) { [Mini32Diagnostic.Checks]::Run(); exit 0 }
    if ($ListDevices) { [Mini32Diagnostic.MidiInput]::Devices() | Format-Table Index,Name,Manufacturer,Product; exit 0 }
    if ($SmokeTest) { [Mini32Diagnostic.Checks]::Smoke($PSScriptRoot); exit 0 }
    [System.Windows.Forms.Application]::EnableVisualStyles()
    $window = New-Object Mini32Diagnostic.MonitorWindow($PSScriptRoot)
    if ($UiSmokeTest) { $window.EnableSmokeClose() }
    [System.Windows.Forms.Application]::Run($window)
} catch {
    $failure = $_ | Out-String
    $failure | Out-File -LiteralPath (Join-Path $PSScriptRoot 'startup-error.txt') -Encoding utf8
    if (-not ($SelfTest -or $ListDevices -or $SmokeTest -or $UiSmokeTest)) {
        [System.Windows.Forms.MessageBox]::Show("Falha ao iniciar. Consulte startup-error.txt na pasta do prototipo.`n`n$($_.Exception.Message)", 'Mini32 - Diagnostico') | Out-Null
    }
    Write-Error $failure -ErrorAction Continue
    exit 1
}

param([string]$RiderPath)
$ErrorActionPreference = 'Stop'
if (!$RiderPath) {
    $app = Get-ItemProperty 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/*','HKLM:/Software/Microsoft/Windows/CurrentVersion/Uninstall/*' -ErrorAction SilentlyContinue | Where-Object DisplayName -eq 'Rider' | Select-Object -First 1
    $RiderPath = $app.InstallLocation
}
if (!$RiderPath -or !(Test-Path "$RiderPath/bin/rider64.exe")) { throw 'FAILED: Rider installation not found. Supply -RiderPath.' }
if (Get-Process rider64 -ErrorAction SilentlyContinue) { throw 'FAILED: close Rider before installing/updating Android Support; unsaved work will not be terminated.' }
$process = Start-Process "$RiderPath/bin/rider64.exe" -ArgumentList 'installPlugins','com.jetbrains.rider.android' -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode) { throw "FAILED: Rider plugin installer exit $($process.ExitCode)" }
Write-Output 'Plugin installation finished; reopen Rider interactively and verify Android Support is loaded.'

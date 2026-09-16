$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
New-Item -ItemType Directory -Force "$root/.tooling" | Out-Null
$release = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$version = $release.'latest-sdk'
$installed = if (Get-Command dotnet -ErrorAction SilentlyContinue) { & dotnet --list-sdks } else { @() }
if (!($installed | Where-Object { $_ -match "^$([regex]::Escape($version)) " })) {
    $sdk = $release.releases | ForEach-Object { $_.sdks } | Where-Object version -eq $version | Select-Object -First 1
    $asset = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.exe' }
    if (!$asset) { throw 'FAILED: no official Windows x64 SDK installer found' }
    $installer = "$root/.tooling/dotnet-sdk-installer.exe"
    Invoke-WebRequest $asset.url -OutFile $installer
    if ((Get-FileHash $installer -Algorithm SHA512).Hash -ne $asset.hash) { throw 'FAILED: SDK checksum mismatch' }
    $process = Start-Process $installer -ArgumentList '/install','/quiet','/norestart' -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0,3010)) { throw "FAILED: SDK installer exit $($process.ExitCode)" }
    if ($process.ExitCode -eq 3010) { Write-Warning 'Windows restart required by SDK installer' }
}
& dotnet --info
if ($LASTEXITCODE) { throw 'FAILED: dotnet --info' }

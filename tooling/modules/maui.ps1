$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dest = Join-Path $env:LOCALAPPDATA 'Microsoft/OpenJDK/Cache'
New-Item -ItemType Directory -Force $dest | Out-Null
. "$PSScriptRoot/../enter-env.ps1" -Modules maui
if (!((& dotnet --version) -match '^10\.')) { throw 'FAILED: install .NET 10 SDK first (sdk.ps1)' }
& dotnet workload install maui-android
if ($LASTEXITCODE) { throw 'FAILED: MAUI workload installation' }
& dotnet workload update
if ($LASTEXITCODE) { throw 'FAILED: MAUI workload update' }
# Reuse a configured/shared JDK 21. A copied repository must not copy or replace it.
if ($env:JAVA_HOME) {
    $releaseFile = Join-Path $env:JAVA_HOME 'release'
    if (!(Test-Path "$env:JAVA_HOME/bin/java.exe") -or !(Test-Path $releaseFile) -or
        !((Get-Content -LiteralPath $releaseFile -Raw) -match 'JAVA_VERSION="21[.+]')) {
        throw 'JAVA_HOME must select a working JDK 21 installation. Correct the machine setting before setup.'
    }
    $java = $env:JAVA_HOME
} else {
    $downloadPage = (Invoke-WebRequest 'https://learn.microsoft.com/en-us/java/openjdk/download').Content
    $versions = [regex]::Matches($downloadPage, 'microsoft-jdk-(21\.[0-9.]+)-windows-x64\.zip') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
    $latest = $versions | Sort-Object { [version]$_ } -Descending | Select-Object -First 1
    if (!$latest) { throw 'FAILED: cannot resolve current Microsoft OpenJDK 21 release' }
    $jdkParent = Join-Path $env:LOCALAPPDATA 'Programs/Microsoft'
    New-Item -ItemType Directory -Force $jdkParent | Out-Null
    Invoke-WebRequest 'https://aka.ms/download-jdk/microsoft-jdk-21-windows-x64.zip' -OutFile "$dest/jdk.zip"
    Invoke-WebRequest 'https://aka.ms/download-jdk/microsoft-jdk-21-windows-x64.zip.sha256sum.txt' -OutFile "$dest/jdk.sha256"
    $expected = (Get-Content "$dest/jdk.sha256").Split(' ')[0]
    if ((Get-FileHash "$dest/jdk.zip" -Algorithm SHA256).Hash -ne $expected) { throw 'FAILED: JDK checksum mismatch' }
    Expand-Archive -LiteralPath "$dest/jdk.zip" -DestinationPath $jdkParent
    $java = (Get-ChildItem -LiteralPath $jdkParent -Directory | Where-Object Name -like "jdk-$latest+*" | Select-Object -First 1).FullName
    if (!$java) { throw 'FAILED: extracted JDK does not match the resolved current release' }
    $env:JAVA_HOME = $java
}
$sdk = [IO.Path]::GetFullPath($env:ANDROID_HOME)
$scratchRoot = [IO.Path]::GetFullPath("$root/.tooling/scratch")
$scratch = Join-Path $scratchRoot ("maui-install-" + [guid]::NewGuid())
try {
    & dotnet new maui -n ToolingMaui -o $scratch --no-restore
    if ($LASTEXITCODE) { throw 'FAILED: MAUI scratch creation' }
    $project = "$scratch/ToolingMaui.csproj"
    $properties = @('-f','net10.0-android','-p:TargetFrameworks=net10.0-android',"-p:AndroidSdkDirectory=$sdk","-p:JavaSdkDirectory=$java")
    & dotnet build $project -t:InstallAndroidDependencies @properties -p:AcceptAndroidSdkLicenses=True
    if ($LASTEXITCODE) { throw 'FAILED: Android dependency installation' }
    & "$PSScriptRoot/../update-android.ps1" -SdkPath $sdk
    & dotnet build $project @properties
    if ($LASTEXITCODE) { throw 'FAILED: scratch MAUI Android build' }
    & "$sdk/platform-tools/adb.exe" version
    if ($LASTEXITCODE) { throw 'FAILED: adb version' }
    & "$sdk/platform-tools/adb.exe" devices -l
    if ($LASTEXITCODE) { throw 'FAILED: adb devices' }
} finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    if (!$resolved.StartsWith($scratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe scratch cleanup path' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}

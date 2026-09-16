param([string]$SdkPath)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/enter-env.ps1" -Modules maui
if ($SdkPath) { $env:ANDROID_HOME = [IO.Path]::GetFullPath($SdkPath); $env:ANDROID_SDK_ROOT = $env:ANDROID_HOME }
$sdk = [IO.Path]::GetFullPath($env:ANDROID_HOME)
$standard = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Android/Sdk'))
if (($sdk -ne $standard) -and !$SdkPath) { throw 'Updating a custom/shared SDK requires its explicit -SdkPath.' }
if (!(Test-Path "$sdk/platform-tools/adb.exe") -or !(Test-Path "$sdk/cmdline-tools/latest/bin/sdkmanager.bat")) {
    throw 'The selected directory is not an installed Android SDK.'
}
if ((Get-Item -LiteralPath $sdk).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'SDK root must not be a junction or symlink for package updates.' }
$android = "$sdk/cmdline-tools/latest/bin/android.exe"
if (Test-Path $android) {
    & $android "--sdk=$sdk" --no-metrics sdk install cmdline-tools/latest
    if ($LASTEXITCODE) { throw 'FAILED: Android command-line tools update' }
    & $android "--sdk=$sdk" --no-metrics sdk install platform-tools
    if ($LASTEXITCODE) { throw 'FAILED: Android platform-tools update' }
} else {
    & "$sdk/cmdline-tools/latest/bin/sdkmanager.bat" "--sdk_root=$sdk" 'cmdline-tools;latest' 'platform-tools'
    if ($LASTEXITCODE) { throw 'FAILED: Android tools update' }
}
# Google's updater can retain a package backup and put the current CLI in latest-2.
& "$sdk/platform-tools/adb.exe" kill-server
$obsolete = @('platform-tools.backup')
if (Test-Path "$sdk/cmdline-tools/latest-2/bin/sdkmanager.bat") { $obsolete += 'cmdline-tools/latest' }
foreach ($relative in $obsolete) {
    $target = [IO.Path]::GetFullPath((Join-Path $sdk $relative))
    if (!$target.StartsWith($sdk + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe Android cleanup path' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
if (Test-Path "$sdk/cmdline-tools/latest-2") {
    $source = [IO.Path]::GetFullPath("$sdk/cmdline-tools/latest-2")
    $target = [IO.Path]::GetFullPath("$sdk/cmdline-tools/latest")
    if (!$source.StartsWith($sdk + [IO.Path]::DirectorySeparatorChar) -or !$target.StartsWith($sdk + [IO.Path]::DirectorySeparatorChar)) { throw 'Unsafe Android move' }
    Move-Item -LiteralPath $source -Destination $target
}
& "$sdk/cmdline-tools/latest/bin/sdkmanager.bat" --list_installed
if ($LASTEXITCODE) { throw 'FAILED: Android package verification' }
& "$sdk/platform-tools/adb.exe" version
if ($LASTEXITCODE) { throw 'FAILED: adb verification' }

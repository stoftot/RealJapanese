param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('', 'android-arm64', 'android-x64')][string]$RuntimeIdentifier = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'enter-env.ps1') -Modules dotnet,maui

# aapt2 cannot reliably consume Unicode paths. An alias keeps this checkout in
# place and gives every intermediate/output file a short ASCII path.
$driveLetter = @('R','S','T','U','V','W','X','Y','Z') |
    Where-Object { !(Test-Path "${_}:/") -and !(Get-PSDrive -Name $_ -ErrorAction SilentlyContinue) } |
    Select-Object -First 1
if (!$driveLetter) { throw 'No free drive letter is available for the Android build.' }
$drive = "${driveLetter}:"
& subst $drive $repositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Could not create the temporary Android build drive.' }
$buildExitCode = 1
try {
    $arguments = @(
        'build', "$drive/RealJapanese/RealJapanese.Mobile/RealJapanese.Mobile.csproj",
        '-c', $Configuration, '--artifacts-path', "$drive/.tooling/android-artifacts",
        '--nologo', '-v', 'minimal',
        "-p:AndroidSdkDirectory=$env:ANDROID_HOME", "-p:JavaSdkDirectory=$env:JAVA_HOME"
    )
    if ($RuntimeIdentifier) { $arguments += @('-r', $RuntimeIdentifier) }
    & dotnet @arguments
    $buildExitCode = $LASTEXITCODE
}
finally {
    & subst $drive /d
}
if ($buildExitCode -ne 0) { exit $buildExitCode }
Get-ChildItem (Join-Path $repositoryRoot '.tooling/android-artifacts/bin/RealJapanese.Mobile') -Recurse -Filter '*Signed.apk' |
    Select-Object FullName, Length

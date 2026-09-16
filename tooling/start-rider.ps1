param([Parameter(Mandatory)][string]$RiderPath, [string]$Project)
$ErrorActionPreference = 'Stop'
if ($Project) {
    if (!(Test-Path -LiteralPath $Project -PathType Leaf)) {
        $probe = Join-Path (Split-Path $PSScriptRoot -Parent) '.tooling/scratch/ToolingProbe.slnx'
        throw "Project file does not exist: '$Project'. The verification solution is '$probe'. Copy paths literally; underscores do not need backslashes."
    }
    $Project = (Resolve-Path -LiteralPath $Project).Path
}
. "$PSScriptRoot/enter-env.ps1" -Modules dotnet,maui
if (Get-Process rider64 -ErrorAction SilentlyContinue) {
    throw 'Close Rider first so the new process inherits the selected JDK and Android SDK environment.'
}
$exe = Join-Path $RiderPath 'bin/rider64.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'RiderPath must be the installed Rider directory.' }
Write-Host "Android SDK Location: $env:ANDROID_HOME"
Write-Host "Java Development Kit Location: $env:JAVA_HOME"
Write-Host 'If Rider reports missing SDK locations, copy these folders into its Android settings and Apply.'
# Run interactively: the user must be able to see startup/trust dialogs.
if ($Project) { & $exe $Project } else { & $exe }

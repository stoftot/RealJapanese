param(
    [Parameter(Mandatory, Position=0)][string]$Task,
    [string[]]$Modules,
    [string]$Server,
    [string]$RiderPath,
    [string]$Project,
    [string]$Url,
    [string[]]$CliArguments = @()
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sharedRoot = if ($env:AI_SHARED_TOOLING_ROOT) { $env:AI_SHARED_TOOLING_ROOT } else { 'E:\AI\Template shared tooling' }
if ($sharedRoot -notmatch '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+(?:[\\/]|$))') { throw 'AI_SHARED_TOOLING_ROOT must be an absolute path.' }
# This project pins the shared implementation. Change profiles only deliberately.
$entry = Join-Path $sharedRoot 'project-tools/v1/invoke.ps1'
if (!(Test-Path -LiteralPath $entry -PathType Leaf)) { throw "Shared project tooling is missing: $entry. Provision it outside the project." }
. $entry -ProjectRoot $projectRoot @PSBoundParameters

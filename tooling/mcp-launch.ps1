param([Parameter(Mandatory)][ValidateSet('headroom','netcoredbg')][string]$Server)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Server -eq 'headroom') {
    & "$root/.tooling/core/venv/Scripts/headroom.exe" mcp serve
} else {
    $version = (Get-Content "$root/.tooling/dotnet/engine-version.txt" -Raw).Trim()
    $env:NETCOREDBG_PATH = "$root/.tooling/dotnet/$version/netcoredbg/netcoredbg.exe"
    & "$root/.tooling/dotnet/venv/Scripts/netcoredbg-mcp.exe" --project $root
}
exit $LASTEXITCODE

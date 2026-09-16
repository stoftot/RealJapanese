$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dest = Join-Path $root '.tooling/dotnet'
New-Item -ItemType Directory -Force $dest | Out-Null
$release = Invoke-RestMethod 'https://api.github.com/repos/Samsung/netcoredbg/releases/latest'
$asset = $release.assets | Where-Object name -eq 'netcoredbg-win64.zip'
if (!$asset -or $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'FAILED: this installer requires Windows x64' }
if (!(Test-Path "$dest/$($release.tag_name)/netcoredbg/netcoredbg.exe")) {
    Invoke-WebRequest $asset.browser_download_url -OutFile "$dest/netcoredbg.zip"
    Expand-Archive -LiteralPath "$dest/netcoredbg.zip" -DestinationPath "$dest/$($release.tag_name)" -Force
}
$release.tag_name | Set-Content "$dest/engine-version.txt"
$package = Invoke-RestMethod 'https://pypi.org/pypi/netcoredbg-mcp/json'
if (!(Test-Path "$dest/venv/Scripts/python.exe")) {
    python -m venv "$dest/venv"
    if ($LASTEXITCODE) { throw 'FAILED: creating debugger virtual environment' }
}
& "$dest/venv/Scripts/python.exe" -m pip install "netcoredbg-mcp==$($package.info.version)"
if ($LASTEXITCODE) { throw 'FAILED: debugger MCP installation' }
& "$dest/venv/Scripts/python.exe" -m pip freeze | Set-Content "$dest/installed.txt"

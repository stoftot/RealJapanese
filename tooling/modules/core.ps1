$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dest = Join-Path $root '.tooling/core'
New-Item -ItemType Directory -Force $dest | Out-Null
$release = Invoke-RestMethod 'https://pypi.org/pypi/headroom-ai/json'
if (!(Test-Path "$dest/venv/Scripts/python.exe")) {
    python -m venv "$dest/venv"
    if ($LASTEXITCODE) { throw 'FAILED: creating Headroom virtual environment' }
}
& "$dest/venv/Scripts/python.exe" -m pip install "headroom-ai[mcp]==$($release.info.version)"
if ($LASTEXITCODE) { throw 'FAILED: Headroom installation' }
& "$dest/venv/Scripts/python.exe" -m pip freeze | Set-Content "$dest/installed.txt"

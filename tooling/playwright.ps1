$root = Split-Path $PSScriptRoot -Parent
$env:PLAYWRIGHT_BROWSERS_PATH = "$root/.tooling/web/browsers"
$localNode = Get-ChildItem "$root/.tooling/web/node" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($localNode) { $env:PATH = "$($localNode.FullName);$env:PATH" }
& "$root/.tooling/web/node_modules/.bin/playwright-cli.cmd" @args
exit $LASTEXITCODE

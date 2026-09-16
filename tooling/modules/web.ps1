$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dest = Join-Path $root '.tooling/web'
New-Item -ItemType Directory -Force $dest | Out-Null
$node = if (Get-Command node -ErrorAction SilentlyContinue) { & node --version } else { '' }
if (!$node -or [int]($node.TrimStart('v').Split('.')[0]) -notin @(22,24)) {
    $nodeRelease = Invoke-RestMethod 'https://nodejs.org/dist/index.json' | Where-Object { $_.lts -and $_.files -contains 'win-x64-zip' } | Select-Object -First 1
    if (!$nodeRelease) { throw 'FAILED: no supported official Node LTS Windows x64 release found' }
    $file = "node-$($nodeRelease.version)-win-x64.zip"
    $base = "https://nodejs.org/dist/$($nodeRelease.version)"
    Invoke-WebRequest "$base/$file" -OutFile "$dest/node.zip"
    $checksums = (Invoke-WebRequest "$base/SHASUMS256.txt").Content
    $match = [regex]::Match($checksums, "(?m)^([a-f0-9]{64})\s+$([regex]::Escape($file))\s*$")
    if (!$match.Success -or (Get-FileHash "$dest/node.zip" -Algorithm SHA256).Hash -ne $match.Groups[1].Value) { throw 'FAILED: Node checksum mismatch' }
    Expand-Archive -LiteralPath "$dest/node.zip" -DestinationPath "$dest/node" -Force
    $nodeDirectory = "$dest/node/node-$($nodeRelease.version)-win-x64"
    $env:PATH = "$nodeDirectory;$env:PATH"
}
$release = Invoke-RestMethod 'https://registry.npmjs.org/@playwright/cli/latest'
& npm.cmd install --prefix $dest --save-exact "@playwright/cli@$($release.version)"
if ($LASTEXITCODE) { throw 'FAILED: Playwright CLI installation' }
$env:PLAYWRIGHT_BROWSERS_PATH = "$dest/browsers"
& "$dest/node_modules/.bin/playwright.cmd" install chromium
if ($LASTEXITCODE) { throw 'FAILED: Chromium installation' }

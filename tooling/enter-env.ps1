param([ValidateSet('core','dotnet','web','maui')][string[]]$Modules = @('core','dotnet','web','maui'))
# Dot-source this script. Changes affect only this PowerShell process and children.
$toolingRoot = Join-Path (Split-Path $PSScriptRoot -Parent) '.tooling'
foreach ($module in $Modules) {
    switch ($module) {
        core { if (Test-Path "$toolingRoot/core/venv/Scripts") { $env:PATH = "$toolingRoot/core/venv/Scripts;$env:PATH" } }
        dotnet {
            if (Test-Path "$toolingRoot/dotnet/engine-version.txt") {
                $engine = (Get-Content "$toolingRoot/dotnet/engine-version.txt" -Raw).Trim()
                $env:NETCOREDBG_PATH = "$toolingRoot/dotnet/$engine/netcoredbg/netcoredbg.exe"
                $env:PATH = "$toolingRoot/dotnet/venv/Scripts;$env:PATH"
            }
        }
        web {
            $env:PLAYWRIGHT_BROWSERS_PATH = "$toolingRoot/web/browsers"
            $env:PATH = "$toolingRoot/web/node_modules/.bin;$env:PATH"
            $localNode = Get-ChildItem "$toolingRoot/web/node" -Directory -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($localNode) { $env:PATH = "$($localNode.FullName);$env:PATH" }
        }
        maui {
            foreach ($setting in @('JAVA_HOME','ANDROID_HOME')) {
                if (![Environment]::GetEnvironmentVariable($setting, 'Process')) {
                    $value = [Environment]::GetEnvironmentVariable($setting, 'User')
                    if (!$value) { $value = [Environment]::GetEnvironmentVariable($setting, 'Machine') }
                    if ($value) { [Environment]::SetEnvironmentVariable($setting, $value, 'Process') }
                }
            }
            if (!$env:JAVA_HOME) {
                $jdkRoots = @((Join-Path $env:ProgramFiles 'Microsoft'), (Join-Path $env:LOCALAPPDATA 'Programs/Microsoft'))
                $java = Get-ChildItem -LiteralPath $jdkRoots -Directory -Filter 'jdk-21*' -ErrorAction SilentlyContinue |
                    Where-Object { Test-Path (Join-Path $_.FullName 'bin/java.exe') } |
                    Sort-Object { if ($_.Name -match '^jdk-(\d+(?:\.\d+){1,3})') { [version]$Matches[1] } else { [version]'0.0' } } -Descending |
                    Select-Object -First 1
                if ($java) { $env:JAVA_HOME = $java.FullName }
            }
            if (!$env:ANDROID_HOME) { $env:ANDROID_HOME = Join-Path $env:LOCALAPPDATA 'Android/Sdk' }
            $env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
            $toolPaths = @()
            if ($env:JAVA_HOME -and (Test-Path "$env:JAVA_HOME/bin")) { $toolPaths += "$env:JAVA_HOME/bin" }
            foreach ($relative in @('platform-tools','cmdline-tools/latest/bin')) {
                $candidate = Join-Path $env:ANDROID_HOME $relative
                if (Test-Path -LiteralPath $candidate) { $toolPaths += $candidate }
            }
            if ($toolPaths.Count) { $env:PATH = ($toolPaths -join ';') + ';' + $env:PATH }
        }
    }
}

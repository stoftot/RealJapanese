[CmdletBinding()]
param(
    [string] $ConfigFile,
    [string] $Url = 'http://127.0.0.1:5278',
    [switch] $NoBuild,
    [switch] $NoBrowser
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Genki Studio requires PowerShell 7 or newer. Install PowerShell 7, then run Start Genki Studio.cmd again.' }
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$studioProject = Join-Path $repositoryRoot 'RealJapanese/Genki.Studio/Genki.Studio.csproj'
$studioAssembly = Join-Path $repositoryRoot 'RealJapanese/Genki.Studio/bin/Debug/net10.0/Genki.Studio.dll'
$studioWorkingDirectory = Split-Path $studioProject -Parent
$urlValue = $null
if (-not [Uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$urlValue) -or
    $urlValue.Scheme -ne 'http' -or -not $urlValue.IsLoopback -or $urlValue.AbsolutePath -ne '/' -or
    $urlValue.Query -or $urlValue.Fragment) {
    throw 'Url must be a loopback HTTP URL such as http://127.0.0.1:5278.'
}
$studioUrl = $Url.TrimEnd('/')
$dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
if (-not $dotnetCommand) { $dotnetCommand = Get-Command 'dotnet' -ErrorAction SilentlyContinue }
if (-not $dotnetCommand) { throw 'The .NET SDK was not found. Install the .NET 10 SDK, then run Start Genki Studio.cmd again.' }
$dotnet = $dotnetCommand.Source

$installedSdks = @(& $dotnet --list-sdks)
if (-not ($installedSdks | Where-Object { $_ -match '^10\.' })) {
    throw 'Genki Studio targets .NET 10, but no .NET 10 SDK is installed.'
}

function Find-AiLibraryRoot {
    $configuredRoot = $env:AI_LIBRARY_ROOT
    if (-not [string]::IsNullOrWhiteSpace($configuredRoot)) {
        $fullRoot = [IO.Path]::GetFullPath($configuredRoot)
        if (-not (Test-Path (Join-Path $fullRoot 'src/AiLibrary.Core/AiLibrary.Core.csproj')) -or
            -not (Test-Path (Join-Path $fullRoot 'src/AiLibrary.LlamaServer/AiLibrary.LlamaServer.csproj'))) {
            throw "AI_LIBRARY_ROOT does not contain the AiLibrary Core and LlamaServer projects: $fullRoot"
        }
        return $fullRoot
    }

    # The extraction project is the repository's existing source of truth for the optional checkout.
    $extractProject = Join-Path $repositoryRoot 'RealJapanese/Extract kanji/Extract kanji.csproj'
    if (-not (Test-Path $extractProject)) { return $null }
    [xml]$project = Get-Content -LiteralPath $extractProject -Raw
    foreach ($reference in $project.SelectNodes("//*[local-name()='ProjectReference']")) {
        $include = [string]$reference.Include
        if ($include -notmatch 'AiLibrary\.Core\.csproj$') { continue }
        try { $coreProject = [IO.Path]::GetFullPath($include, (Split-Path $extractProject -Parent)) }
        catch { continue }
        if (-not (Test-Path $coreProject)) { continue }
        $sourceDirectory = Split-Path $coreProject -Parent
        $libraryRoot = Split-Path (Split-Path $sourceDirectory -Parent) -Parent
        if ((Test-Path (Join-Path $libraryRoot 'src/AiLibrary.Core/AiLibrary.Core.csproj')) -and
            (Test-Path (Join-Path $libraryRoot 'src/AiLibrary.LlamaServer/AiLibrary.LlamaServer.csproj'))) {
            return $libraryRoot
        }
    }
    return $null
}

$aiLibraryRoot = Find-AiLibraryRoot
if ($aiLibraryRoot) {
    Write-Host "Using local AiLibrary checkout: $aiLibraryRoot"
} else {
    Write-Host 'AiLibrary was not found. Studio will still support configuration, coverage scans and review; model tagging and generation will be unavailable.' -ForegroundColor Yellow
}

if ([string]::IsNullOrWhiteSpace($ConfigFile)) { $ConfigFile = $env:GENKI_STUDIO_CONFIG }
if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
    $ConfigFile = [IO.Path]::GetFullPath($ConfigFile, $repositoryRoot)
}

if (-not $NoBuild) {
    $buildArguments = @('build', $studioProject, '-m:1', '--nologo')
    if ($aiLibraryRoot) { $buildArguments += "-p:AiLibraryRoot=$aiLibraryRoot" }
    Write-Host 'Building Genki Studio for .NET 10...'
    & $dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Genki Studio build failed with exit code $LASTEXITCODE." }
}
if (-not (Test-Path $studioAssembly)) {
    throw "Genki Studio was not built. Expected output at $studioAssembly"
}

$toolingDirectory = Join-Path $repositoryRoot '.tooling/genki-studio'
New-Item -ItemType Directory -Force -Path $toolingDirectory | Out-Null
$stdoutLog = Join-Path $toolingDirectory 'host.stdout.log'
$stderrLog = Join-Path $toolingDirectory 'host.stderr.log'
Remove-Item -LiteralPath $stdoutLog, $stderrLog -Force -ErrorAction SilentlyContinue

$startInfo = @{
    FilePath = $dotnet
    # Start-Process joins ArgumentList into one command line; quote this path because the repo may contain spaces.
    ArgumentList = ('"{0}"' -f $studioAssembly)
    WorkingDirectory = $studioWorkingDirectory
    WindowStyle = 'Hidden'
    PassThru = $true
    RedirectStandardOutput = $stdoutLog
    RedirectStandardError = $stderrLog
}
$hostProcess = $null
$hostExitCode = 0

try {
    $env:ASPNETCORE_URLS = $studioUrl
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    if ($aiLibraryRoot) { $env:AI_LIBRARY_ROOT = $aiLibraryRoot }
    if ($ConfigFile) { $env:GenkiStudio__ConfigFile = $ConfigFile }
    else { Remove-Item Env:GenkiStudio__ConfigFile -ErrorAction SilentlyContinue }
    ${env:Logging__LogLevel__Microsoft.Hosting.Lifetime} = 'Information'
    $hostProcess = Start-Process @startInfo

    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while (-not $ready -and [DateTime]::UtcNow -lt $deadline) {
        if ($hostProcess.HasExited) {
            $hostProcess.Refresh()
            $details = @()
            if (Test-Path $stderrLog) { $details += Get-Content -LiteralPath $stderrLog -Tail 30 }
            if (Test-Path $stdoutLog) { $details += Get-Content -LiteralPath $stdoutLog -Tail 30 }
            throw "Genki Studio exited during startup (code $($hostProcess.ExitCode)).`n$($details -join [Environment]::NewLine)"
        }
        # Wait for this child process's Kestrel listening log before probing the URL,
        # so an unrelated service already bound to the port cannot look like success.
        $listeningLine = "Now listening on: $studioUrl"
        $ownListenerReady = $false
        foreach ($log in @($stdoutLog, $stderrLog)) {
            if (Test-Path $log) {
                try { if (Select-String -LiteralPath $log -SimpleMatch $listeningLine -Quiet) { $ownListenerReady = $true } }
                catch { }
            }
        }
        if ($ownListenerReady) {
            try {
                $response = Invoke-WebRequest -Uri $studioUrl -TimeoutSec 2
                $ready = $response.StatusCode -eq 200 -and $response.Content -match 'GENKI STUDIO'
            } catch { Start-Sleep -Milliseconds 500 }
        } else { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw "Genki Studio did not become ready at $studioUrl within 60 seconds. Check $stderrLog and $stdoutLog." }

    Write-Host "Genki Studio is ready at $studioUrl"
    Write-Host "Configuration: $(if ($ConfigFile) { $ConfigFile } else { Join-Path $repositoryRoot '.tooling/genki.local.json' })"
    Write-Host "Host logs: $toolingDirectory"
    if (-not $NoBrowser) { Start-Process $studioUrl | Out-Null }
    Write-Host 'Keep this window open while using Studio. Press Ctrl+C to stop its local server.'
    while (-not $hostProcess.HasExited) { Start-Sleep -Seconds 1 }
    if ($hostProcess.HasExited) {
        $hostProcess.Refresh()
        $details = @()
        if (Test-Path $stderrLog) { $details += Get-Content -LiteralPath $stderrLog -Tail 30 }
        if (Test-Path $stdoutLog) { $details += Get-Content -LiteralPath $stdoutLog -Tail 30 }
        Write-Host "Genki Studio stopped unexpectedly (code $($hostProcess.ExitCode)).`n$($details -join [Environment]::NewLine)" -ForegroundColor Red
        $hostExitCode = $hostProcess.ExitCode
    }
}
finally {
    if ($hostProcess -and -not $hostProcess.HasExited) {
        # Stop only the process tree created by this launcher; do not touch an unrelated server on the port.
        $hostProcess.Kill($true)
        $hostProcess.WaitForExit(10000) | Out-Null
    }
    if ($hostProcess) { $hostProcess.Dispose() }
}
if ($hostExitCode -ne 0) { exit $hostExitCode }

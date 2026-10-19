param(
    [Parameter(Mandatory = $true)][string] $LibraryBin
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$destination = [IO.Path]::GetFullPath((Join-Path $root '.tooling/tts-ai-library'))
$source = (Resolve-Path -LiteralPath $LibraryBin).Path
if ($source.TrimEnd('\') -eq $destination.TrimEnd('\')) { throw 'Source and destination must differ.' }
for ($directory = [IO.DirectoryInfo]::new($destination); $null -ne $directory; $directory = $directory.Parent) {
    if ($directory.Exists -and ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Destination must not traverse a junction or symlink: $($directory.FullName)"
    }
}
$names = @('AiLibrary.Core.dll', 'AiLibrary.Speech.dll', 'AiLibrary.StyleBertVits2.dll', 'Microsoft.Extensions.Logging.Abstractions.dll', 'Microsoft.Extensions.DependencyInjection.Abstractions.dll')
foreach ($name in $names) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Missing prebuilt dependency: $name" }
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $destination $name) -Force }
Write-Output "Copied existing binaries to $destination. No external project was built or modified."

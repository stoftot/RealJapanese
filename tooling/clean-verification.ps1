$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
foreach ($relative in @('.tooling/scratch','.playwright-cli','tooling/__pycache__')) {
    $target = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (!$target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup path' }
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
}
Write-Output 'Verification fixtures removed. Installed tools and receipts preserved.'

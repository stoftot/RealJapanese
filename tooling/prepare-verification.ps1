$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $root '.tooling/scratch'
if (Test-Path $scratch) { throw 'Scratch fixtures already exist. Finish/clean the previous verification first.' }
New-Item -ItemType Directory -Force $scratch | Out-Null
& dotnet new console -n DebugProbe -o "$scratch/debug" --framework net10.0 --no-restore
if ($LASTEXITCODE) { throw 'FAILED: console fixture creation' }
@'
using System;
using System.Threading;

internal static class Program
{
    private static void Main()
    {
        int value = 40;
        value += 2;
        Console.WriteLine(value);
        Thread.Sleep(1000);
    }
}
'@ | Set-Content "$scratch/debug/Program.cs"
& dotnet restore "$scratch/debug/DebugProbe.csproj"
if ($LASTEXITCODE) { throw 'FAILED: restore' }
& dotnet build "$scratch/debug/DebugProbe.csproj" --no-restore
if ($LASTEXITCODE) { throw 'FAILED: build' }
& dotnet format "$scratch/debug/DebugProbe.csproj" --verify-no-changes --no-restore
if ($LASTEXITCODE) { throw 'FAILED: dotnet format check' }
& dotnet new xunit -n ToolingTests -o "$scratch/tests" --framework net10.0
if ($LASTEXITCODE) { throw 'FAILED: test project creation/restore' }
& dotnet test "$scratch/tests/ToolingTests.csproj"
if ($LASTEXITCODE) { throw 'FAILED: test workflow' }
Write-Output 'Scratch .NET workflow passed. Run Codex MCP acceptance before cleanup.'

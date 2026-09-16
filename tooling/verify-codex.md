Verify the external tools in this repository using the actual configured MCP tools.
Do not install anything, change configuration, use a manual DAP client, or delegate.
Report failures honestly and stop a failing path after one reasonable retry.

1. Enumerate the tools visible from headroom and netcoredbg (using discovery if
   necessary). Confirm Headroom has exactly headroom_compress, headroom_retrieve,
   headroom_stats, and NetCoreDbg only the 18 enabled tools in .codex/config.toml.
   Specifically confirm write_memory, ui_click, runtime_smoke_start and
   apply_code_change are absent from the exposed debugger tool surface.
2. Call headroom_compress with synthetic repetitive log content, retrieve its
   returned hash with headroom_retrieve, and check the original is preserved.
   Call headroom_stats. No proxy should be started.
3. The scratch .NET 10 Debug program is already built at
   .tooling/scratch/debug/bin/Debug/net10.0/DebugProbe.dll.
   Its Program.cs has value=40 at line 8, value+=2 at line 9, Console.WriteLine
   at line 10, and Thread.Sleep at line 11.
   Use the configured netcoredbg tools only for the following:
   - Launch with stop_at_entry=true and pre_build=false.
   - Set a source breakpoint at Program.cs line 9.
   - Continue until the breakpoint is hit.
   - Get threads, call stack, scopes, and local variables. Confirm value is 40.
   - Evaluate value + 2 and confirm 42.
   - Step over line 9; inspect value and confirm it became 42.
   - Remove the breakpoint, resume execution, and terminate or stop cleanly.
   Use absolute paths resolved from the repository root.
4. If Rider is configured and connected, call a small project-information tool.
5. Return a concise evidence report with actual observed values, tool names,
   each pass/failure, and confirmation of debugger shutdown. Do not delete the
   fixtures; the supervising task will clean them up.

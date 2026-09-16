"""Discover Rider's actual tool surface before generating a narrow module."""
import asyncio
import json
import sys
from pathlib import Path
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client

ROOT = Path(__file__).resolve().parent.parent
# Candidates from JetBrains documentation; only actually advertised names survive.
CANDIDATES = {
    'get_project_modules', 'get_project_dependencies', 'get_project_info',
    'get_symbol_info', 'search_symbol', 'find_usages', 'analyze_calls',
    'get_file_problems', 'get_run_configurations', 'execute_run_configuration',
    'build_project', 'get_solution_projects', 'build_solution_start', 'build_solution_state',
}

async def main():
    url = sys.argv[1] if len(sys.argv) > 1 else 'http://127.0.0.1:64342/stream'
    if not url.startswith(('http://127.0.0.1:', 'http://localhost:')):
        raise ValueError('Use the loopback HTTP Stream URL copied from Rider settings.')
    async with streamablehttp_client(url) as streams:
        async with ClientSession(streams[0], streams[1]) as session:
            await session.initialize()
            result = await session.list_tools()
            tools = [t.model_dump(mode='json') for t in result.tools]
    selected = sorted({t['name'] for t in tools} & CANDIDATES)
    if not selected:
        raise RuntimeError('FAILED: no selected IDE capabilities advertised; inspect Rider Exposed Tools settings.')
    (ROOT/'.tooling/rider-tools.json').write_text(json.dumps(tools, indent=2), encoding='utf-8')
    lines = ['# Generated from actual Rider tools/list; rediscover after IDE upgrades.',
             '[mcp_servers.rider]', f'url = {json.dumps(url)}', 'required = false',
             'startup_timeout_sec = 10', 'tool_timeout_sec = 120',
             f'enabled_tools = {json.dumps(selected)}']
    for name in selected:
        lines += ['', f'[mcp_servers.rider.tools.{name}]', 'output_token_limit = 4000']
    (ROOT/'tooling/modules/rider.toml').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print('Discovered allowlist:', ', '.join(selected))
    print('Run: ./tooling/configure.ps1 -Modules core,dotnet,rider')

asyncio.run(main())

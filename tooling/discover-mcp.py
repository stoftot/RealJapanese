"""Protocol discovery, not a substitute for the Codex acceptance test."""
import asyncio
import json
import os
import sys
from pathlib import Path
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from mcp.client.streamable_http import streamablehttp_client

ROOT = Path(__file__).resolve().parent.parent

async def inspect(streams, name):
    async with ClientSession(streams[0], streams[1]) as session:
        await session.initialize()
        result = await session.list_tools()
        data = [t.model_dump(mode='json') for t in result.tools]
        (ROOT / '.tooling' / f'{name}-tools.json').write_text(json.dumps(data, indent=2), encoding='utf-8')
        print(name, json.dumps([t['name'] for t in data]))

async def main():
    name = sys.argv[1]
    if name == 'rider':
        async with streamablehttp_client('http://127.0.0.1:64342/stream') as streams:
            await inspect(streams, name)
    else:
        params = StdioServerParameters(command='powershell.exe', args=['-NoProfile','-File',str(ROOT/'tooling/mcp-launch.ps1'),'-Server',name], cwd=str(ROOT), env=dict(os.environ))
        async with stdio_client(params) as streams:
            await inspect(streams, name)

asyncio.run(main())

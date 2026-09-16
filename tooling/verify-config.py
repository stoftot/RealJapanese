"""Validate portable MCP policy and compare with captured real discovery."""
import json
import tomllib
from pathlib import Path

root = Path(__file__).resolve().parent.parent
config = tomllib.loads((root/'.codex/config.toml').read_text(encoding='utf-8'))
rejected = {'write_memory', 'read_memory', 'set_variable', 'ui_click',
            'runtime_smoke_start', 'apply_code_change'}
servers = config['mcp_servers']
for name, server in servers.items():
    assert 'enabled_tools' in server, f'{name}: missing explicit allowlist'
    allowed = set(server['enabled_tools'])
    assert not (allowed & rejected), f'{name}: unwanted capability'
    assert server['startup_timeout_sec'] > 0 and server['tool_timeout_sec'] > 0
    discovery = root/'.tooling'/f'{name}-tools.json'
    if discovery.exists():
        actual = {t['name'] for t in json.loads(discovery.read_text(encoding='utf-8'))}
        assert allowed <= actual, f'{name}: unknown tool names {allowed-actual}'
    elif allowed:
        raise AssertionError(f'{name}: missing actual discovery evidence')
    print(name, len(allowed), 'allowlisted tools')
if 'headroom' in servers:
    assert set(servers['headroom']['enabled_tools']) == {'headroom_compress','headroom_retrieve','headroom_stats'}
if 'rider' in servers:
    assert servers['rider']['required'] is False
    if not servers['rider']['enabled_tools']:
        raise SystemExit('FAILED: Rider connection/discovery is still pending; core/debugger policy is valid.')
print('PASS: policy and discovered names. Run Codex acceptance for functional proof.')

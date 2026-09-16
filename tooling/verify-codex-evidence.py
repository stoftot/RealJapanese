"""Check the real Codex JSONL transcript, not a manually driven debugger."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
calls = []
for line in (root/'.tooling/codex-verification.jsonl').read_text(encoding='utf-8').splitlines():
    event = json.loads(line)
    item = event.get('item', {})
    if event['type'] == 'item.completed' and item.get('type') == 'mcp_tool_call':
        if item['server'] in {'headroom', 'netcoredbg'}:
            assert not item.get('error'), item
            blocks = item['result']['content']
            item['decoded'] = json.loads(next(c['text'] for c in blocks if c['type'] == 'text'))
            calls.append(item)

def matching(tool):
    return [c for c in calls if c['tool'] == tool]

compressed = matching('headroom_compress')[-1]
retrieved = matching('headroom_retrieve')[-1]
assert retrieved['decoded']['original_content'] == compressed['arguments']['content']
assert retrieved['decoded']['source'] == 'local'
assert matching('headroom_stats')
required = {'start_debug','add_breakpoint','continue_execution','get_threads',
            'get_call_stack','get_scopes','get_variables','evaluate_expression',
            'step_over','remove_breakpoint','stop_debug','get_debug_state'}
assert required <= {c['tool'] for c in calls}
assert any(c['decoded'].get('reason') == 'breakpoint' for c in matching('continue_execution'))
values = [v['value'] for c in matching('get_variables') for v in c['decoded']['data'] if v['name'] == 'value']
assert values == ['40','42'], values
assert matching('evaluate_expression')[-1]['decoded']['data']['result'] == '42'
assert matching('step_over')[-1]['decoded']['location']['line'] == 10
assert matching('continue_execution')[-1]['decoded']['exit_code'] == 0
last = matching('get_debug_state')[-1]['decoded']
assert last['state'] == 'idle' and last['data']['debuggeeAlive'] is False
print('PASS: exact local compression round-trip; breakpoint; variable 40 -> 42; expression 42; stepping; clean exit.')

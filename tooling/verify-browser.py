"""Exercise the installed CLI against a disposable loopback page."""
import functools
import http.server
import json
import re
import subprocess
import tempfile
import threading
import uuid
from pathlib import Path

root = Path(__file__).resolve().parent.parent
scratch = root/'.tooling/scratch'
scratch.mkdir(parents=True, exist_ok=True)
session = 'verify-' + uuid.uuid4().hex[:12]

def cli(*args):
    print('CLI:', args[0], flush=True)
    # A detached CLI daemon can inherit pipe handles on Windows. Use a file so
    # process completion never waits for a long-lived daemon to close stdout.
    output = Path(folder)/'cli-output.txt'
    with output.open('w',encoding='utf-8') as stream:
        p = subprocess.run(['powershell.exe','-NoProfile','-File',str(root/'tooling/playwright.ps1'),f'-s={session}',*args],
                           cwd=folder, stdout=stream, stderr=subprocess.STDOUT, timeout=60)
    text = output.read_text(encoding='utf-8',errors='replace')
    if p.returncode or '### Error' in text:
        raise RuntimeError(text)
    return text

with tempfile.TemporaryDirectory(prefix='browser-',dir=scratch) as folder:
    page = Path(folder)/'index.html'
    page.write_text('<!doctype html><html lang="en"><meta charset="utf-8"><title>Tooling verification</title>'
                    '<link rel="icon" href="data:,"><label>Name <input id="name"></label>'
                    '<button onclick="document.querySelector(\'#result\').textContent=\'Hello \'+document.querySelector(\'#name\').value">Greet</button>'
                    '<p id="result" role="status"></p></html>',encoding='utf-8')
    config = Path(folder)/'cli.json'
    config.write_text(json.dumps({'browser':{'browserName':'chromium','launchOptions':{'channel':'chromium','headless':True}}}))
    server = http.server.ThreadingHTTPServer(('127.0.0.1',0),functools.partial(http.server.SimpleHTTPRequestHandler,directory=folder))
    worker = threading.Thread(target=server.serve_forever,daemon=True)
    worker.start()
    try:
        cli('open',f'http://127.0.0.1:{server.server_port}','--config',str(config))
        snapshot = cli('snapshot')
        textbox = re.search(r'textbox "Name" \[ref=(e\d+)\]',snapshot).group(1)
        button = re.search(r'button "Greet" \[ref=(e\d+)\]',snapshot).group(1)
        cli('fill',textbox,'Codex')
        cli('click',button)
        assert 'Hello Codex' in cli('snapshot')
        screenshot = Path(folder)/'verification.png'
        cli('screenshot','--filename',str(screenshot))
        assert screenshot.read_bytes().startswith(b'\x89PNG\r\n\x1a\n')
        cli('console')
        cli('requests')
    finally:
        try:
            cli('close')
            cli('delete-data')
        finally:
            server.shutdown()
            server.server_close()
            worker.join(timeout=5)
print('PASS: Chromium launch/navigation/snapshot/fill/click/screenshot/console/requests/close; fixtures removed.')

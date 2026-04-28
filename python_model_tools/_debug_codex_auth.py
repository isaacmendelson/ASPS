import paramiko, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

def run(cmd, timeout=60):
    print(f"\n$ {cmd[:130]}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    if o.strip(): print(o.strip()[:5000])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e

# Check auth status
run("openclaw capability model auth status 2>&1 | head -30")

# Look for token extraction logic in codex extension
run("grep -r 'accountId' /usr/lib/node_modules/openclaw/dist/extensions/codex/ 2>&1 | grep -v node_modules | head -10")
run("grep -r 'extract.*account\\|chatgpt_account_id' /usr/lib/node_modules/openclaw/dist/extensions/codex/ 2>&1 | head -10")

# Check shared-client (likely where token is read)
run("grep -n 'accountId\\|chatgpt_account\\|token' /usr/lib/node_modules/openclaw/dist/extensions/codex/shared-client-COi62iWf.js 2>&1 | head -30")

# Check if there's a way to specify accountId manually
run("openclaw plugins inspect codex 2>&1 | head -100 | tail -60")

# Look at config schema for codex plugin appServer auth options
run("openclaw config schema 2>&1 | python3 -c 'import json,sys;s=json.load(sys.stdin); cfg=s[\"properties\"][\"plugins\"][\"properties\"][\"entries\"][\"properties\"][\"codex\"][\"properties\"][\"config\"][\"properties\"][\"appServer\"][\"properties\"]; print(json.dumps(cfg, indent=2)[:3500])'")

# Check codex app-server auth setup
run("codex app-server --help 2>&1 | head -30")

# Try running codex app-server in stdio mode and see what env it expects
print("\n=== Codex app-server env vars detection ===")
run("strings /usr/lib/node_modules/@openai/codex/node_modules/@openai/codex-linux-x64/vendor/x86_64-unknown-linux-musl/codex/codex 2>&1 | grep -iE 'CODEX_|OPENAI_|chatgpt_account' | head -20")

# Check the token's parsed contents and try extracting accountId
print("\n=== Decode JWT to see structure ===")
run("python3 -c '\nimport json, base64\nwith open(\"/root/.codex/auth.json\") as f:\n    d = json.load(f)\nfor name in [\"id_token\", \"access_token\"]:\n    tok = d[\"tokens\"].get(name, \"\")\n    if not tok: continue\n    parts = tok.split(\".\")\n    if len(parts) < 2: continue\n    payload_b64 = parts[1] + \"=\" * (4 - len(parts[1]) % 4)\n    payload = json.loads(base64.urlsafe_b64decode(payload_b64))\n    print(f\"--- {name} ---\")\n    if \"https://api.openai.com/auth\" in payload:\n        print(json.dumps(payload[\"https://api.openai.com/auth\"], indent=2)[:1000])\n    print(\"chatgpt_account_id:\", payload.get(\"https://api.openai.com/auth\", {}).get(\"chatgpt_account_id\"))\n' 2>&1")

client.close()

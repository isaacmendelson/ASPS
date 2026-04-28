import paramiko, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

def run(cmd, timeout=60):
    print(f"\n$ {cmd[:120]}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    if o.strip(): print(o.strip()[:5000])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e

# Inspect codex
print("===== CODEX (ChatGPT subscription) =====")
run("openclaw plugins inspect codex 2>&1 | head -80")
run("cat /usr/lib/node_modules/openclaw/dist/extensions/codex/openclaw.plugin.json 2>&1 | head -80")

# Check codex extension's auth/config
run("ls /usr/lib/node_modules/openclaw/dist/extensions/codex/")

# Inspect github-copilot
print("\n===== GITHUB COPILOT (Copilot subscription) =====")
run("openclaw plugins inspect github-copilot 2>&1 | head -80")

# Inspect copilot-proxy
print("\n===== COPILOT PROXY =====")
run("openclaw plugins inspect copilot-proxy 2>&1 | head -40")

# Check opencode
print("\n===== OPENCODE =====")
run("openclaw plugins inspect opencode 2>&1 | head -40")

# Search for "subscription" or "oauth" or "login" in codex extension
print("\n===== Codex auth method check =====")
run("grep -r -l 'oauth\\|chatgpt\\|subscription\\|device_code' /usr/lib/node_modules/openclaw/dist/extensions/codex/ 2>&1 | head -5")
run("grep -i 'login\\|auth\\|chatgpt' /usr/lib/node_modules/openclaw/dist/extensions/codex/openclaw.plugin.json 2>&1")

# What models does codex provide
print("\n===== Codex models =====")
run("cat /usr/lib/node_modules/openclaw/dist/extensions/codex/models-DSX4Bm3f.js 2>&1 | head -50")

# Check schema for codex-specific config
sftp = client.open_sftp()
script = '''
import json, sys
s = json.load(sys.stdin)
# Search for "codex" in providers or auth sections
import re
serialized = json.dumps(s)
for key in s.get("properties", {}):
    sub = s["properties"][key]
    text = json.dumps(sub)
    if "codex" in text.lower() or "chatgpt" in text.lower():
        print(f"=== Mentions in {key} ===")
        # Find specific paths
        def search(obj, path=""):
            if isinstance(obj, dict):
                for k, v in obj.items():
                    if "codex" in str(k).lower() or "chatgpt" in str(k).lower():
                        print(f"  {path}.{k}: {json.dumps(v)[:200]}")
                    search(v, f"{path}.{k}")
            elif isinstance(obj, list):
                for i, item in enumerate(obj):
                    search(item, f"{path}[{i}]")
        search(sub, key)
'''
with sftp.file("/tmp/find_codex.py", "w") as f:
    f.write(script)
sftp.close()
run("openclaw config schema 2>&1 | python3 /tmp/find_codex.py 2>&1 | head -40")

client.close()

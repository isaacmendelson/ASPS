import paramiko, os, sys, io, time, json
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
    if o.strip(): print(o.strip()[:3500])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Step 1: Verify login completed
print("===== STEP 1: Verify codex login =====")
run("ls -la /tmp/codex-login.done 2>&1")
run("cat /tmp/codex-login.log 2>&1")
run("codex login status 2>&1")
run("ls -la /root/.codex/ 2>&1")
# Show auth.json (sensitive - mask the actual token)
run("python3 -c 'import json; d=json.load(open(\"/root/.codex/auth.json\")); print({k: (v[:20]+\"...\" if isinstance(v,str) and len(v)>20 else v) for k,v in d.items()})' 2>&1")

# Step 2: Test that codex can call the API using subscription
print("\n===== STEP 2: Test codex works (small prompt) =====")
run("codex exec --help 2>&1 | head -25")

# Step 3: List models codex has access to
print("\n===== STEP 3: List codex-managed models =====")
# The codex app-server needs to be running for OpenClaw to query models
# Let's see what's available

# Step 4: Check OpenClaw codex plugin schema
print("\n===== STEP 4: OpenClaw config schema for codex section =====")
sftp = client.open_sftp()
script = '''
import json, sys
s = json.load(sys.stdin)
# Look for plugins.entries.codex
codex = s["properties"]["plugins"]["properties"]["entries"]["properties"].get("codex", {})
print("=== plugins.entries.codex schema ===")
print(json.dumps(codex, indent=2)[:5000])

# Also look at agents.defaults.embeddedHarness and agents.defaults.model
print("\\n=== agents.defaults.model schema ===")
print(json.dumps(s["properties"]["agents"]["properties"]["defaults"]["properties"].get("model", {}), indent=2)[:1000])
'''
with sftp.file("/tmp/check_codex_schema.py", "w") as f:
    f.write(script)
sftp.close()
run("openclaw config schema 2>&1 | python3 /tmp/check_codex_schema.py")

client.close()

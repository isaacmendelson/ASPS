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
    if o.strip(): print(o.strip()[:3500])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e

# List all plugins
run("openclaw plugins list 2>&1 | head -80 || true")
run("openclaw plugins --help 2>&1 | head -30 || true")

# Look for harnesses/agents
run("openclaw agents --help 2>&1 | head -40")

# Check capabilities
run("openclaw capability --help 2>&1 | head -40")

# Check auth
run("openclaw channels capabilities 2>&1 | head -40")

# Look for codex and claude-code plugins
run("ls /usr/lib/node_modules/openclaw/dist/plugins 2>&1 | head -60 || ls /usr/lib/node_modules/openclaw/plugins 2>&1 | head -60 || true")
run("find /usr/lib/node_modules/openclaw -name 'package.json' -maxdepth 4 2>&1 | head -20")

# Check what plugins exist
run("openclaw doctor 2>&1 | grep -iE 'codex|claude|loaded|disabled' | head -30")

# Look for embeddedHarness configurations
sftp = client.open_sftp()
script = '''
import json, sys
s = json.load(sys.stdin)
# Check agents.defaults.embeddedHarness
em = s["properties"]["agents"]["properties"]["defaults"]["properties"].get("embeddedHarness", {})
print("=== embeddedHarness schema ===")
print(json.dumps(em, indent=2)[:2000])

# Check if there are plugins with codex/claude
plugins = s["properties"].get("plugins", {})
print("\\n=== plugins schema ===")
print(json.dumps(plugins, indent=2)[:2000])
'''
with sftp.file("/tmp/check_harness.py", "w") as f:
    f.write(script)
sftp.close()
run("openclaw config schema 2>&1 | python3 /tmp/check_harness.py")

client.close()

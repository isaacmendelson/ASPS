import paramiko, os, sys, io, time, json
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

TG_TOKEN = os.environ["TG_TOKEN"]
OPENAI_KEY = os.environ["OPENAI_KEY"]

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)


def run(cmd, timeout=60, hide=None):
    display = cmd if not hide else cmd.replace(hide, "***")
    print(f"\n$ {display}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    if hide:
        o = o.replace(hide, "***")
        e = e.replace(hide, "***")
    if o.strip(): print(o.strip()[:2500])
    if e.strip(): print(f"[stderr] {e.strip()[:1000]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Check what providers/secrets section looks like
sftp = client.open_sftp()
script = '''
import json, sys
s = json.load(sys.stdin)
top = list(s.get("properties", {}).keys())
print("Top-level config keys:")
for k in top:
    desc = s["properties"][k].get("description", "")[:120]
    print(f"  {k}: {desc}")
'''
with sftp.file("/tmp/list_top.py", "w") as f:
    f.write(script)
sftp.close()

run("openclaw config schema 2>&1 | python3 /tmp/list_top.py")

# Stop the failing service
run("systemctl stop openclaw 2>&1")

# Build a correct config with groupPolicy disabled and bot token directly
config = {
    "agents": {
        "defaults": {
            "model": "openai/gpt-4o-mini"
        }
    },
    "channels": {
        "telegram": {
            "enabled": True,
            "botToken": TG_TOKEN,
            "dmPolicy": "pairing",
            "groupPolicy": "disabled"
        }
    }
}

sftp = client.open_sftp()
with sftp.file("/root/.openclaw/openclaw.json", "w") as f:
    f.write(json.dumps(config, indent=2))
sftp.chmod("/root/.openclaw/openclaw.json", 0o600)
sftp.close()
print("\nWrote corrected /root/.openclaw/openclaw.json (mode 600)")

# Validate
run("openclaw config validate 2>&1", hide=TG_TOKEN)
run("openclaw config get agents.defaults.model 2>&1")
run("openclaw config get channels.telegram.dmPolicy 2>&1")
run("openclaw config get channels.telegram.enabled 2>&1")

# Run doctor to see if any plugins or providers need to be configured
run("openclaw doctor 2>&1 | head -60", hide=TG_TOKEN)

# Try the gateway briefly to see if it starts
print("\n===== Try gateway in foreground briefly =====")
# We expect this to either start cleanly or show config errors
run("timeout 8 bash -c 'set -a; . /etc/openclaw/env; set +a; openclaw gateway --port 18789 2>&1' || true", timeout=20, hide=TG_TOKEN)

# Check for openai providers needed
sftp = client.open_sftp()
script2 = '''
import json, sys
s = json.load(sys.stdin)
# Check for providers section
for key in ["providers", "secrets", "models", "credentials", "auth"]:
    if key in s.get("properties", {}):
        print(f"FOUND: {key}")
        print(json.dumps(s["properties"][key], indent=2)[:2000])
        print()
'''
with sftp.file("/tmp/list_providers.py", "w") as f:
    f.write(script2)
sftp.close()
run("openclaw config schema 2>&1 | python3 /tmp/list_providers.py")

client.close()

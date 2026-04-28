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

# Find codex/claude plugins
run("ls /usr/lib/node_modules/openclaw/dist/extensions/ | grep -iE 'codex|claude|cli|harness|chatgpt|subscription' 2>&1")
run("ls /usr/lib/node_modules/openclaw/dist/extensions/ | head -120")

# Search for any harness-related plugin
run("openclaw plugins list 2>&1 | grep -iE 'codex|claude|harness|cli' || true")

# Check the codex package if it exists
run("cat /usr/lib/node_modules/openclaw/dist/extensions/codex/package.json 2>&1 | head -30 || echo 'no codex extension'")
run("ls /usr/lib/node_modules/openclaw/dist/extensions/codex/ 2>&1 | head -10 || true")
run("ls /usr/lib/node_modules/openclaw/dist/extensions/claude-code/ 2>&1 | head -10 || ls /usr/lib/node_modules/openclaw/dist/extensions/ | grep -i claude")

# Check anthropic provider
run("cat /usr/lib/node_modules/openclaw/dist/extensions/anthropic/package.json 2>&1 | head -40")

# Look at the openai provider details
run("cat /usr/lib/node_modules/openclaw/dist/extensions/openai/package.json 2>&1 | head -40")

# Look at openai-related plugins
run("ls /usr/lib/node_modules/openclaw/dist/extensions/ | grep -i open")

# Search for things that might be CLI-based
run("openclaw plugins list 2>&1 | tail -100 | head -100")

client.close()

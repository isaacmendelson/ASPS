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

# Find the Failed to extract message
run("grep -rn 'Failed to extract' /usr/lib/node_modules/openclaw/dist/ 2>&1 | head -10")
run("grep -rn 'extract.*accountId\\|extractAccount' /usr/lib/node_modules/openclaw/ 2>&1 | grep -v node_modules | head -10")

# Look for codex-acp process info
run("ps -eo pid,ppid,cmd | grep -E 'codex' | grep -v grep | head")
run("which codex-acp 2>&1; npm list -g --depth=2 2>&1 | head -30")

# Look for chatgpt_account_id usage
run("grep -rn 'chatgpt_account' /usr/lib/node_modules/openclaw/dist/ 2>&1 | grep -v node_modules | head -10")

# Look at @mariozechner/pi-coding-agent
run("ls /usr/lib/node_modules/openclaw/dist/extensions/codex/node_modules/@mariozechner/pi-coding-agent/ 2>&1 | head")
run("find /usr/lib/node_modules/openclaw/dist/extensions/codex/node_modules/@mariozechner -name 'package.json' 2>&1")
run("cat /usr/lib/node_modules/openclaw/dist/extensions/codex/node_modules/@mariozechner/pi-coding-agent/package.json 2>&1 | head -20")

# Search deeply for the message
run("find /usr/lib/node_modules/openclaw -name '*.js' -exec grep -l 'Failed to extract' {} \\; 2>&1 | head -5")
run("find /usr/lib/node_modules -name '*.js' -exec grep -l 'Failed to extract accountId' {} \\; 2>&1 | head -5")

# Try with non-embedded path
print("\n===== Try direct/non-embedded inference path =====")
run("openclaw capability model run --gateway --model 'codex/gpt-5' --prompt 'OK only' 2>&1", timeout=60)

client.close()

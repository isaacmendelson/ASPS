import paramiko, os, sys, io, time
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


# Wait for service to fully come up
print("Waiting 20s...")
time.sleep(20)

run("systemctl status openclaw --no-pager 2>&1 | head -10")
run("openclaw channels status --probe 2>&1 | head -10")

# List codex provider models
print("\n===== List models from codex provider =====")
run("openclaw capability model providers 2>&1 | head -20")
run("openclaw capability model list --provider codex 2>&1 | head -30")

# Test a simple inference
print("\n===== Test inference with gpt-5 via codex =====")
run("timeout 60 openclaw capability model run --model 'codex/gpt-5' --prompt 'Say only OK' 2>&1", timeout=90)

# If that fails, try other models
print("\n===== Test with gpt-5-mini =====")
run("timeout 60 openclaw capability model run --model 'codex/gpt-5-mini' --prompt 'Say only OK' 2>&1", timeout=90)

# Try via openai provider with the API key as fallback comparison
print("\n===== Compare: same call via openai provider =====")
run("set -a; . /etc/openclaw/env; set +a; timeout 60 openclaw capability model run --model 'openai/gpt-4o-mini' --prompt 'Say only OK' 2>&1", timeout=90)

# Recent logs
print("\n===== Recent logs =====")
run("journalctl -u openclaw -n 30 --no-pager --since '1 minute ago' 2>&1 | tail -30")

client.close()

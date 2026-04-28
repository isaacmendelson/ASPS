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


# Step 1: Try discovering models from codex app-server first
print("===== STEP 1: Try codex app-server briefly to see available models =====")
# Start codex app-server, ask for models, then stop
run("timeout 5 codex app-server 2>&1 | head -20 || true", timeout=15)

# Step 2: List codex models from inside OpenClaw
print("\n===== STEP 2: List codex catalog via openclaw =====")
run("openclaw capability model --help 2>&1 | head -20")
run("openclaw capability list 2>&1 | head -20")

# Step 3: Configure agents to use codex harness + codex model
print("\n===== STEP 3: Configure OpenClaw to use codex =====")

# Set embeddedHarness.runtime to codex (so harness uses codex's app-server)
run("openclaw config set agents.defaults.embeddedHarness.runtime codex 2>&1")

# Switch model to a codex-managed one. Codex authenticated user gets GPT-5 etc
# Try gpt-5 first; we'll iterate if it fails
run("openclaw config set agents.defaults.model 'codex/gpt-5' 2>&1")

# Make sure codex plugin is enabled
run("openclaw config set plugins.entries.codex.enabled true 2>&1")

# Validate
run("openclaw config validate 2>&1")
run("cat /root/.openclaw/openclaw.json 2>&1")

# Step 4: Restart service and check
print("\n===== STEP 4: Restart OpenClaw =====")
run("systemctl restart openclaw 2>&1")
time.sleep(20)
run("systemctl status openclaw --no-pager 2>&1 | head -15")

# Step 5: Recent logs
print("\n===== STEP 5: Logs =====")
run("journalctl -u openclaw -n 60 --no-pager --since '30 seconds ago' 2>&1")

# Step 6: Status
print("\n===== STEP 6: Channel status =====")
run("openclaw channels status --probe 2>&1 | head -10")

# Step 7: Try a test inference via openclaw to make sure codex is working
print("\n===== STEP 7: Test inference with codex =====")
run("timeout 30 openclaw capability model run --help 2>&1 | head -20 || true")

client.close()

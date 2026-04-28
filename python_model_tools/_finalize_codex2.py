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


# Step 1: Verify login completed
print("===== STEP 1: Verify codex login =====")
run("ls -la /tmp/codex-login2.done 2>&1")
run("cat /tmp/codex-login2.log 2>&1 | tail -10")
run("codex login status 2>&1")

# Step 2: Verify auth.json content
print("\n===== STEP 2: Verify auth.json =====")
run("python3 -c 'import json; d=json.load(open(\"/root/.codex/auth.json\")); print(\"auth_mode:\", d.get(\"auth_mode\")); print(\"has_tokens:\", \"tokens\" in d); print(\"has_api_key:\", bool(d.get(\"OPENAI_API_KEY\")))' 2>&1")

# Step 3: Start OpenClaw service (without OPENAI_API_KEY in env now)
print("\n===== STEP 3: Start OpenClaw =====")
run("systemctl start openclaw 2>&1")
time.sleep(20)
run("systemctl status openclaw --no-pager 2>&1 | head -10")

# Step 4: Verify auth.json is still chatgpt mode (not overwritten)
print("\n===== STEP 4: Verify auth.json STILL chatgpt mode after openclaw started =====")
run("python3 -c 'import json; d=json.load(open(\"/root/.codex/auth.json\")); print(\"auth_mode:\", d.get(\"auth_mode\"))' 2>&1")

# Step 5: Test inference via codex
print("\n===== STEP 5: Test codex inference =====")
run("timeout 60 openclaw capability model run --model 'codex/gpt-5' --prompt 'Reply with only: WORKING' 2>&1", timeout=90)

# Step 6: If gpt-5 didn't work, try other defaults
print("\n===== STEP 6: Try gpt-5.3-codex (one of codex defaults) =====")
run("timeout 60 openclaw capability model run --model 'codex/gpt-5.3-codex' --prompt 'Reply with only: WORKING' 2>&1", timeout=90)

# Step 7: Channel status
print("\n===== STEP 7: Channel status =====")
run("openclaw channels status --probe 2>&1 | head -10")

# Step 8: Recent logs
print("\n===== STEP 8: Recent logs =====")
run("journalctl -u openclaw -n 30 --no-pager --since '1 minute ago' 2>&1")

client.close()

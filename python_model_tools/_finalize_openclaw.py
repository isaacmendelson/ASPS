import paramiko, os, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

TG_TOKEN = os.environ["TG_TOKEN"]

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
    if o.strip(): print(o.strip()[:3500])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Step 1: See full doctor output (was cut off before)
print("===== STEP 1: Full doctor output =====")
run("openclaw doctor 2>&1 | tail -80", hide=TG_TOKEN)

# Step 2: Set gateway.mode = local
print("\n===== STEP 2: Set gateway.mode = local =====")
run("openclaw config set gateway.mode local 2>&1")

# Step 3: Run doctor --fix to apply auto-suggested fixes
print("\n===== STEP 3: Run doctor --fix =====")
run("openclaw doctor --fix 2>&1 | tail -60", hide=TG_TOKEN)

# Step 4: Validate again
print("\n===== STEP 4: Validate =====")
run("openclaw config validate 2>&1")
run("cat /root/.openclaw/openclaw.json 2>&1", hide=TG_TOKEN)

# Step 5: Try gateway in foreground for 10 seconds
print("\n===== STEP 5: Try gateway briefly =====")
run("timeout 12 bash -c 'set -a; . /etc/openclaw/env; set +a; openclaw gateway --port 18789 2>&1' || true",
    timeout=20, hide=TG_TOKEN)

# Step 6: Restart systemd service
print("\n===== STEP 6: Restart systemd service =====")
run("systemctl restart openclaw 2>&1")
time.sleep(8)
run("systemctl status openclaw --no-pager 2>&1 | head -25")
run("journalctl -u openclaw -n 30 --no-pager 2>&1", hide=TG_TOKEN)

# Step 7: Channel status
print("\n===== STEP 7: Channel status =====")
time.sleep(3)
run("set -a; . /etc/openclaw/env; set +a; openclaw channels list 2>&1", hide=TG_TOKEN)
run("set -a; . /etc/openclaw/env; set +a; openclaw channels status 2>&1", hide=TG_TOKEN)

client.close()

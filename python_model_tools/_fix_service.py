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
    if o.strip(): print(o.strip()[:3000])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Step 1: See all openclaw processes
print("===== STEP 1: Find all openclaw processes =====")
run("ps aux | grep -i openclaw | grep -v grep")
run("ls -la /root/.config/systemd/user/ 2>&1")
run("systemctl --user list-units 2>&1 | head -10 || true")

# Step 2: Stop our system service first
print("\n===== STEP 2: Stop system service =====")
run("systemctl stop openclaw.service 2>&1")
run("systemctl disable openclaw.service 2>&1")

# Step 3: Kill any leftover openclaw processes
print("\n===== STEP 3: Kill any leftover processes =====")
run("pkill -f openclaw 2>&1; sleep 2; ps aux | grep -i openclaw | grep -v grep")

# Step 4: Check if there's a doctor-installed user service running
run("cat /root/.config/systemd/user/openclaw-gateway.service 2>&1")

# Decision: doctor created a USER service at /root/.config/systemd/user/openclaw-gateway.service.
# But we want a SYSTEM service since this is a server (root-owned, runs at boot reliably).
# Let's:
#   1. Remove the user service to avoid confusion
#   2. Use our system service instead

print("\n===== STEP 4: Remove conflicting user service =====")
run("rm -f /root/.config/systemd/user/openclaw-gateway.service 2>&1")

# Step 5: Re-enable and restart our system service
print("\n===== STEP 5: Re-enable system service =====")
run("systemctl daemon-reload")
run("systemctl enable openclaw.service 2>&1")
run("systemctl start openclaw.service 2>&1")
time.sleep(15)
run("systemctl status openclaw --no-pager 2>&1 | head -15")

# Step 6: Check logs
print("\n===== STEP 6: Recent logs =====")
run("journalctl -u openclaw -n 40 --no-pager --since '30 seconds ago' 2>&1", hide=TG_TOKEN)

# Step 7: Status
print("\n===== STEP 7: Channel status =====")
run("set -a; . /etc/openclaw/env; set +a; openclaw channels status --probe 2>&1", hide=TG_TOKEN)

print("\n===== STEP 8: Pairing list (should be empty if user is approved) =====")
run("set -a; . /etc/openclaw/env; set +a; openclaw pairing list 2>&1", hide=TG_TOKEN)

# Step 9: Look for any messages from user / responses sent
print("\n===== STEP 9: Search for activity =====")
run("journalctl -u openclaw -n 100 --no-pager 2>&1 | grep -iE 'message|response|chat|inference|completion|error|fail' | tail -20", hide=TG_TOKEN)

client.close()

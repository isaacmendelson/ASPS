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


print("Waiting 30 seconds for service to fully initialize...")
time.sleep(30)

run("systemctl status openclaw --no-pager 2>&1 | head -10")
run("set -a; . /etc/openclaw/env; set +a; openclaw channels status --probe 2>&1", hide=TG_TOKEN)

# All recent logs
print("\n===== Last 60 log entries =====")
run("journalctl -u openclaw -n 60 --no-pager --since '1 minute ago' 2>&1", hide=TG_TOKEN)

# Search for telegram-specific activity
print("\n===== Telegram-specific activity =====")
run("journalctl -u openclaw -n 200 --no-pager 2>&1 | grep -iE 'telegram|polling|update|message' | tail -20", hide=TG_TOKEN)

# Check for any errors
print("\n===== Errors/warnings =====")
run("journalctl -u openclaw -n 200 --no-pager 2>&1 | grep -iE 'error|warn|fail|except' | grep -v 'health-monitor' | tail -20", hide=TG_TOKEN)

# Confirmed pairing list
run("set -a; . /etc/openclaw/env; set +a; openclaw pairing list --channel telegram 2>&1", hide=TG_TOKEN)

# Memory & processes
run("free -h")
run("ps aux --sort=-%mem | grep -i openclaw | grep -v grep")

client.close()

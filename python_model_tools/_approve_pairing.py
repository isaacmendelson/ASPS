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
    if e.strip(): print(f"[stderr] {e.strip()[:1000]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Approve the pairing
run("set -a; . /etc/openclaw/env; set +a; openclaw pairing approve telegram 3SRTDW85 2>&1", hide=TG_TOKEN)
run("set -a; . /etc/openclaw/env; set +a; openclaw pairing list telegram 2>&1", hide=TG_TOKEN)

# Watch logs as user sends messages
print("\n===== Watching logs for 20s for any messages/errors =====")
time.sleep(20)
run("journalctl -u openclaw -n 50 --no-pager --since '1 minute ago' 2>&1 | tail -40", hide=TG_TOKEN)

# Check if there are auth profiles needed
print("\n===== Auth info =====")
run("set -a; . /etc/openclaw/env; set +a; openclaw auth --help 2>&1 | head -30", hide=TG_TOKEN)

client.close()

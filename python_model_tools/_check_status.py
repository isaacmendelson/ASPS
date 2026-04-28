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


# Wait for service to fully come up
print("Waiting 15s for service to fully initialize...")
time.sleep(15)

run("systemctl status openclaw --no-pager 2>&1 | head -8")
run("set -a; . /etc/openclaw/env; set +a; openclaw channels status --probe 2>&1", hide=TG_TOKEN)

# Check recent logs - especially for telegram polling
print("\n===== Recent OpenClaw logs (telegram-related) =====")
run("journalctl -u openclaw -n 100 --no-pager 2>&1 | grep -iE 'telegram|polling|openai|connected|error|warn' | tail -30", hide=TG_TOKEN)

# Show all recent logs to see what's happening
print("\n===== All recent logs (last 50 lines) =====")
run("journalctl -u openclaw -n 50 --no-pager 2>&1 | tail -50", hide=TG_TOKEN)

# Check pairing
print("\n===== Pairing list =====")
run("set -a; . /etc/openclaw/env; set +a; openclaw pairing list telegram 2>&1 || openclaw pairing list 2>&1", hide=TG_TOKEN)

# Check network connectivity to Telegram
print("\n===== Telegram API connectivity =====")
run(f"curl -s -o /dev/null -w 'HTTP %{{http_code}}\\n' 'https://api.telegram.org/bot{TG_TOKEN}/getMe' 2>&1", hide=TG_TOKEN)
run(f"curl -s 'https://api.telegram.org/bot{TG_TOKEN}/getMe' | python3 -m json.tool 2>&1", hide=TG_TOKEN)

# Check OpenAI connectivity
print("\n===== OpenAI API check =====")
run("set -a; . /etc/openclaw/env; set +a; curl -s -H \"Authorization: Bearer $OPENAI_API_KEY\" https://api.openai.com/v1/models 2>&1 | python3 -c 'import json,sys; d=json.load(sys.stdin); print(\"OK,\",len(d.get(\"data\",[])),\"models accessible\") if \"data\" in d else print(\"ERROR:\",d)'")

client.close()

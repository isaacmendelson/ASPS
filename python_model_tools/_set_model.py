import paramiko, os, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

MODEL = os.environ.get("MODEL", "openai/gpt-5.3-codex")

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)


def run(cmd, timeout=60):
    print(f"\n$ {cmd[:120]}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    if o.strip(): print(o.strip()[:2500])
    if e.strip(): print(f"[stderr] {e.strip()[:1000]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Test the model is callable first
print(f"===== Test model {MODEL} works =====")
run(f"""set -a; . /etc/openclaw/env; set +a; curl -s -X POST https://api.openai.com/v1/chat/completions \\
  -H "Authorization: Bearer $OPENAI_API_KEY" \\
  -H "Content-Type: application/json" \\
  -d '{{"model": "{MODEL.split('/')[-1]}", "messages": [{{"role":"user","content":"Reply with just OK"}}], "max_completion_tokens": 50}}' | python3 -m json.tool 2>&1 | head -30""")

# Set the model in config
print(f"\n===== Set agents.defaults.model = {MODEL} =====")
run(f"openclaw config set agents.defaults.model '{MODEL}' 2>&1")
run("openclaw config validate 2>&1")
run("openclaw config get agents.defaults.model 2>&1")

# Restart service
print("\n===== Restart OpenClaw service =====")
run("systemctl restart openclaw 2>&1")
time.sleep(15)
run("systemctl status openclaw --no-pager 2>&1 | head -10")

# Verify in logs
print("\n===== Check logs for model =====")
run("journalctl -u openclaw -n 50 --no-pager --since '20 seconds ago' 2>&1 | grep -iE 'model|telegram|ready'")

# Channel status
print("\n===== Channel status =====")
run("openclaw channels status --probe 2>&1")

client.close()

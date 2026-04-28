import paramiko, os, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

TG_TOKEN = os.environ["TG_TOKEN"]
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

def run(cmd, timeout=60, hide=None):
    display = cmd if not hide else cmd.replace(hide, "***")
    print(f"\n$ {display[:120]}")
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


# 1. Stop OpenClaw to prevent it from interfering with auth
print("===== STEP 1: Stop OpenClaw =====")
run("systemctl stop openclaw 2>&1")
run("ps aux | grep -E 'openclaw|codex' | grep -v grep | head")

# 2. Remove OPENAI_API_KEY from env file (keep only TG_TOKEN + others)
print("\n===== STEP 2: Remove OPENAI_API_KEY from /etc/openclaw/env =====")
sftp = client.open_sftp()
new_env = f"""TELEGRAM_BOT_TOKEN={TG_TOKEN}
NODE_COMPILE_CACHE=/var/tmp/openclaw-compile-cache
OPENCLAW_NO_RESPAWN=1
"""
with sftp.file("/etc/openclaw/env", "w") as f:
    f.write(new_env)
sftp.chmod("/etc/openclaw/env", 0o600)
sftp.close()
print("Updated /etc/openclaw/env (removed OPENAI_API_KEY)")
run("cat /etc/openclaw/env 2>&1 | grep -v BOT_TOKEN", hide=TG_TOKEN)

# 3. Logout codex first to clean state
print("\n===== STEP 3: Codex logout =====")
run("codex logout 2>&1")
run("ls /root/.codex/auth.json 2>&1 || echo 'auth removed'")

# 4. Start a NEW device-auth login (background)
print("\n===== STEP 4: Start NEW codex login --device-auth =====")
run("rm -f /tmp/codex-login2.log /tmp/codex-login2.done")
run("nohup bash -c 'codex login --device-auth > /tmp/codex-login2.log 2>&1; touch /tmp/codex-login2.done' >/dev/null 2>&1 &")
time.sleep(6)
run("cat /tmp/codex-login2.log 2>&1")

print("\n===== READY FOR USER ACTION =====")
print("User must visit URL + enter code shown above")

client.close()

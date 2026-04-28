"""
Configure OpenClaw with Telegram + OpenAI, set up systemd service.
"""
import paramiko, os, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

TG_TOKEN = os.environ["TG_TOKEN"]
OPENAI_KEY = os.environ["OPENAI_KEY"]
MODEL = "openai/gpt-4o-mini"

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)


def run(cmd, timeout=60, hide_secret=None):
    display = cmd if not hide_secret else cmd.replace(hide_secret, "***SECRET***")
    print(f"\n$ {display}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    # Sanitize secrets from output
    if hide_secret:
        o = o.replace(hide_secret, "***SECRET***")
        e = e.replace(hide_secret, "***SECRET***")
    if o.strip(): print(o.strip()[:2500])
    if e.strip(): print(f"[stderr] {e.strip()[:1000]}")
    print(f"[exit {rc}]")
    return rc, o, e


# === STEP 1: Create /etc/openclaw with secret env file ===
print("===== STEP 1: Create secure env file =====")
run("mkdir -p /etc/openclaw && chmod 750 /etc/openclaw")

sftp = client.open_sftp()
env_content = f"""OPENAI_API_KEY={OPENAI_KEY}
TELEGRAM_BOT_TOKEN={TG_TOKEN}
NODE_COMPILE_CACHE=/var/tmp/openclaw-compile-cache
OPENCLAW_NO_RESPAWN=1
"""
with sftp.file("/etc/openclaw/env", "w") as f:
    f.write(env_content)
sftp.chmod("/etc/openclaw/env", 0o600)
sftp.close()
print("Wrote /etc/openclaw/env (mode 600)")
run("ls -la /etc/openclaw/env && wc -l /etc/openclaw/env")

run("mkdir -p /var/tmp/openclaw-compile-cache")

# === STEP 2: Initialize OpenClaw config (create dir + base config) ===
print("\n===== STEP 2: Initialize OpenClaw config =====")
run("mkdir -p /root/.openclaw")

# Create a base openclaw.json. The schema supports SecretRef for tokens via env.
# But simplest path: Use the env var TELEGRAM_BOT_TOKEN (per docs, applies to default account).
# For OpenAI, set OPENAI_API_KEY env (standard for OpenAI SDK).
base_config = """{
  "agent": {
    "model": "openai/gpt-4o-mini"
  },
  "channels": {
    "telegram": {
      "enabled": true,
      "dmPolicy": "pairing"
    }
  }
}
"""
sftp = client.open_sftp()
with sftp.file("/root/.openclaw/openclaw.json", "w") as f:
    f.write(base_config)
sftp.close()
print("Wrote /root/.openclaw/openclaw.json")
run("cat /root/.openclaw/openclaw.json")

# === STEP 3: Add telegram channel non-interactively ===
print("\n===== STEP 3: Add Telegram channel via CLI =====")
# Source the env file so OPENAI_API_KEY etc are available, and run channel add
run(
    f"set -a; . /etc/openclaw/env; set +a; openclaw channels add --channel telegram --token '{TG_TOKEN}' 2>&1",
    timeout=60,
    hide_secret=TG_TOKEN,
)

# === STEP 4: Validate config ===
print("\n===== STEP 4: Validate config =====")
run("set -a; . /etc/openclaw/env; set +a; openclaw config validate 2>&1", timeout=30, hide_secret=TG_TOKEN)
run("set -a; . /etc/openclaw/env; set +a; openclaw config get channels.telegram 2>&1", timeout=30, hide_secret=TG_TOKEN)
run("set -a; . /etc/openclaw/env; set +a; openclaw config get agent.model 2>&1", timeout=30)

# === STEP 5: Create systemd service ===
print("\n===== STEP 5: Create systemd service =====")
service_unit = """[Unit]
Description=OpenClaw Gateway (personal AI assistant)
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=root
EnvironmentFile=/etc/openclaw/env
ExecStart=/usr/bin/openclaw gateway --port 18789
Restart=always
RestartSec=5
StandardOutput=journal
StandardError=journal
# Security hardening (light - need network + read user data)
NoNewPrivileges=true
ProtectSystem=full
ProtectHome=false

[Install]
WantedBy=multi-user.target
"""
sftp = client.open_sftp()
with sftp.file("/etc/systemd/system/openclaw.service", "w") as f:
    f.write(service_unit)
sftp.close()
print("Wrote /etc/systemd/system/openclaw.service")

run("systemctl daemon-reload")
run("systemctl enable openclaw.service 2>&1")
run("systemctl start openclaw.service 2>&1")
time.sleep(5)
run("systemctl status openclaw.service --no-pager 2>&1 | head -25")

# === STEP 6: Check logs ===
print("\n===== STEP 6: Check OpenClaw logs =====")
run("journalctl -u openclaw -n 50 --no-pager 2>&1", timeout=20, hide_secret=TG_TOKEN)

# === STEP 7: Show channel status ===
print("\n===== STEP 7: Channel status =====")
time.sleep(3)
run("set -a; . /etc/openclaw/env; set +a; openclaw channels list 2>&1", timeout=30, hide_secret=TG_TOKEN)
run("set -a; . /etc/openclaw/env; set +a; openclaw channels status 2>&1", timeout=30, hide_secret=TG_TOKEN)

print("\n===== DONE =====")
print("Now: send a DM to your Telegram bot, then run pairing approve")
client.close()

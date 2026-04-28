import paramiko, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

cmds = [
    "openclaw config --help 2>&1",
    "openclaw config set --help 2>&1",
    "openclaw configure --help 2>&1",
    "openclaw channels --help 2>&1",
    "openclaw channels telegram --help 2>&1 || openclaw pairing --help 2>&1",
    "openclaw onboard --help 2>&1",
    "openclaw gateway --help 2>&1",
    "openclaw config file 2>&1",
    "openclaw config validate 2>&1 || true",
    "openclaw agents --help 2>&1",
    "openclaw capability --help 2>&1",
    "openclaw credentials --help 2>&1 || openclaw secrets --help 2>&1 || echo 'no creds command'",
]
for c in cmds:
    print(f"\n===== $ {c[:80]} =====")
    _, out, err = client.exec_command(c, timeout=20)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    if o.strip(): print(o.strip()[:2500])
    if e.strip(): print(f"[stderr] {e.strip()[:500]}")
client.close()

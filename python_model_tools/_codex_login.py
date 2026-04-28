"""
Initiate codex login --device-auth and capture URL+code for user.
"""
import paramiko, os, sys, io, time, threading
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
    if o.strip(): print(o.strip()[:3000])
    if e.strip(): print(f"[stderr] {e.strip()[:1000]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Look at existing auth.json - it might already have valid auth
print("===== Check existing auth.json =====")
run("cat /root/.codex/auth.json 2>&1 | head -10")

# Try login status (with/without TTY)
print("\n===== Login status =====")
run("codex login status 2>&1")

# Logout first if there's a stale token, to ensure clean device-auth flow
print("\n===== Clean logout =====")
run("codex logout 2>&1 || true")
run("ls -la /root/.codex/auth.json 2>&1 || echo 'auth.json removed'")

# Now start device-auth login in BACKGROUND so we can read URL/code
print("\n===== Starting device-auth login (background) =====")

# Run codex login --device-auth, redirect output to a file we can poll
# It will block waiting for the user to authenticate, so run it in background
run("rm -f /tmp/codex-login.log /tmp/codex-login.done 2>&1")
run("nohup bash -c 'codex login --device-auth > /tmp/codex-login.log 2>&1; touch /tmp/codex-login.done' > /dev/null 2>&1 &", timeout=10)

# Wait a moment for it to print the device code/URL
time.sleep(5)

print("\n===== Output from codex login (so far) =====")
run("cat /tmp/codex-login.log 2>&1")
run("ls -la /tmp/codex-login.log /tmp/codex-login.done 2>&1")

# Show raw bytes if file is hard to read
run("xxd /tmp/codex-login.log 2>&1 | head -30 || cat -A /tmp/codex-login.log")

# Sometimes device flow needs a bit more time
print("\n===== Wait a bit more, then re-check =====")
time.sleep(5)
run("cat /tmp/codex-login.log 2>&1")

# Show running processes
run("ps aux | grep codex | grep -v grep")

client.close()

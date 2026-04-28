import paramiko, os, sys, io, time
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
    if o.strip(): print(o.strip()[:5000])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e


# Step 1: Install OpenAI Codex CLI
print("===== STEP 1: Install OpenAI Codex CLI =====")
run("which codex 2>&1 || echo 'not installed'")
run("npm install -g @openai/codex 2>&1 | tail -10", timeout=300)
run("which codex && codex --version 2>&1")

# Step 2: Show available login options
print("\n===== STEP 2: Codex CLI commands =====")
run("codex --help 2>&1 | head -50")
run("codex login --help 2>&1 | head -30")

# Step 3: Check existing auth state
print("\n===== STEP 3: Check existing auth =====")
run("ls -la /root/.codex 2>&1 || echo 'not yet logged in'")
run("codex whoami 2>&1 || true")

client.close()

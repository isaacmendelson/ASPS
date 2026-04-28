import paramiko, os, sys, io, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)


def run(cmd, timeout=60):
    print(f"\n$ {cmd[:100]}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    if o.strip(): print(o.strip()[:3000])
    if e.strip(): print(f"[stderr] {e.strip()[:1000]}")
    print(f"[exit {rc}]")
    return rc, o, e


# 1. Find available codex / GPT-5 / latest models
print("===== Find available codex / latest models =====")
run("""set -a; . /etc/openclaw/env; set +a; curl -s -H "Authorization: Bearer $OPENAI_API_KEY" https://api.openai.com/v1/models | python3 -c '
import json, sys
d = json.load(sys.stdin)
models = [m["id"] for m in d.get("data", [])]
codex = sorted([m for m in models if "codex" in m.lower()])
gpt5 = sorted([m for m in models if "gpt-5" in m.lower()])
gpt4 = sorted([m for m in models if "gpt-4" in m.lower()])[:10]
o_models = sorted([m for m in models if m.startswith(("o1", "o3", "o4", "o5"))])
print("=== Codex models ===")
for m in codex: print(f"  {m}")
print("\\n=== GPT-5 models ===")
for m in gpt5: print(f"  {m}")
print("\\n=== o-series (reasoning) ===")
for m in o_models: print(f"  {m}")
print("\\n=== GPT-4 models (first 10) ===")
for m in gpt4: print(f"  {m}")
print(f"\\n=== Total models: {len(models)} ===")
'
""")

client.close()

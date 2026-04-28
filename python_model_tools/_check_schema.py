import paramiko, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

# First save current config and reset
cmds = [
    "mv /root/.openclaw/openclaw.json /root/.openclaw/openclaw.json.bad 2>&1",
    "echo '{}' > /root/.openclaw/openclaw.json",
    # Get top-level keys from schema
    "openclaw config schema 2>&1 | python3 -c 'import json,sys; s=json.load(sys.stdin); props=s.get(\"properties\",{}); print(\"Top-level keys:\"); [print(f\"  {k}: {v.get(\\\"type\\\",\\\"?\\\")} - {v.get(\\\"description\\\",\\\"\\\")[:80]}\") for k,v in props.items()]'",
    # Get agents section structure
    "openclaw config schema 2>&1 | python3 -c 'import json,sys; s=json.load(sys.stdin); a=s.get(\"properties\",{}).get(\"agents\",{}); print(json.dumps(a, indent=2)[:3000])'",
    # Get channels section structure
    "openclaw config schema 2>&1 | python3 -c 'import json,sys; s=json.load(sys.stdin); a=s.get(\"properties\",{}).get(\"channels\",{}); print(json.dumps(a, indent=2)[:3000])'",
]
for c in cmds:
    print(f"\n===== $ {c[:100]} =====")
    _, out, err = client.exec_command(c, timeout=30)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    if o.strip(): print(o.strip())
    if e.strip(): print(f"[stderr] {e.strip()[:500]}")
client.close()

import paramiko, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

# Write the python query to a file to avoid quoting hell
sftp = client.open_sftp()
script = '''
import json, sys
s = json.load(sys.stdin)
tg = s["properties"]["channels"]["properties"]["telegram"]
# print top-level fields
print("=== Telegram fields ===")
for k, v in tg.get("properties", {}).items():
    desc = v.get("description", "")
    typ = v.get("type", v.get("anyOf", "?"))
    print(f"  {k}: {typ if isinstance(typ, str) else 'union'} - {str(desc)[:100]}")

print("\\n=== Required ===")
print(tg.get("required", []))

print("\\n=== Full enabled+token+dmPolicy ===")
for k in ["enabled", "token", "botToken", "dmPolicy", "name"]:
    if k in tg.get("properties", {}):
        print(f"\\n[{k}]:")
        print(json.dumps(tg["properties"][k], indent=2)[:800])
'''
with sftp.file("/tmp/parse_schema.py", "w") as f:
    f.write(script)
sftp.close()

cmds = [
    "openclaw config schema 2>&1 | python3 /tmp/parse_schema.py",
    # Also check agents.defaults
    "openclaw config schema 2>&1 | python3 -c \"import json, sys; s=json.load(sys.stdin); print(json.dumps(s['properties']['agents']['properties']['defaults']['properties']['model'], indent=2))\"",
]
for c in cmds:
    print(f"\n===== $ {c[:80]} =====")
    _, out, err = client.exec_command(c, timeout=30)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    if o.strip(): print(o.strip()[:5000])
    if e.strip(): print(f"[stderr] {e.strip()[:500]}")
client.close()

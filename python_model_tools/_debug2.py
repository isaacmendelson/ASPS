import paramiko, os, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

def run(cmd, timeout=60):
    print(f"\n$ {cmd[:130]}")
    _, out, err = client.exec_command(cmd, timeout=timeout)
    o = out.read().decode(errors="replace")
    e = err.read().decode(errors="replace")
    rc = out.channel.recv_exit_status()
    if o.strip(): print(o.strip()[:5000])
    if e.strip(): print(f"[stderr] {e.strip()[:1500]}")
    print(f"[exit {rc}]")
    return rc, o, e

# Find the auth extraction code
run("grep -rn 'Failed to extract accountId\\|accountId' /usr/lib/node_modules/openclaw/dist/extensions/codex/ --include='*.js' 2>&1 | grep -v 'node_modules' | head -10")

# Check decoded JWT - rewrite the python carefully
sftp = client.open_sftp()
script = r'''import json, base64
auth = json.load(open("/root/.codex/auth.json"))
print("Keys:", list(auth.keys()))
print()
print("Full content (sensitive masked):")
def mask(v):
    if isinstance(v, str) and len(v) > 30:
        return v[:25] + "...[masked]"
    return v
def walk(o, indent=0):
    if isinstance(o, dict):
        for k, v in o.items():
            if isinstance(v, dict):
                print(" "*indent + f"{k}:")
                walk(v, indent+2)
            else:
                print(" "*indent + f"{k}: {mask(v)}")
walk(auth)

# Try decoding tokens
print()
def try_decode(name, tok):
    parts = tok.split(".")
    if len(parts) < 2:
        return None
    p = parts[1] + "=" * (4 - len(parts[1]) % 4)
    try:
        return json.loads(base64.urlsafe_b64decode(p))
    except Exception as e:
        return f"ERROR: {e}"

for keypath in ["id_token", "access_token", "tokens.id_token", "tokens.access_token"]:
    o = auth
    parts_p = keypath.split(".")
    try:
        for p in parts_p:
            o = o[p]
    except (KeyError, TypeError):
        continue
    if isinstance(o, str) and "." in o:
        decoded = try_decode(keypath, o)
        if isinstance(decoded, dict):
            print(f"=== {keypath} payload ===")
            print(json.dumps(decoded, indent=2)[:2000])
'''
with sftp.file("/tmp/decode_auth.py", "w") as f:
    f.write(script)
sftp.close()
run("python3 /tmp/decode_auth.py 2>&1")

# Find the harness code that does authentication
run("grep -rn 'Failed to extract' /usr/lib/node_modules/openclaw/dist/ 2>&1 | grep -v node_modules | head -10")
run("grep -rln 'extract.*account' /usr/lib/node_modules/openclaw/dist/extensions/codex/*.js 2>&1 | head -5")

# Check harness.js for auth extraction
run("grep -n 'account\\|token\\|extract' /usr/lib/node_modules/openclaw/dist/extensions/codex/harness.js 2>&1 | head -30")

client.close()

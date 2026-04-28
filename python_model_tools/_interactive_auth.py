"""
Run OpenClaw's openai-codex auth login interactively, select Device Pairing,
capture the device code/URL.
"""
import paramiko, os, sys, io, time, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

client = paramiko.SSHClient()
client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
client.connect("168.231.111.91", username="root", password=os.environ["SSH_PASS"], timeout=15)

chan = client.invoke_shell()


def read_output(timeout=5, settle_time=1.0):
    """Read output until no more data for settle_time seconds."""
    out = b""
    deadline = time.time() + timeout
    last_data = time.time()
    while time.time() < deadline:
        if chan.recv_ready():
            chunk = chan.recv(8192)
            out += chunk
            last_data = time.time()
        else:
            if time.time() - last_data > settle_time:
                break
            time.sleep(0.1)
    return out.decode(errors="replace")


# Wait for prompt
read_output(timeout=3)

# Run the login command
print(">>> Running: openclaw capability model auth login --provider openai-codex")
chan.send("openclaw capability model auth login --provider openai-codex 2>&1\n")
time.sleep(5)

out = read_output(timeout=8, settle_time=1.5)
print("=== Initial output ===")
print(out)

# Select "Device Pairing" by sending Down Arrow + Enter
print("\n>>> Selecting Device Pairing (Down arrow + Enter)")
chan.send("\x1b[B")  # Down arrow
time.sleep(0.5)
chan.send("\r")  # Enter
time.sleep(2)

out2 = read_output(timeout=15, settle_time=2)
print("=== After selection ===")
print(out2)

# Look for URL/code in output
combined = out + out2
url_match = re.search(r"https://\S+", combined)
code_match = re.search(r"\b[A-Z0-9]{4,5}-?[A-Z0-9]{4,5}\b", combined)

print(f"\n=== Detected ===")
print(f"URL: {url_match.group() if url_match else 'NOT FOUND'}")
print(f"Code: {code_match.group() if code_match else 'NOT FOUND'}")

# Keep the channel open - we need user to authenticate, then check result
# Save it so we can resume later
print("\nLeaving connection open for 3 minutes to wait for user to authenticate...")
print("After user authenticates, run check script.")

# Wait for auth completion (poll for new output)
print("\n=== Waiting up to 5 minutes for authentication to complete ===")
end = time.time() + 300
while time.time() < end:
    if chan.recv_ready():
        out3 = read_output(timeout=2, settle_time=1)
        print(out3, end="")
        if "ogged in" in out3 or "ogin successful" in out3 or "uccess" in out3 or "rofile" in out3 or "$" in out3:
            print("\n>>> Detected completion or shell prompt")
            break
        if "rror" in out3 or "ailed" in out3 or "ncel" in out3:
            print("\n>>> Detected error")
            break
    time.sleep(2)

# Final read
final = read_output(timeout=2)
print("\n=== Final output ===")
print(final)

# Check auth status
chan.send("openclaw capability model auth status 2>&1 | head -50\n")
time.sleep(5)
status = read_output(timeout=10, settle_time=2)
print("\n=== Auth status ===")
print(status)

chan.send("exit\n")
time.sleep(1)
client.close()

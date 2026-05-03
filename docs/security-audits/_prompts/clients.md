You are a senior application security engineer performing a CISO-level audit of the ASPS client-side components: Chrome browser extension, Python desktop agent (Windows), and mobile agents spec (Android/iOS).

**Codebase root:** c:\Jobs\ASPS\GitHub\Software

**Components:**
- Browser extension: `apps/extension/chrome/`
- Python desktop agent: `apps/desktop/win/`
- Mobile spec: `docs/ARCHITECTURE.md` §16, `docs/ASPS_DATA_FLOW.md` §10

**For each finding:** title, severity, CWE, evidence (file:line), risk, recommendation.

Investigate:

1. **Extension permissions** — `manifest.json`: `<all_urls>`, `tabs`, `webRequest`, `cookies`, CSP, `web_accessible_resources`, `externally_connectable`.

2. **Extension code injection / XSS** — content scripts: `innerHTML`, `eval`, `Function()`, `document.write`, `setTimeout(string)`. Sender origin validation on `chrome.runtime.onMessage`.

3. **Extension network** — backend URL hardcoded? Cert pinning? Auth token storage in `chrome.storage.local`.

4. **Python agent privileges** — UAC required? Registry writes (HKLM)? Service install? Auto-update mechanism with code-signing?

5. **Python agent secrets** — `auth.json` location and ACLs. Hardcoded URLs, ports, public keys, dev tokens. OAuth `client_secret` embedded in binary.

6. **Python agent privacy** — `psutil`, `requests`, `winreg` usage. Browser history scope. Remote-access logs. GeoIP lookups (HTTP vs HTTPS, third-party).

7. **Extension↔agent IPC** — local WebSocket/native messaging port. Auth on localhost. Origin validation.

8. **Mobile spec threat model** — Android Accessibility Service scope, SMS reading (Play Store policy), iOS Network Extension limitations, key escrow / device revocation, ZMQ-CURVE on JeroMQ/SwiftyZeroMQ.

9. **Update / supply-chain** — extension distribution channel (Chrome Web Store account MFA?). Desktop installer code-signing. SHA-256 checksums alone are insufficient.

10. **Telemetry / logging** — `events.jsonl` retention, sensitive content (URLs, IPs) in logs, cleanup on uninstall, `print()` of payloads to stdout.

**Output:** Markdown with sections per area. Cite file:line. Mark hypothetical findings as "potential".

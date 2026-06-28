---
name: mv3-content-script
description: Add or extend a Chrome MV3 content script in the AntiScam extension. Covers manifest declarations, the existing message-bus pattern, and the MV3 constraints that bite first-timers.
---

# /mv3-content-script

Scaffolds a new content-script feature in [apps/extension/chrome/](c:/Jobs/ASPS/GitHub/Software/apps/extension/chrome/) and wires it into the existing architecture: `manifest.json` declarations + `content.js` integration + `MessageBus` to talk to `background.js`.

## When to invoke
- User wants to add page-context behavior (DOM access, page-side detection, UI overlay).
- User says "new content script", "extension feature on the page", "DOM monitor", "page overlay".

## Architecture recap

The extension splits into three execution contexts. Each context has different capabilities and limits — pick the right one for the feature:

| Context | File(s) | Has DOM | Has chrome.* APIs | Module system |
|---|---|---|---|---|
| **Content script** | `content.js` | ✅ | Limited (no `chrome.cookies`, no `chrome.tabs`, no `chrome.webRequest`) | IIFE (not ES modules — manifest doesn't declare `"type": "module"`) |
| **Background service worker** | `background.js` + `services/*.js` | ❌ | Full | ES modules (`import`/`export`) |
| **Popup** | `popup.html` + `popup.js` | Popup DOM only (not page DOM) | Full | ES modules |

Content ↔ background communication goes through `chrome.runtime.sendMessage` / `onMessage`, wrapped in [messaging/MessageBus.js](c:/Jobs/ASPS/GitHub/Software/apps/extension/chrome/messaging/MessageBus.js) and typed via [messaging/MessageTypes.js](c:/Jobs/ASPS/GitHub/Software/apps/extension/chrome/messaging/MessageTypes.js).

## Ask first

1. **Does this feature need DOM access?**
   - Reading the page, injecting an overlay, watching mutations → content script.
   - Just listening for events, calling APIs, persisting state → background service.
   - Most "new content script" requests are really *new content-script features inside the existing `content.js`*, plus a new background service to handle the response.

2. **Does it need new origin permissions?** If the user wants to fetch from a new backend host or read cookies from a new domain, `host_permissions` in the manifest needs updating.

3. **Does it need a new `run_at` or `world`?** Default `document_end` is right for almost everything. Use `document_start` only if you must run before the page's own scripts. `world: "MAIN"` exposes the script to the page's JS scope — security trap, avoid unless asked.

## Files to create / modify

### 1. Manifest — `apps/extension/chrome/manifest.json`

**Add permissions only if new.** Current set: `activeTab, tabs, webNavigation, storage, notifications, alarms, cookies`. Don't add ones you won't use — Chrome Web Store review flags excess permissions.

If the feature needs a new origin (e.g. fetching from a new analyzer host), add to `host_permissions`.

If splitting into a separate content script (rare — usually extend `content.js` instead):

```json
"content_scripts": [
  { "matches": ["<all_urls>"], "js": ["content.js"], "css": ["content.css"], "run_at": "document_end" },
  { "matches": ["https://specific.example.com/*"], "js": ["my-feature-content.js"], "run_at": "document_idle" }
]
```

### 2. Content script — extend `content.js` (preferred) OR new file

**Default: extend `content.js`.** Add a new service object inside the existing IIFE, register its message handlers via the existing pattern. Splitting into a new file is justified only when the feature is for a specific origin and shouldn't run everywhere.

Pattern inside `content.js`:

```javascript
const MyFeatureService = {
  init() {
    // wire DOM observers, listeners
    document.addEventListener('...', this.onEvent.bind(this));
  },
  onEvent(e) {
    // ... do stuff ...
    chrome.runtime.sendMessage({
      type: 'myfeature:detected',
      payload: { /* serializable */ }
    });
  }
};

// Bottom of the IIFE:
MyFeatureService.init();
```

**Constraint:** anything sent via `chrome.runtime.sendMessage` must be **structured-clonable** — no DOM nodes, no functions, no class instances with methods. Pass plain objects / arrays / primitives.

### 3. Message type — `messaging/MessageTypes.js`

Add a constant so the type isn't string-literal-duplicated across files:

```javascript
export const MY_FEATURE_MSG = {
  DETECTED: 'myfeature:detected',
  RESPONSE: 'myfeature:response'
};
```

### 4. Background handler — new file in `services/` OR extend an existing one

If the feature is large, create `services/MyFeatureService.js`:

```javascript
import { messageBus } from '../messaging/index.js';
import { MY_FEATURE_MSG } from '../messaging/MessageTypes.js';

class MyFeatureService {
  init() {
    messageBus.on(MY_FEATURE_MSG.DETECTED, this.handle.bind(this));
  }
  async handle(payload, sender) {
    // ... process, possibly fetch backend, return result ...
    return { ok: true, /* ... */ };
  }
}

export const myFeatureService = new MyFeatureService();
```

Then in `background.js`:

```javascript
import { myFeatureService } from './services/MyFeatureService.js';
// ... existing imports ...
myFeatureService.init();
```

### 5. Bump manifest version

Increment `manifest.json` → `version`. Current is `0.0.1.4`. Use 4-part for the extension version (CWS allows up to 4-part dotted versions).

> **Note for SCRUM-906:** the extension update mechanism uses both CWS and the SCRUM-863 control plane reuse. If you're adding a feature that's part of a coordinated agent + extension release, also follow [/velopack-publish](c:/Jobs/ASPS/GitHub/Software/.claude/skills/velopack-publish.md) for the agent side and document the version pairing in the JIRA ticket.

### 6. Tests — `tests/unit/services/`

Existing service tests follow a pattern: mock `chrome.*` APIs in `setup/jest.setup.cjs`, then unit-test the service class. Add a test file matching the convention:

```
apps/extension/chrome/tests/unit/services/MyFeatureService.test.js
```

## MV3 traps to surface

These bite first-timers. Mention the relevant ones to the user up front:

- **Service workers terminate after idle.** Background state in module-level variables is lost when the SW unloads. Persist via `chrome.storage.local` for anything that must survive an unload.
- **No `XMLHttpRequest` in service workers** — use `fetch`.
- **No `setTimeout` longer than ~5min** in service workers (the SW gets unloaded). Use `chrome.alarms` for delayed/recurring work.
- **`<all_urls>` host permissions** are aggressive and trigger CWS review. If the feature works on a known set of origins, list them explicitly.
- **Content scripts cannot access `chrome.storage.local` directly with module syntax** — they use the same `chrome.storage` API but cannot `import` from the background's ES modules.
- **Message responses must be sync OR return `true` from the listener** to keep the message channel open for an async response. The existing MessageBus wraps this; if you bypass it, follow the rule.

## Verification

1. Load unpacked: `chrome://extensions/` → Developer mode → Load unpacked → `apps/extension/chrome/`.
2. Open DevTools on a target page → Console → confirm the content script's debug logs appear.
3. Open the service worker console: `chrome://extensions/` → click "service worker" link under AntiScam Protection → confirm background logs.
4. Trigger the feature → confirm message flows: content `console.log('sent')` → background `console.log('received')` → optional backend → background `console.log('replied')` → content `console.log('got reply')`.
5. Existing tests: `cd apps/extension/chrome/tests && npm test`.

## Never

- Use `eval()` or inject inline scripts. CWS rejects extensions doing this and MV3 blocks remotely-hosted code.
- Add `host_permissions: ["<all_urls>"]` if the feature only needs specific origins.
- Pass DOM nodes through `sendMessage` — they're not structured-clonable. Serialize what you need.
- Mutate `manifest.json` permissions without updating `README.md` — review will ask why.
- Reuse a message-type string across unrelated features. The handler routing breaks subtly.

## Output convention

```
Feature: <Name>
Files modified:
  - apps/extension/chrome/manifest.json (version bumped, [permissions added: ...])
  - apps/extension/chrome/content.js (added <Name>Service)
  - apps/extension/chrome/messaging/MessageTypes.js (added <Name>_MSG)
  - apps/extension/chrome/background.js (wired <name>Service.init())
Files created:
  - apps/extension/chrome/services/<Name>Service.js
  - apps/extension/chrome/tests/unit/services/<Name>Service.test.js
Unit tests: PASS/FAIL
Manual test: PASS/FAIL (loaded unpacked, message round-trip confirmed)
```

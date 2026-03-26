# E2E Test Scenarios for AntiScam Extension

## Overview
These scenarios should be tested manually across all supported browsers:
- Chrome
- Edge
- Firefox
- Comet (if available)

## Test Environment Setup

### Prerequisites
1. Desktop app running and connected
2. Backend API accessible
3. Test user account with valid credentials
4. Extension installed in browser

### Test Data
- **Safe URL**: https://www.google.com
- **Phishing URL**: Use controlled test phishing site or ASPS test URL
- **Blocked URL**: Use known malicious test URL

---

## Scenario 1: Extension Installation & First Run

### Steps:
1. Load unpacked extension from `chrome/` directory
2. Extension icon should appear in toolbar
3. Click extension icon to open popup
4. Verify version number matches manifest.json

### Expected Results:
- ✅ Extension installs without errors
- ✅ Icon appears in toolbar (colored logo)
- ✅ Popup displays "Disconnected" status (red indicator)
- ✅ Version number displayed correctly
- ✅ No JavaScript errors in console

### Browser-Specific Notes:
- **Firefox**: Use `about:debugging` → "Load Temporary Add-on"
- **Edge**: Use same process as Chrome

---

## Scenario 2: Desktop App Connection

### Steps:
1. Start desktop app
2. Verify app shows "waiting for connection" or similar
3. Extension should auto-connect within 5 seconds
4. Check extension popup

### Expected Results:
- ✅ Extension badge turns green (connected)
- ✅ Popup shows "Connected" status
- ✅ Desktop status: ✓ Connected
- ✅ Server status: ✓ Connected
- ✅ User email displayed in popup
- ✅ Console shows successful WebSocket connection

### Troubleshooting:
- If connection fails, check WebSocket port (default 9007)
- Verify firewall allows localhost:9007
- Check desktop app logs

---

## Scenario 3: Automatic Page Scan (Safe Site)

### Steps:
1. Ensure extension is connected
2. Navigate to https://www.google.com
3. Wait for scan to complete (2-5 seconds)
4. Check extension icon
5. Open popup to see details

### Expected Results:
- ✅ Icon shows loading animation during scan
- ✅ Icon turns green after scan completes
- ✅ Popup displays:
  - Risk Score: 90-100 (green)
  - Risk Label: "Safe"
  - Domain: google.com
  - Source: "Auto Scan"
- ✅ No warning banner on page
- ✅ Feedback buttons visible (Correct/Incorrect)

---

## Scenario 4: Automatic Page Scan (Warning - Medium Risk)

### Steps:
1. Navigate to a flagged test URL (score 50-79)
2. Wait for scan to complete
3. Check page for warning banner
4. Check extension icon and popup

### Expected Results:
- ✅ Icon shows yellow/orange (warning)
- ✅ Warning banner appears at top of page:
  - ⚠️ Warning icon
  - Risk score displayed
  - Risk types listed (e.g., "Phishing")
  - "Continue Anyway" button
  - "Go Back" button
- ✅ Popup shows warning details
- ✅ Popup displays risk score 50-79 (orange/yellow)

### User Actions:
1. **Test "Go Back"**: Should navigate to previous page
2. **Test "Continue Anyway"**: Warning banner should disappear

---

## Scenario 5: Automatic Page Scan (Blocked - High Risk)

### Steps:
1. Navigate to a known malicious test URL (score < 50)
2. Wait for scan to complete

### Expected Results:
- ✅ Icon shows red (danger)
- ✅ Page is blocked with full-screen overlay:
  - 🛑 Block icon/symbol
  - "This site has been blocked" message
  - Risk score displayed
  - Risk types listed
  - "Go Back to Safety" button
  - "I understand the risks, proceed" button (if enabled)
- ✅ Browser notification appears: "Dangerous site blocked"
- ✅ Popup shows danger status (red)

### User Actions:
1. **Test "Go Back to Safety"**: Should navigate away
2. **Test bypass** (if enabled): Should remove block and allow access

---

## Scenario 6: Manual Scan from Popup

### Steps:
1. Navigate to any website
2. Open extension popup
3. Click "Scan Now" button
4. Wait for scan to complete

### Expected Results:
- ✅ "Scan Now" button shows loading state
- ✅ Scan completes within 5 seconds
- ✅ Results update in popup
- ✅ Icon updates based on result
- ✅ Cache is updated with new result

---

## Scenario 7: Cache Functionality

### Steps:
1. Navigate to a test URL (first visit)
2. Note scan time
3. Navigate away
4. Return to same URL
5. Check console for "Using cached result" message

### Expected Results:
- ✅ First scan takes 2-5 seconds
- ✅ Second scan is instant (< 100ms)
- ✅ Console shows cache hit message
- ✅ Results are identical
- ✅ Source shows "Cache" in popup

### Cache Expiration Test:
1. Manually modify cache timestamp to 25+ hours old
2. Revisit URL
3. Should trigger new scan (not use cache)

---

## Scenario 8: Tracker Detection

### Steps:
1. Navigate to a website with known trackers (e.g., news site)
2. Wait for scan to complete
3. Open popup
4. Check for tracker information

### Expected Results:
- ✅ Facebook Pixel IDs displayed (if present)
- ✅ Google Analytics IDs displayed (if present)
- ✅ Tracker count accurate
- ✅ Console logs tracker detection

---

## Scenario 9: Connection Loss & Reconnection

### Steps:
1. Extension connected and working
2. Stop desktop app
3. Wait 10 seconds
4. Check extension status
5. Restart desktop app
6. Wait for reconnection

### Expected Results:
- ✅ Badge turns red (disconnected) within 5 seconds
- ✅ Popup shows "Disconnected" status
- ✅ "Reconnect" button appears
- ✅ Extension attempts auto-reconnect every 30 seconds
- ✅ When app restarts, auto-reconnection succeeds
- ✅ Badge turns green again
- ✅ Console shows reconnection attempts

### Manual Reconnect:
1. Click "Reconnect" button in popup
2. Should attempt immediate reconnection

---

## Scenario 10: User Feedback Submission

### Steps:
1. Navigate to a scanned page
2. Open popup
3. Click "✓ Correct" or "✗ Incorrect" button
4. Check feedback submission

### Expected Results:
- ✅ Button changes to "Feedback sent" with checkmark
- ✅ Button becomes disabled
- ✅ Feedback sent to backend via WebSocket
- ✅ Console shows feedback submission
- ✅ Feedback persisted (doesn't reset on page refresh)

---

## Scenario 11: Remote Access Tool Detection

### Steps:
1. Open TeamViewer/AnyDesk/Remote Desktop
2. Navigate to any website while remote tool is running
3. Check for remote access warning

### Expected Results:
- ✅ Warning banner appears: "Remote Access Detected"
- ✅ Tool name displayed (e.g., "TeamViewer")
- ✅ Warning includes security advice
- ✅ "I understand" button to dismiss
- ✅ Warning can be dismissed but persists on reload

---

## Scenario 12: Multi-Tab Functionality

### Steps:
1. Open multiple tabs
2. Navigate to different URLs in each tab
3. Each tab should be scanned independently
4. Switch between tabs and check popup

### Expected Results:
- ✅ Each tab icon shows correct status
- ✅ Popup displays info for active tab only
- ✅ Switching tabs updates popup instantly
- ✅ Cache works independently per URL
- ✅ No interference between tabs

---

## Scenario 13: Settings Persistence

### Steps:
1. Configure extension settings (if any)
2. Close browser completely
3. Restart browser
4. Check settings

### Expected Results:
- ✅ Settings persist across browser restarts
- ✅ Connection preferences saved
- ✅ Cache survives restart
- ✅ User email persists

---

## Scenario 14: Content Script Injection

### Steps:
1. Navigate to various types of pages:
   - Regular website
   - HTTPS site
   - Local file (file:///)
   - Chrome settings (chrome://settings)
2. Check console for content script load

### Expected Results:
- ✅ Content script loads on regular websites
- ✅ Content script loads on HTTPS sites
- ✅ Content script gracefully fails on restricted pages
- ✅ No errors in console on restricted pages

---

## Scenario 15: Performance & Resource Usage

### Steps:
1. Open browser task manager (Shift+Esc in Chrome)
2. Navigate to multiple websites
3. Monitor extension resource usage
4. Check for memory leaks

### Expected Results:
- ✅ Extension memory usage < 50MB
- ✅ CPU usage minimal when idle
- ✅ No memory leaks over time
- ✅ Page load time impact < 200ms
- ✅ No visible lag or slowdown

---

## Browser-Specific Testing

### Chrome
- Test on Chrome Stable (latest)
- Test on Chrome Beta
- Verify manifest V3 compatibility

### Edge
- Test on latest Edge version
- Verify all features work identically to Chrome

### Firefox
- Convert manifest to Firefox format if needed
- Test WebExtensions API compatibility
- Check for Firefox-specific console errors

### Comet (if available)
- Test basic functionality
- Document any incompatibilities

---

## Automated E2E Testing (Future)

Recommended tools:
- **Selenium WebDriver** for cross-browser testing
- **Puppeteer** for Chrome-specific tests
- **Playwright** for multi-browser support

Example test framework structure:
```javascript
// e2e/automated/safe-site.spec.js
describe('Safe Site Scan', () => {
  it('should show green icon for safe site', async () => {
    await browser.url('https://www.google.com');
    await browser.pause(5000);
    const icon = await browser.$('.extension-icon');
    const color = await icon.getCSSProperty('color');
    expect(color.value).toBe('rgb(34, 197, 94)'); // Green
  });
});
```

---

## Test Coverage Checklist

- [x] Installation & setup
- [x] Connection management
- [x] Automatic scanning
- [x] Manual scanning
- [x] Cache functionality
- [x] Warning display
- [x] Page blocking
- [x] User feedback
- [x] Tracker detection
- [x] Remote access warnings
- [x] Multi-tab support
- [x] Performance
- [x] Cross-browser compatibility

---

## Known Issues & Limitations

Document any known issues here during testing:

1. 
2. 
3. 

---

## Test Sign-Off

| Browser | Version | Tester | Date | Pass/Fail | Notes |
|---------|---------|--------|------|-----------|-------|
| Chrome  |         |        |      |           |       |
| Edge    |         |        |      |           |       |
| Firefox |         |        |      |           |       |
| Comet   |         |        |      |           |       |

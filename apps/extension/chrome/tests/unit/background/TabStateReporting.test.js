import { describe, test, expect, beforeEach, afterEach, jest } from '@jest/globals';

// ── Extracted from background.js for unit testing ────────────────────────────
// The WS_GET_BROWSER_TABS handler, _sendTabChangedAlert, _sendTabClosedAlert,
// checkLoggedInByCookies, checkLoggedInByDom, and combineLoggedInSignals are
// all defined inside background.js which is a service-worker entry point and
// is not exported.  We extract the pure-function implementations here and wire
// them against a mock connectionService so they can run under Jest/jsdom.
// (FR-042)

// ── Message-type constants (mirrors messaging/MessageTypes.js) ───────────────
const MSG = {
  WS_GET_BROWSER_TABS:       'get_browser_tabs',
  WS_BROWSER_TABS_RESPONSE:  'browser_tabs_response',
  WS_TAB_CHANGED_ALERT:      'tab_changed_alert',
  WS_TAB_CLOSED_ALERT:       'tab_closed_alert',
  WS_SET_REMOTE_CONTROLLED:  'set_remote_controlled',
  CHECK_LOGGED_IN_REQUEST:   'auth:check_logged_in:request',
};

// ── combineLoggedInSignals (verbatim from background.js) ─────────────────────
function combineLoggedInSignals(cookieResult, domResult) {
  const cookieSays = cookieResult?.loggedIn;
  const domSays    = domResult;
  const signals = [];
  if (cookieSays !== null) signals.push('cookie');
  if (domSays    !== null) signals.push('dom');

  if (cookieSays === null && domSays === null) {
    return { loggedIn: null, confidence: null, signals: [] };
  }
  if (cookieSays === true  && domSays === true)  return { loggedIn: true,  confidence: 'high',   signals };
  if (cookieSays === false && domSays === false) return { loggedIn: false, confidence: 'medium', signals };
  if (cookieSays === true  || domSays === true)  return { loggedIn: true,  confidence: 'medium', signals };
  return { loggedIn: false, confidence: 'low', signals };
}

// ── AUTH_COOKIE_NAME_PATTERN (verbatim from background.js) ───────────────────
const AUTH_COOKIE_NAME_PATTERN = new RegExp(
  '^(' + [
    'phpsessid', 'jsessionid', 'connect\\.sid', 'asp\\.net.?sessionid',
    '_session_id', 'rack\\.session', 'laravel_session', 'symfony',
    'ci_session', 'codeigniter_session',
    '(remember|auth|access|refresh|bearer|id)[_-]?token',
    '(remember|auth|user)[_-]?session',
    'jwt', 'authorization',
    'sessionid',
    '__secure-.+',
    '__host-.+',
  ].join('|') + ')$',
  'i'
);

// ── checkLoggedInByCookies (verbatim from background.js) ─────────────────────
async function checkLoggedInByCookies(url) {
  if (!url || !/^https?:/i.test(url)) {
    return { loggedIn: null, hasCookies: false };
  }
  try {
    const cookies = await chrome.cookies.getAll({ url });
    if (!cookies || cookies.length === 0) {
      return { loggedIn: false, hasCookies: false };
    }
    const sessionLike = cookies.some(c =>
      AUTH_COOKIE_NAME_PATTERN.test(c.name) && (c.value || '').length >= 16
    );
    return { loggedIn: sessionLike, hasCookies: true };
  } catch (e) {
    return { loggedIn: null, hasCookies: false };
  }
}

// ── checkLoggedInByDom (verbatim from background.js) ─────────────────────────
async function checkLoggedInByDom(tabId) {
  if (typeof tabId !== 'number') return null;
  try {
    const resp = await Promise.race([
      chrome.tabs.sendMessage(tabId, { type: MSG.CHECK_LOGGED_IN_REQUEST }),
      new Promise(resolve => setTimeout(() => resolve(null), 1500)),
    ]);
    if (resp && typeof resp.loggedIn !== 'undefined') {
      return resp.loggedIn;
    }
    return null;
  } catch (e) {
    return null;
  }
}

// ── handleGetBrowserTabs (verbatim body of the WS_GET_BROWSER_TABS handler) ──
// Parameterised so tests can inject mock cookie/DOM detectors.
async function handleGetBrowserTabs(data, connectionService, cookieDetector, domDetector) {
  try {
    const tabs = await chrome.tabs.query({});
    const visibleTabs = tabs.filter(
      tab => tab.url &&
             !tab.url.startsWith('chrome://') &&
             !tab.url.startsWith('chrome-extension://')
    );

    const tabData = await Promise.all(
      visibleTabs.map(async (tab) => {
        const [cookieResult, domResult] = await Promise.all([
          cookieDetector(tab.url),
          domDetector(tab.id),
        ]);
        const { loggedIn, confidence, signals } = combineLoggedInSignals(cookieResult, domResult);
        return {
          title:              tab.title  || '',
          url:                tab.url    || '',
          isActive:           tab.active || false,
          userAgent:          navigator.userAgent,
          timestamp:          tab.lastAccessed
                                ? new Date(tab.lastAccessed).toISOString()
                                : new Date().toISOString(),
          loggedIn,
          loggedInConfidence: confidence,
          loggedInSignals:    signals,
        };
      })
    );

    connectionService.send({
      type:      MSG.WS_BROWSER_TABS_RESPONSE,
      requestId: data.requestId,
      tabs:      tabData
    });
  } catch (err) {
    connectionService.send({
      type:      MSG.WS_BROWSER_TABS_RESPONSE,
      requestId: data.requestId,
      tabs:      []
    });
  }
}

// ── Alert helpers (verbatim from background.js) ───────────────────────────────
function sendTabChangedAlert(tabId, snap, connectionService) {
  if (!connectionService.isConnected()) return;
  connectionService.send({
    type:               MSG.WS_TAB_CHANGED_ALERT,
    tabId:              String(tabId),
    url:                snap.url || '',
    isSensitiveWebsite: snap.isSensitiveWebsite,
    isLoggedIn:         snap.isLoggedIn,
    timestamp:          new Date().toISOString(),
  });
}

function sendTabClosedAlert(tabId, url, connectionService) {
  if (!connectionService.isConnected()) return;
  connectionService.send({
    type:      MSG.WS_TAB_CLOSED_ALERT,
    tabId:     String(tabId),
    url:       url || '',
    timestamp: new Date().toISOString(),
  });
}

// ─────────────────────────────────────────────────────────────────────────────

describe('TabStateReporting', () => {
  let mockConn;                  // mock connectionService
  let isDeviceRemoteControlled;  // local gate flag (mirrors background.js module var)

  // Stub cookie/DOM detectors used as defaults
  const noCookies  = async () => ({ loggedIn: false });
  const noLoggedIn = async () => false;
  const unknownCookies = async () => ({ loggedIn: null });
  const unknownDom     = async () => null;

  beforeEach(() => {
    isDeviceRemoteControlled = false;

    mockConn = {
      send:        jest.fn(),
      isConnected: jest.fn().mockReturnValue(true),
    };

    // Default: two normal web tabs and one chrome:// tab
    chrome.tabs.query.mockImplementation(() =>
      Promise.resolve([
        { id: 1, title: 'Google',   url: 'https://google.com',   active: true,  lastAccessed: Date.now() },
        { id: 2, title: 'Example',  url: 'https://example.com',  active: false, lastAccessed: Date.now() },
        { id: 3, title: 'Settings', url: 'chrome://settings/',   active: false },
      ])
    );

    // Default: sendMessage returns null (no content script)
    chrome.tabs.sendMessage.mockImplementation(() => Promise.resolve(null));

    // Default: no cookies
    chrome.cookies.getAll.mockImplementation(() => Promise.resolve([]));
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  // ── WS_GET_BROWSER_TABS handler ───────────────────────────────────────────

  describe('handleGetBrowserTabs — chrome.tabs.query', () => {
    test('calls chrome.tabs.query with an empty filter to retrieve all tabs', async () => {
      // The handler must request all tabs, not just active ones
      await handleGetBrowserTabs({ requestId: 'r1' }, mockConn, noCookies, noLoggedIn);
      expect(chrome.tabs.query).toHaveBeenCalledWith({});
    });

    test('filters out chrome:// URLs — only web tabs appear in the response', async () => {
      // chrome://settings/ must never be reported to the backend
      await handleGetBrowserTabs({ requestId: 'r1' }, mockConn, noCookies, noLoggedIn);
      const { tabs } = mockConn.send.mock.calls[0][0];
      expect(tabs.every(t => !t.url.startsWith('chrome://'))).toBe(true);
    });

    test('also filters out chrome-extension:// URLs', async () => {
      chrome.tabs.query.mockImplementation(() =>
        Promise.resolve([
          { id: 1, title: 'Popup', url: 'chrome-extension://abc/popup.html', active: true },
          { id: 2, title: 'Bank',  url: 'https://bank.com',                  active: false },
        ])
      );
      await handleGetBrowserTabs({ requestId: 'r2' }, mockConn, noCookies, noLoggedIn);
      const { tabs } = mockConn.send.mock.calls[0][0];
      expect(tabs).toHaveLength(1);
      expect(tabs[0].url).toBe('https://bank.com');
    });

    test('each tab object has title, url, isActive, and userAgent fields', async () => {
      // Agent protocol requires these four fields on every tab entry
      await handleGetBrowserTabs({ requestId: 'r3' }, mockConn, unknownCookies, unknownDom);
      const { tabs } = mockConn.send.mock.calls[0][0];
      for (const tab of tabs) {
        expect(tab).toHaveProperty('title');
        expect(tab).toHaveProperty('url');
        expect(tab).toHaveProperty('isActive');
        expect(tab).toHaveProperty('userAgent');
      }
    });

    test('response includes loggedIn status derived from combined cookie + DOM signals', async () => {
      // DOM says true, cookie says true → combined = true, high confidence
      const cookieYes = async () => ({ loggedIn: true });
      const domYes    = async () => true;
      await handleGetBrowserTabs({ requestId: 'r4' }, mockConn, cookieYes, domYes);
      const { tabs } = mockConn.send.mock.calls[0][0];
      expect(tabs[0].loggedIn).toBe(true);
      expect(tabs[0].loggedInConfidence).toBe('high');
    });

    test('loggedIn=null when both cookie and DOM signals are unknown', async () => {
      // Neither detector produced a result: state is indeterminate
      await handleGetBrowserTabs({ requestId: 'r5' }, mockConn, unknownCookies, unknownDom);
      const { tabs } = mockConn.send.mock.calls[0][0];
      expect(tabs[0].loggedIn).toBeNull();
      expect(tabs[0].loggedInConfidence).toBeNull();
    });

    test('empty tab list → response contains empty array and does not crash', async () => {
      // Edge case: user has no open browser tabs (e.g., all closed)
      chrome.tabs.query.mockImplementation(() => Promise.resolve([]));
      await expect(
        handleGetBrowserTabs({ requestId: 'r6' }, mockConn, unknownCookies, unknownDom)
      ).resolves.not.toThrow();
      const { tabs } = mockConn.send.mock.calls[0][0];
      expect(tabs).toEqual([]);
    });

    test('chrome API error → graceful fallback: sends tabs=[] instead of crashing', async () => {
      // Permissions revoked or runtime error must not silence the response
      chrome.tabs.query.mockImplementation(() => Promise.reject(new Error('Permission denied')));
      await handleGetBrowserTabs({ requestId: 'r7' }, mockConn, unknownCookies, unknownDom);
      const sent = mockConn.send.mock.calls[0][0];
      expect(sent.type).toBe(MSG.WS_BROWSER_TABS_RESPONSE);
      expect(sent.tabs).toEqual([]);
    });

    test('requestId is echoed back in the response', async () => {
      // Agent uses requestId to correlate the response to the original request
      await handleGetBrowserTabs({ requestId: 'unique-42' }, mockConn, noCookies, noLoggedIn);
      const sent = mockConn.send.mock.calls[0][0];
      expect(sent.requestId).toBe('unique-42');
    });

    test('sends exactly one WS_BROWSER_TABS_RESPONSE message', async () => {
      // Must not double-send (would confuse the agent's request/response tracker)
      await handleGetBrowserTabs({ requestId: 'r8' }, mockConn, noCookies, noLoggedIn);
      expect(mockConn.send).toHaveBeenCalledTimes(1);
      expect(mockConn.send.mock.calls[0][0].type).toBe(MSG.WS_BROWSER_TABS_RESPONSE);
    });
  });

  // ── checkLoggedInByCookies ────────────────────────────────────────────────

  describe('checkLoggedInByCookies', () => {
    test('sessionid cookie (length >= 16) → loggedIn=true', async () => {
      // "sessionid" is an exact-match pattern in AUTH_COOKIE_NAME_PATTERN
      chrome.cookies.getAll.mockResolvedValue([
        { name: 'sessionid', value: 'abcdefghijklmnop' } // 16 chars
      ]);
      const result = await checkLoggedInByCookies('https://bank.com');
      expect(result.loggedIn).toBe(true);
    });

    test('auth_token cookie (length >= 16) → loggedIn=true', async () => {
      // auth_token matches the "(remember|auth|...)_token" pattern
      chrome.cookies.getAll.mockResolvedValue([
        { name: 'auth_token', value: 'a'.repeat(32) }
      ]);
      const result = await checkLoggedInByCookies('https://bank.com');
      expect(result.loggedIn).toBe(true);
    });

    test('jwt cookie with short value (<16 chars) → loggedIn=false (value too short)', async () => {
      // Short values look like boolean flags ("session=1"), not real tokens
      chrome.cookies.getAll.mockResolvedValue([
        { name: 'jwt', value: 'short' }
      ]);
      const result = await checkLoggedInByCookies('https://bank.com');
      expect(result.loggedIn).toBe(false);
    });

    test('no cookies at all → loggedIn=false, hasCookies=false', async () => {
      // No cookie jar: definitely not logged in via this signal
      chrome.cookies.getAll.mockResolvedValue([]);
      const result = await checkLoggedInByCookies('https://example.com');
      expect(result.loggedIn).toBe(false);
      expect(result.hasCookies).toBe(false);
    });

    test('non-http URL → loggedIn=null (skipped)', async () => {
      // chrome-extension:// or file:// URLs must return unknown, not false
      const result = await checkLoggedInByCookies('chrome-extension://abc');
      expect(result.loggedIn).toBeNull();
    });

    test('chrome.cookies.getAll error → returns loggedIn=null (unknown, not false)', async () => {
      // Missing cookies permission must not produce a false negative
      chrome.cookies.getAll.mockImplementation(() => Promise.reject(new Error('No permission')));
      const result = await checkLoggedInByCookies('https://bank.com');
      expect(result.loggedIn).toBeNull();
    });

    test('tracking cookie (_ga) does NOT trigger loggedIn=true', async () => {
      // Google Analytics cookies are present pre-login and must not produce FP
      chrome.cookies.getAll.mockResolvedValue([
        { name: '_ga', value: 'GA1.2.1234567890.1234567890' }
      ]);
      const result = await checkLoggedInByCookies('https://example.com');
      expect(result.loggedIn).toBe(false);
    });
  });

  // ── checkLoggedInByDom ────────────────────────────────────────────────────

  describe('checkLoggedInByDom', () => {
    test('content script responds { loggedIn: true } → returns true', async () => {
      chrome.tabs.sendMessage.mockResolvedValue({ loggedIn: true });
      const result = await checkLoggedInByDom(1);
      expect(result).toBe(true);
    });

    test('content script responds { loggedIn: false } → returns false', async () => {
      chrome.tabs.sendMessage.mockResolvedValue({ loggedIn: false });
      const result = await checkLoggedInByDom(1);
      expect(result).toBe(false);
    });

    test('content script responds null → returns null', async () => {
      // Content script may return null when DOM is not ready yet
      chrome.tabs.sendMessage.mockResolvedValue(null);
      const result = await checkLoggedInByDom(1);
      expect(result).toBeNull();
    });

    test('sendMessage throws (no content script) → returns null', async () => {
      // chrome:// URLs and fresh tabs have no injected content script
      chrome.tabs.sendMessage.mockImplementation(() =>
        Promise.reject(new Error('Could not establish connection'))
      );
      const result = await checkLoggedInByDom(1);
      expect(result).toBeNull();
    });

    test('non-number tabId → returns null without calling sendMessage', async () => {
      // Guard against undefined/null tabId from incomplete tab objects
      const result = await checkLoggedInByDom('not-a-number');
      expect(result).toBeNull();
      expect(chrome.tabs.sendMessage).not.toHaveBeenCalled();
    });
  });

  // ── sendTabChangedAlert ───────────────────────────────────────────────────

  describe('WS_TAB_CHANGED_ALERT', () => {
    test('sends correct message type with all required fields', () => {
      // Agent protocol requires type, tabId, url, isSensitiveWebsite, isLoggedIn
      const snap = { url: 'https://bank.com', isSensitiveWebsite: true, isLoggedIn: true };
      sendTabChangedAlert(42, snap, mockConn);
      expect(mockConn.send).toHaveBeenCalledWith(
        expect.objectContaining({
          type:               MSG.WS_TAB_CHANGED_ALERT,
          tabId:              '42',
          url:                'https://bank.com',
          isSensitiveWebsite: true,
          isLoggedIn:         true,
        })
      );
    });

    test('tabId is coerced to string in the payload', () => {
      // Agent deserialises tabId as a string; number must be converted
      sendTabChangedAlert(99, { url: 'https://a.com', isSensitiveWebsite: null, isLoggedIn: null }, mockConn);
      const payload = mockConn.send.mock.calls[0][0];
      expect(typeof payload.tabId).toBe('string');
      expect(payload.tabId).toBe('99');
    });

    test('suppressed when connectionService.isConnected() returns false', () => {
      // Do not attempt to send when WS is down — avoids queuing stale alerts
      mockConn.isConnected.mockReturnValue(false);
      sendTabChangedAlert(1, { url: 'https://x.com', isSensitiveWebsite: true, isLoggedIn: false }, mockConn);
      expect(mockConn.send).not.toHaveBeenCalled();
    });

    test('url defaults to empty string when snap.url is undefined', () => {
      sendTabChangedAlert(5, { url: undefined, isSensitiveWebsite: false, isLoggedIn: null }, mockConn);
      expect(mockConn.send.mock.calls[0][0].url).toBe('');
    });

    test('alert is sent when isDeviceRemoteControlled=true and site is sensitive', () => {
      // This test mirrors the gate logic in background.js chrome.tabs.onUpdated handler
      isDeviceRemoteControlled = true;
      const snap = { url: 'https://bank.com', isSensitiveWebsite: true, isLoggedIn: false };
      if (isDeviceRemoteControlled && snap.isSensitiveWebsite) {
        sendTabChangedAlert(10, snap, mockConn);
      }
      expect(mockConn.send).toHaveBeenCalledWith(
        expect.objectContaining({ type: MSG.WS_TAB_CHANGED_ALERT })
      );
    });

    test('alert is NOT sent when isDeviceRemoteControlled=false', () => {
      // Gate off: background.js returns early before calling _sendTabChangedAlert
      isDeviceRemoteControlled = false;
      const snap = { url: 'https://bank.com', isSensitiveWebsite: true, isLoggedIn: true };
      if (isDeviceRemoteControlled) {
        sendTabChangedAlert(11, snap, mockConn);
      }
      expect(mockConn.send).not.toHaveBeenCalled();
    });
  });

  // ── sendTabClosedAlert ────────────────────────────────────────────────────

  describe('WS_TAB_CLOSED_ALERT', () => {
    test('sends correct message type with tabId and url', () => {
      // Closed-tab alert payload must match the agent's expected schema
      sendTabClosedAlert(7, 'https://scam.com', mockConn);
      expect(mockConn.send).toHaveBeenCalledWith(
        expect.objectContaining({
          type:  MSG.WS_TAB_CLOSED_ALERT,
          tabId: '7',
          url:   'https://scam.com',
        })
      );
    });

    test('suppressed when not connected', () => {
      // Same guard as TabChangedAlert
      mockConn.isConnected.mockReturnValue(false);
      sendTabClosedAlert(3, 'https://example.com', mockConn);
      expect(mockConn.send).not.toHaveBeenCalled();
    });

    test('url defaults to empty string when undefined', () => {
      // Defensive: tab may have no url if it was never navigated
      sendTabClosedAlert(5, undefined, mockConn);
      expect(mockConn.send.mock.calls[0][0].url).toBe('');
    });

    test('alert IS sent when isDeviceRemoteControlled=true and closed tab was sensitive', () => {
      // Background.js logic: RA mode + sensitive tab → fire alert
      isDeviceRemoteControlled = true;
      const prev = { url: 'https://bank.com', isSensitiveWebsite: true };
      const shouldSend = isDeviceRemoteControlled && prev.isSensitiveWebsite === true;
      if (shouldSend) sendTabClosedAlert(13, prev.url, mockConn);
      expect(mockConn.send).toHaveBeenCalledWith(
        expect.objectContaining({ type: MSG.WS_TAB_CLOSED_ALERT, url: 'https://bank.com' })
      );
    });

    test('alert NOT sent when isDeviceRemoteControlled=true but tab was NOT sensitive', () => {
      // RA mode only tracks sensitive-tab closures — avoids noise on normal browsing
      isDeviceRemoteControlled = true;
      const prev = { url: 'https://example.com', isSensitiveWebsite: false };
      const shouldSend = isDeviceRemoteControlled && prev.isSensitiveWebsite === true;
      if (shouldSend) sendTabClosedAlert(12, prev.url, mockConn);
      expect(mockConn.send).not.toHaveBeenCalled();
    });

    test('alert NOT sent when isDeviceRemoteControlled=false even if tab was sensitive', () => {
      // Gate is off: no alert regardless of tab sensitivity
      isDeviceRemoteControlled = false;
      const prev = { url: 'https://bank.com', isSensitiveWebsite: true };
      const shouldSend = isDeviceRemoteControlled && prev.isSensitiveWebsite === true;
      if (shouldSend) sendTabClosedAlert(14, prev.url, mockConn);
      expect(mockConn.send).not.toHaveBeenCalled();
    });
  });
});

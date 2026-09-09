import { describe, test, expect, beforeEach, afterEach, jest } from '@jest/globals';

// ============================================================================
// ASPS-755 rewrite.
//
// The previous version of this file never imported background.js and never
// touched the real ConnectionService/MessageBus singletons it wires up. Every
// assertion instead checked `chrome.*` jest-chrome mock call counts that were
// only ever populated by OTHER, unrelated tests running earlier in the same
// file (test-ordering coupling) — the suite passed or failed depending on
// execution order, not on anything background.js actually does.
//
// This rewrite imports the REAL background.js module for every test. Because
// background.js's module-level `init()` (bottom of the file) is what actually
// calls setupWebSocketHandlers()/setupMessageHandlers()/setupTabListeners()/
// setupStateListeners() — registering handlers on the real connectionService
// and messageBus singletons — importing it is the only way to exercise the
// genuine wiring instead of a hand-copied stand-in.
//
// Strategy per test:
//   1. jest.resetModules() + clear all chrome.* Event listener registries so
//      each test gets a brand-new set of singleton service instances AND a
//      clean chrome.tabs.onUpdated/onCreated/... listener set (this is what
//      the old suite was missing — chrome event listeners are a persistent,
//      file-scoped Set() inside jest-chrome, NOT reset by resetModules()).
//   2. Import the real singleton services (ConnectionService, MessageBus,
//      StateManager, CacheService, IconService, MessageQueueService,
//      TrackingService, ScanService) from the SAME module graph background.js
//      resolves, so assertions observe genuine production state.
//   3. Stub only connectionService.connect() — the actual WebSocket handshake
//      is ConnectionService's own contract, already covered by
//      tests/unit/services/ConnectionService.test.js. Stubbing it here keeps
//      this suite fast/deterministic while every handler under test
//      (setupWebSocketHandlers/setupMessageHandlers/tab listeners) still runs
//      for real against the real singletons.
//   4. Import background.js LAST so its init() wires the real handlers, then
//      drive messages through connectionService.handleMessage(...) (exactly
//      what ws.onmessage does) and messageBus.handleMessage(...) (exactly
//      what chrome.runtime.onMessage does), and dispatch real chrome tab
//      events via chrome.tabs.onUpdated.callListeners(...).
//
// One behavior is deliberately NOT exercised end-to-end: the real
// WebSocket port-scan handshake in ConnectionService.connect() (tryPort/
// setupConnection). Driving that for real would mean either a genuine 2s
// per-port timeout (as tests/unit/services/ConnectionService.test.js already
// accepts for its own narrower scope) or reimplementing a fake WebSocket
// transport — neither adds coverage over ConnectionService's own suite, and
// doing it here would re-introduce slow/flaky tests for a concern this file
// isn't about. See individual test comments for any other real Chrome API
// surface that isn't asserted (e.g. the 30s scan-timeout fallback branch in
// triggerScan(), which is bypassed by stubbing scanService.scan() in the
// auto-scan test rather than left dangling as a real 30s timer).
// ============================================================================

function clearChromeListeners() {
  chrome.alarms.onAlarm.clearListeners();
  chrome.tabs.onUpdated.clearListeners();
  chrome.tabs.onCreated.clearListeners();
  chrome.tabs.onRemoved.clearListeners();
  chrome.tabs.onActivated.clearListeners();
  chrome.webNavigation.onCompleted.clearListeners();
  chrome.webNavigation.onHistoryStateUpdated.clearListeners();
  chrome.cookies.onChanged.clearListeners();
  chrome.runtime.onMessage.clearListeners();
}

// Lets pending microtask chains inside async handlers (which are invoked
// fire-and-forget by ConnectionService.handleMessage/chrome Event dispatch)
// finish before assertions run.
async function flushAsync() {
  for (let i = 0; i < 3; i++) {
    await Promise.resolve();
  }
  await new Promise((resolve) => setTimeout(resolve, 0));
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe('Extension Integration Flow — background.js real wiring (ASPS-755)', () => {
  let connectionService;
  let messageBus;
  let MSG;
  let stateManager;
  let cacheService;
  let iconService;
  let messageQueueService;
  let trackingService;
  let scanService;

  beforeEach(async () => {
    jest.clearAllMocks();
    clearChromeListeners();
    jest.resetModules();

    // background.js reads chrome.runtime.getManifest() at module top level
    // (outside any function) — must be mocked BEFORE the dynamic import below
    // or the import itself throws.
    chrome.runtime.getManifest.mockReturnValue({ version: '9.9.9-test' });

    // jest-chrome 0.8.0 predates MV3's chrome.action (it only ships the MV2
    // chrome.browserAction) — same gap that quarantines
    // tests/unit/services/IconService.test.js (29 expected failures in
    // scripts/test-baseline-exceptions.json, untouched by this task). Polyfill
    // it locally so IconService's real setColor()/drawLoadingFrame() calls
    // (exercised for real by background.js's init()/handlers below) don't
    // throw. Scoped to this file only — does not affect IconService.test.js.
    if (!chrome.action) {
      chrome.action = {
        setIcon: jest.fn(),
        setBadgeText: jest.fn(),
        setBadgeBackgroundColor: jest.fn()
      };
    }

    // Realistic MV3 promise-based chrome.* defaults. Individual tests override
    // as needed.
    chrome.storage.local.get.mockImplementation(() => Promise.resolve({}));
    chrome.storage.local.set.mockImplementation(() => Promise.resolve());
    chrome.storage.local.remove.mockImplementation(() => Promise.resolve());
    chrome.storage.session.get.mockImplementation(() => Promise.resolve({}));
    chrome.storage.session.set.mockImplementation(() => Promise.resolve());
    chrome.storage.session.remove.mockImplementation(() => Promise.resolve());
    chrome.tabs.query.mockImplementation(() => Promise.resolve([]));
    chrome.tabs.get.mockImplementation(() => Promise.reject(new Error('no such tab')));
    chrome.tabs.sendMessage.mockImplementation(() => Promise.resolve(undefined));
    chrome.cookies.getAll.mockImplementation(() => Promise.resolve([]));
    chrome.action.setIcon.mockImplementation(() => Promise.resolve());
    chrome.action.setBadgeText.mockImplementation(() => Promise.resolve());
    chrome.action.setBadgeBackgroundColor.mockImplementation(() => Promise.resolve());
    chrome.notifications.create.mockImplementation(() => Promise.resolve('notif-id'));
    chrome.alarms.create.mockImplementation(() => Promise.resolve());
    chrome.alarms.clear.mockImplementation(() => Promise.resolve(true));

    // OffscreenCanvas doesn't exist in jsdom. IconService's real setColor()/
    // drawLoadingFrame() run during background.js init() (iconService.update())
    // and during several handlers under test, so this must be mocked (same
    // shape as tests/unit/services/IconService.test.js).
    global.OffscreenCanvas = jest.fn(() => ({
      getContext: jest.fn(() => ({
        clearRect: jest.fn(),
        beginPath: jest.fn(),
        moveTo: jest.fn(),
        lineTo: jest.fn(),
        quadraticCurveTo: jest.fn(),
        closePath: jest.fn(),
        fill: jest.fn(),
        stroke: jest.fn(),
        fillText: jest.fn(),
        getImageData: jest.fn(() => ({}))
      }))
    }));

    // Import the REAL singletons from the same module graph background.js
    // uses (relative path `./services/...` from background.js resolves to
    // the identical absolute file as the `@/services/...` alias here), so
    // assertions below observe genuine production state, not re-implemented
    // doubles.
    ({ connectionService } = await import('@/services/ConnectionService.js'));
    ({ messageBus } = await import('@/messaging/index.js'));
    ({ MSG } = await import('@/messaging/index.js'));
    ({ stateManager } = await import('@/state/StateManager.js'));
    ({ cacheService } = await import('@/services/CacheService.js'));
    ({ iconService } = await import('@/services/IconService.js'));
    ({ messageQueueService } = await import('@/services/MessageQueueService.js'));
    ({ trackingService } = await import('@/services/TrackingService.js'));
    ({ scanService } = await import('@/services/ScanService.js'));

    // Stub only the WebSocket handshake (see file header comment).
    jest.spyOn(connectionService, 'connect').mockResolvedValue(true);

    // Import background.js LAST — its top-level init() wires every handler
    // under test onto the real connectionService/messageBus singletons above.
    await import('@/background.js');
    await flushAsync();
  });

  afterEach(() => {
    // background.js's loading animation (IconService) uses a real
    // setInterval/setTimeout; some scenarios (e.g. a scan that never
    // resolves because the desktop agent is disconnected) leave it running.
    // Same cleanup as tests/unit/services/IconService.test.js.
    if (iconService?.animationInterval) clearInterval(iconService.animationInterval);
    if (iconService?.loadingBadgeTimeout) clearTimeout(iconService.loadingBadgeTimeout);
  });

  // ── Initialization wiring ──────────────────────────────────────────────

  test('init() registers the real chrome event listeners and message handlers', () => {
    expect(connectionService.connect).toHaveBeenCalledTimes(1);
    expect(chrome.runtime.onMessage.hasListeners()).toBe(true);
    // Two independent onUpdated listeners are registered: the auto-scan one
    // (setupTabListeners) and the TabAlertService sensitive-domain one.
    expect(chrome.tabs.onUpdated.getListeners().size).toBe(2);
    expect(chrome.tabs.onCreated.hasListeners()).toBe(true);
    expect(chrome.tabs.onRemoved.hasListeners()).toBe(true);
    expect(chrome.tabs.onActivated.hasListeners()).toBe(true);
    expect(chrome.alarms.onAlarm.hasListeners()).toBe(true);
    expect(chrome.cookies.onChanged.hasListeners()).toBe(true);
    expect(chrome.webNavigation.onCompleted.hasListeners()).toBe(true);
    expect(chrome.webNavigation.onHistoryStateUpdated.hasListeners()).toBe(true);
  });

  // ── WebSocket message handlers (setupWebSocketHandlers) ────────────────

  test('WS_PONG → stores the agent email and device IP via the real handler', async () => {
    connectionService.handleMessage({
      type: MSG.WS_PONG,
      email: 'agent-user@example.com',
      ipAddress: '10.0.0.7'
    });
    await flushAsync();

    expect(chrome.storage.local.set).toHaveBeenCalledWith({ userEmail: 'agent-user@example.com' });
    expect(connectionService.getDeviceIpAddress()).toBe('10.0.0.7');
  });

  test('WS_URL_RESULT → real ScanService + CacheService + IconService pipeline', () => {
    connectionService.handleMessage({
      type: MSG.WS_URL_RESULT,
      score: 85,
      riskType: [1],
      protectiveAction: 0, // NONE → IconService falls back to score-based color
      analyzing: false,
      url: 'https://scammy.example.com',
      ttl: 1200
    });

    // Real CacheService now holds the result under the URL's domain.
    expect(cacheService.get('https://scammy.example.com')).toEqual(
      expect.objectContaining({ score: 85, riskType: [1], protectiveAction: 0 })
    );
    // Real StateManager → chrome.storage.local write for the popup.
    expect(chrome.storage.local.set).toHaveBeenCalledWith(
      expect.objectContaining({
        currentPageScore: 85,
        currentPageRiskType: [1],
        currentPageAction: 0,
        currentPageScanning: false
      })
    );
    // Real IconService color decision (score >= 61 → red, since action < 2).
    expect(iconService.getColor()).toBe('red');
  });

  test('WS_NOTIFICATION → creates a real chrome.notifications.create() call', () => {
    connectionService.handleMessage({
      type: MSG.WS_NOTIFICATION,
      title: 'Danger detected',
      message: 'Scam in progress'
    });

    expect(chrome.notifications.create).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'basic',
        title: 'Danger detected',
        message: 'Scam in progress'
      })
    );
  });

  test('WS_IMMEDIATE_DANGER_STARTED / _ENDED → toggles danger state and persists it (DangerStateService → chrome.storage.session)', async () => {
    connectionService.handleMessage({ type: MSG.WS_IMMEDIATE_DANGER_STARTED });
    await flushAsync();

    expect(chrome.storage.session.set).toHaveBeenCalledWith(
      expect.objectContaining({
        dangerState: expect.objectContaining({
          immediateDangerMode: true,
          isDeviceRemoteControlled: false
        })
      })
    );

    chrome.storage.session.set.mockClear();
    connectionService.handleMessage({ type: MSG.WS_IMMEDIATE_DANGER_ENDED });
    await flushAsync();

    expect(chrome.storage.session.set).toHaveBeenCalledWith(
      expect.objectContaining({
        dangerState: expect.objectContaining({ immediateDangerMode: false })
      })
    );
  });

  test('TRACKED_DOMAINS_SET (WS) → rebuilds the real TrackingService map and pushes an update to open http tabs', async () => {
    chrome.tabs.query.mockImplementation(() => Promise.resolve([
      { id: 3, url: 'https://tracked.example.com/page' },
      { id: 4, url: 'chrome://newtab/' }
    ]));

    connectionService.handleMessage({
      type: MSG.TRACKED_DOMAINS_SET,
      domains: [{ Domain: 'tracked.example.com', TrackMode: 1 }],
      alwaysSendFormSubmits: true
    });
    await flushAsync();

    // Real TrackingService: root domain of tracked.example.com is example.com.
    expect(trackingService.getMap().has('example.com')).toBe(true);
    expect(chrome.storage.local.set).toHaveBeenCalledWith(
      expect.objectContaining({
        trackedDomains: [{ Domain: 'tracked.example.com', TrackMode: 1 }],
        alwaysSendFormSubmits: true
      })
    );
    // Only the http(s) tab gets the content-script update — chrome:// is skipped.
    expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
      3,
      expect.objectContaining({ type: MSG.TRACKED_DOMAINS_UPDATED })
    );
    expect(chrome.tabs.sendMessage).not.toHaveBeenCalledWith(4, expect.anything());
  });

  // ── chrome.runtime.onMessage handlers (setupMessageHandlers) ───────────

  test('STATUS_GET → buildStatusResponse() reflects real state written by the WS_URL_RESULT pipeline', async () => {
    connectionService.handleMessage({
      type: MSG.WS_URL_RESULT,
      score: 40,
      riskType: [],
      protectiveAction: 0,
      analyzing: false,
      url: 'https://midrisk.example.com'
    });

    let response;
    await messageBus.handleMessage({ type: MSG.STATUS_GET }, {}, (r) => { response = r; });

    expect(response).toEqual(
      expect.objectContaining({
        // ConnectionService.isConnected() is `this.websocket && ...` — with
        // no websocket ever established in this suite (see file header) the
        // short-circuit yields null, not false.
        isConnectedToDesktop: null,
        cacheSize: 1,
        currentPageScore: 40,
        currentPageRiskType: [],
        currentPageAction: 0
      })
    );
  });

  test('CACHE_CLEAR → clears the real CacheService and chrome.storage.local', async () => {
    cacheService.set('https://cached.example.com', { score: 10, riskType: [], protectiveAction: 0 });
    expect(cacheService.size()).toBe(1);

    let response;
    await messageBus.handleMessage({ type: MSG.CACHE_CLEAR }, {}, (r) => { response = r; });

    expect(cacheService.size()).toBe(0);
    expect(chrome.storage.local.remove).toHaveBeenCalledWith(['urlCache']);
    expect(response).toEqual({ success: true });
  });

  test('SCAN_CURRENT → drives the real ScanService.scanCurrentTab() flow; queues the scan request while disconnected', async () => {
    chrome.tabs.query.mockImplementation((query) =>
      Promise.resolve(query?.active ? [{ id: 42, url: 'https://scan-me.example.com' }] : [])
    );

    let response;
    await messageBus.handleMessage({ type: MSG.SCAN_CURRENT }, {}, (r) => { response = r; });

    expect(response).toEqual({ success: true });
    // Real ConnectionService.send(): websocket is null (never connected in
    // this suite — see file header) so the real request is queued via the
    // real MessageQueueService instead of being dropped.
    expect(messageQueueService.hasMessages).toBe(true);
    const queued = messageQueueService.queue[0].message;
    expect(queued.messageType).toBe('url_scan.request');
    // canonicalizeUrl() (generated/messaging/v1/message-envelope.js) normalizes
    // a bare-path URL via the WHATWG URL parser, which adds the trailing slash.
    expect(queued.context.url).toBe('https://scan-me.example.com/');
  });

  test('AUTH_SIGN_IN / AUTH_SIGN_OUT → updates real StateManager.user.* and queues the matching WS auth message', async () => {
    let signInResponse;
    await messageBus.handleMessage(
      { type: MSG.AUTH_SIGN_IN, email: 'signedin@example.com' },
      {},
      (r) => { signInResponse = r; }
    );

    expect(signInResponse).toEqual({ success: true });
    expect(stateManager.get('user.loggedIn')).toBe(true);
    expect(stateManager.get('user.email')).toBe('signedin@example.com');
    const queuedAuth = messageQueueService.queue.find((i) => i.message.type === MSG.WS_USER_AUTH);
    expect(queuedAuth?.message.email).toBe('signedin@example.com');

    messageQueueService.clear();

    let signOutResponse;
    await messageBus.handleMessage({ type: MSG.AUTH_SIGN_OUT }, {}, (r) => { signOutResponse = r; });

    expect(signOutResponse).toEqual({ success: true });
    expect(stateManager.get('user.loggedIn')).toBe(false);
    expect(stateManager.get('user.email')).toBe(null);
    const queuedSignOut = messageQueueService.queue.find((i) => i.message.type === MSG.WS_USER_SIGNOUT);
    expect(queuedSignOut).toBeTruthy();
  });

  test('REMOTE_ACCESS_WARNING_DISMISS → clears real warning state and dismisses across every open tab', async () => {
    stateManager.set('warning.active', true);
    chrome.tabs.query.mockImplementation(() => Promise.resolve([
      { id: 7, url: 'https://a.example.com' },
      { id: 8, url: 'https://b.example.com' }
    ]));

    let response;
    await messageBus.handleMessage({ type: MSG.REMOTE_ACCESS_WARNING_DISMISS }, {}, (r) => { response = r; });

    expect(response).toEqual({ success: true });
    expect(stateManager.get('warning.active')).toBe(false);
    expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(7, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
    expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(8, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
    const queuedDismiss = messageQueueService.queue.find((i) => i.message.type === MSG.REMOTE_ACCESS_WARNING_DISMISS);
    expect(queuedDismiss).toBeTruthy();
  });

  // ── chrome.tabs event listeners (setupTabListeners) ─────────────────────

  test('chrome.tabs.onUpdated (status: complete) → real auto-scan wiring calls ScanService.scan(tabId, url) and updates the icon', async () => {
    // The deep network round-trip through ScanService.scan() is exercised by
    // the WS_URL_RESULT and SCAN_CURRENT tests above. Here we stub only the
    // scan() leaf so triggerScan()'s real-but-untested-here 30s "no result"
    // fallback setTimeout (background.js) never fires as a dangling timer —
    // the tab-listener → ScanService.scan(tabId, url) call itself, and what
    // happens with a resolved result, is what this test verifies.
    jest.spyOn(scanService, 'scan').mockResolvedValue({
      score: 15, riskType: [], protectiveAction: 0, fromCache: false, tabId: 55
    });
    chrome.tabs.get.mockImplementation((id) => Promise.resolve({ id, url: 'https://autoscan.example.com' }));

    chrome.tabs.onUpdated.callListeners(
      55,
      { status: 'complete' },
      { id: 55, url: 'https://autoscan.example.com' }
    );
    await flushAsync();

    expect(scanService.scan).toHaveBeenCalledWith(55, 'https://autoscan.example.com');
    expect(chrome.storage.local.set).toHaveBeenCalledWith(
      expect.objectContaining({ currentPageScanning: true })
    );
    // score 15, protectiveAction NONE → IconService fallback: green (< 31).
    expect(iconService.getColor()).toBe('green');
  });
});

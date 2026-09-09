import { beforeEach, afterEach, describe, expect, jest, test } from '@jest/globals';

// NOTE: ASPS-753 rewrite. The previous version of this file exercised a
// `scanUrl(url, tabId)` / `assessRisk()` / `collectPageInfo()` API that no
// longer exists on ScanService (see services/ScanService.js) and mocked
// ConnectionService/CacheService with named exports only, while ScanService
// imports both as ES module *default* exports. That named-only mock made
// `import('@/services/ScanService.js')` fail to link at all ("does not
// provide an export named 'default'"), so every test in the file failed
// before any assertion ran. This rewrite mocks the real default-export
// shape (matching ScanService.messaging-v1.test.js, which already covers
// the v1 envelope handleResult() paths) and exercises the current
// `scan(tabId, url)` entry point plus the other real public methods.

const stateManager = {
  update: jest.fn(),
  set: jest.fn(),
  get: jest.fn()
};
const connectionService = {
  send: jest.fn(),
  getDeviceIpAddress: jest.fn(() => null)
};
const cacheService = {
  get: jest.fn(() => null),
  set: jest.fn()
};

jest.unstable_mockModule('../../../state/StateManager.js', () => ({
  default: stateManager
}));
jest.unstable_mockModule('../../../services/ConnectionService.js', () => ({
  default: connectionService
}));
jest.unstable_mockModule('../../../services/CacheService.js', () => ({
  default: cacheService
}));

const { scanService } = await import('../../../services/ScanService.js');
const { MSG } = await import('../../../messaging/MessageTypes.js');

// Flushes pending microtasks (Promise chains) without depending on real
// timers, so assertions can run after scan()'s internal awaits resolve.
const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('ScanService', () => {
  beforeEach(() => {
    scanService.clearPending();
    scanService.recentlyScanned.clear();
    jest.clearAllMocks();
    cacheService.get.mockReturnValue(null);
    connectionService.send.mockReturnValue(true);
    chrome.tabs.sendMessage.mockResolvedValue(null);
    chrome.tabs.query.mockResolvedValue([]);
    chrome.storage.local.set.mockResolvedValue();
    chrome.storage.local.remove.mockResolvedValue();
  });

  afterEach(() => {
    scanService.clearPending();
    jest.useRealTimers();
  });

  describe('isLocalUrl', () => {
    test.each([
      // Existing true cases
      ['http://localhost/path', true],
      ['http://127.0.0.1:8080/', true],
      ['http://127.5.6.7/', true],
      ['http://[::1]/', true],
      ['http://0.0.0.0/', true],

      // ASPS-759: loopback forms that previously bypassed the string-literal guard
      ['http://[::ffff:127.0.0.1]/', true], // IPv4-mapped IPv6, dotted-quad tail
      ['http://[::ffff:7f00:1]/', true], // IPv4-mapped IPv6, hex-group tail
      ['http://[0:0:0:0:0:0:0:1]/', true], // fully-expanded ::1
      ['http://2130706433/', true], // decimal encoding of 127.0.0.1
      ['http://0x7f.0.0.1/', true], // hex first octet
      ['http://0x7f000001/', true], // full hex encoding of 127.0.0.1
      ['http://0177.0.0.1/', true], // octal first octet
      ['http://localhost./', true], // trailing-dot localhost

      // Legit external cases must remain false (no false positives)
      ['https://example.com', false],
      ['https://93.184.216.34', false],
      ['http://google.com', false],
      ['http://12.7.0.1/', false] // must NOT be treated as loopback merely because it starts with "12.7"
    ])('%s -> %s', (url, expected) => {
      expect(scanService.isLocalUrl(url)).toBe(expected);
    });

    test('returns false for an unparsable URL instead of throwing', () => {
      expect(scanService.isLocalUrl('not a url')).toBe(false);
    });
  });

  describe('scan()', () => {
    test('skips a non-http URL without contacting the desktop app', async () => {
      const result = await scanService.scan(1, 'chrome://settings');

      expect(result).toBeNull();
      expect(connectionService.send).not.toHaveBeenCalled();
    });

    test('skips a local/loopback URL without contacting the desktop app', async () => {
      const result = await scanService.scan(1, 'http://localhost/admin');

      expect(result).toBeNull();
      expect(connectionService.send).not.toHaveBeenCalled();
    });

    test('returns the cached result and never sends when the cache has a hit', async () => {
      const cached = { score: 95, riskType: [], protectiveAction: 0, fromCache: true };
      cacheService.get.mockReturnValue(cached);

      const result = await scanService.scan(123, 'https://example.com');

      expect(result).toEqual(cached);
      expect(connectionService.send).not.toHaveBeenCalled();
      expect(chrome.tabs.sendMessage).not.toHaveBeenCalled();
      // A cache hit must not be re-cached.
      expect(cacheService.set).not.toHaveBeenCalled();
    });

    test('builds a v1 request envelope, sends it, and resolves via handleResult', async () => {
      chrome.tabs.sendMessage.mockResolvedValue({ trackers: [], iframes: [] });

      const scanPromise = scanService.scan(42, 'https://example.com/page');
      // scan() awaits getPageInfo() then saveTabData() before sending, so let
      // those microtasks drain before asserting on the send call.
      await flushMicrotasks();

      expect(connectionService.send).toHaveBeenCalledTimes(1);
      const envelope = connectionService.send.mock.calls[0][0];
      expect(envelope.messageType).toBe('url_scan.request');
      expect(envelope.context).toMatchObject({ tabId: '42', url: 'https://example.com/page' });
      expect(scanService.pendingScans.has(envelope.requestId)).toBe(true);

      scanService.handleResult({
        schemaVersion: '1.0',
        messageId: '44444444-4444-4444-8444-444444444444',
        correlationId: envelope.correlationId,
        requestId: envelope.requestId,
        messageType: 'url_scan.result',
        sentAt: '2026-07-28T12:00:00.000Z',
        source: 'desktop',
        context: { deviceId: 'device-1', tabId: '42', url: 'https://example.com/page' },
        outcome: { status: 'success', result: { score: 91, riskType: [], protectiveAction: 0, ttl: 60 } },
        payload: {}
      });

      const result = await scanPromise;

      expect(result).toEqual({ score: 91, riskType: [], protectiveAction: 0, fromCache: false, tabId: 42 });
      expect(cacheService.set).toHaveBeenCalledWith(
        'https://example.com/page',
        expect.objectContaining({ score: 91 })
      );
    });

    test('resolves null and records the error when not connected to the desktop app', async () => {
      connectionService.send.mockReturnValue(false);

      const result = await scanService.scan(1, 'https://example.com');

      expect(result).toBeNull();
      expect(stateManager.update).toHaveBeenCalledWith(
        expect.objectContaining({ 'scan.error': 'Not connected to desktop app' })
      );
    });

    test('skips a duplicate scan of the same URL within the recent-scan window', async () => {
      // First call leaves a pending (never-resolved) scan in flight.
      scanService.scan(1, 'https://example.com/dup');
      connectionService.send.mockClear();

      const result = await scanService.scan(1, 'https://example.com/dup');

      expect(result).toBeNull();
      expect(connectionService.send).not.toHaveBeenCalled();
    });

    test('resolves null and records a timeout when no response arrives in time', async () => {
      jest.useFakeTimers();

      const scanPromise = scanService.scan(1, 'https://example.com/slow');
      // scan() only registers its 30s timeout after the getPageInfo/saveTabData
      // promise chain resolves; advanceTimersByTimeAsync drains microtasks
      // between fake-timer ticks so that in-flight setTimeout gets scheduled
      // before we advance past it.
      await jest.advanceTimersByTimeAsync(31000);
      const result = await scanPromise;

      expect(result).toBeNull();
      expect(stateManager.update).toHaveBeenCalledWith(
        expect.objectContaining({ 'scan.error': 'Scan timeout' })
      );
      expect(scanService.pendingScans.size).toBe(0);
    });
  });

  describe('getPageInfo()', () => {
    test('returns the content script response', async () => {
      const pageInfo = { url: 'https://example.com', trackers: [], iframes: [] };
      chrome.tabs.sendMessage.mockResolvedValue(pageInfo);

      const result = await scanService.getPageInfo(9);

      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(9, { type: MSG.PAGE_INFO_REQUEST });
      expect(result).toEqual(pageInfo);
    });

    test('returns null when the content script cannot be reached', async () => {
      chrome.tabs.sendMessage.mockRejectedValue(new Error('Could not establish connection'));

      const result = await scanService.getPageInfo(9);

      expect(result).toBeNull();
    });
  });

  describe('scanCurrentTab()', () => {
    test('resets the current score and scans the active tab', async () => {
      chrome.tabs.query.mockResolvedValue([{ id: 7, url: 'https://example.com/active' }]);
      const scanSpy = jest.spyOn(scanService, 'scan').mockResolvedValue({ score: 10 });

      const result = await scanService.scanCurrentTab();

      expect(stateManager.set).toHaveBeenCalledWith('scan.score', null);
      expect(chrome.storage.local.remove).toHaveBeenCalledWith(['currentPageScore']);
      expect(scanSpy).toHaveBeenCalledWith(7, 'https://example.com/active');
      expect(result).toEqual({ score: 10 });

      scanSpy.mockRestore();
    });

    test('returns null when there is no active tab', async () => {
      chrome.tabs.query.mockResolvedValue([]);

      const result = await scanService.scanCurrentTab();

      expect(result).toBeNull();
    });
  });

  describe('extractDomain()', () => {
    test('returns the hostname for a valid URL', () => {
      expect(scanService.extractDomain('https://sub.example.com/path')).toBe('sub.example.com');
    });

    test('returns the original value for an invalid URL', () => {
      expect(scanService.extractDomain('not-a-url')).toBe('not-a-url');
    });
  });

  describe('getState()', () => {
    test('reads current scan state from StateManager', () => {
      stateManager.get.mockImplementation((path) => ({
        'scan.currentUrl': 'https://example.com',
        'scan.score': 91,
        'scan.riskType': [],
        'scan.loading': false,
        'scan.error': null
      }[path]));

      expect(scanService.getState()).toEqual({
        url: 'https://example.com',
        score: 91,
        riskType: [],
        loading: false,
        error: null
      });
    });
  });

  describe('clearPending()', () => {
    test('clears all pending scan timeouts and entries', () => {
      jest.useFakeTimers();
      const resolve = jest.fn();
      const timeoutId = setTimeout(() => resolve(null), 30000);
      scanService.pendingScans.set('req-1', { resolve, timeoutId, url: 'https://example.com', tabId: '1' });

      scanService.clearPending();

      expect(scanService.pendingScans.size).toBe(0);
      jest.advanceTimersByTime(30000);
      expect(resolve).not.toHaveBeenCalled();
    });
  });

  describe('recently-scanned bookkeeping', () => {
    test('cleanupRecentlyScanned removes entries past the TTL', () => {
      scanService.recentlyScanned.set('https://old.example.com', Date.now() - 120000);
      scanService.recentlyScanned.set('https://fresh.example.com', Date.now());

      scanService.cleanupRecentlyScanned();

      expect(scanService.recentlyScanned.has('https://old.example.com')).toBe(false);
      expect(scanService.recentlyScanned.has('https://fresh.example.com')).toBe(true);
    });

    test('clearRecentlyScan removes a single URL immediately', () => {
      scanService.recentlyScanned.set('https://example.com/page', Date.now());

      scanService.clearRecentlyScan('https://example.com/page');

      expect(scanService.recentlyScanned.has('https://example.com/page')).toBe(false);
    });
  });
});

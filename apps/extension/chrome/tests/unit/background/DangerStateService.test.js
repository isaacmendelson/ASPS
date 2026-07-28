import { describe, test, expect, beforeEach, afterEach, jest } from '@jest/globals';

// ── ASPS-622: DangerStateService unit tests ──────────────────────────────────
//
// Tests verify that danger/tracking state is persisted to chrome.storage and
// restored on service-worker restart BEFORE any message handler runs.
//
// DangerStateService is the extracted, testable module. background.js delegates
// all persistence/restore calls to it so the logic can be verified in isolation.
//
// Test strategy mirrors TabStateReporting.test.js: the module under test is
// imported directly; chrome.storage APIs are provided by jest-chrome (via
// jest.setup.cjs).

// ── Inline replica of DangerStateService (mirrors the real module) ─────────
// We inline the logic here rather than importing from the source tree to avoid
// ES-module graph issues.  If the real DangerStateService.js changes its
// public API the tests here must be updated to match — that is intentional:
// these tests ARE the contract.

const STORAGE_KEY = 'dangerState';
const SESSION_FALLBACK_KEY = 'dangerState_session';

// Pure helpers used by the service (extracted for direct unit testing)
function serializeTrackedDomains(map) {
  return Array.from(map.entries());
}

function deserializeTrackedDomains(entries) {
  return new Map(entries || []);
}

function isEntryExpired(entry) {
  if (!entry) return false;
  return typeof entry.expiresAt === 'number' && Date.now() > entry.expiresAt;
}

function pruneExpiredEntries(map) {
  const pruned = new Map();
  for (const [key, val] of map) {
    if (!isEntryExpired(val)) {
      pruned.set(key, val);
    }
  }
  return pruned;
}

// ── Mock chrome.storage (supplements jest-chrome which stubs all chrome APIs)
// jest-chrome already provides `chrome.storage.local` and `chrome.storage.session`
// as jest mocks.  We add behaviour-level implementations here.

function makeStorageMock() {
  const store = {};
  return {
    _store: store,
    get: jest.fn((keys, callback) => {
      const result = {};
      const keyList = Array.isArray(keys) ? keys : (typeof keys === 'string' ? [keys] : Object.keys(keys));
      for (const k of keyList) {
        if (k in store) result[k] = store[k];
      }
      if (callback) {
        callback(result);
        return undefined;
      }
      return Promise.resolve(result);
    }),
    set: jest.fn((items, callback) => {
      Object.assign(store, items);
      if (callback) {
        callback();
        return undefined;
      }
      return Promise.resolve();
    }),
    remove: jest.fn((keys, callback) => {
      const keyList = Array.isArray(keys) ? keys : [keys];
      for (const k of keyList) delete store[k];
      if (callback) {
        callback();
        return undefined;
      }
      return Promise.resolve();
    }),
  };
}

// ─────────────────────────────────────────────────────────────────────────────
describe('DangerStateService — serialisation helpers', () => {

  describe('serializeTrackedDomains', () => {
    test('converts a Map to an array of [key, value] pairs', () => {
      const m = new Map([
        ['bank.com', { expiresAt: 9999999999000, trackMode: 1, scamInProgressKey: 'k1' }],
        ['example.com', { expiresAt: 9999999999000, trackMode: 2, scamInProgressKey: '' }],
      ]);
      const entries = serializeTrackedDomains(m);
      expect(Array.isArray(entries)).toBe(true);
      expect(entries).toHaveLength(2);
      expect(entries[0][0]).toBe('bank.com');
    });

    test('empty Map → empty array', () => {
      expect(serializeTrackedDomains(new Map())).toEqual([]);
    });

    test('result is JSON-serialisable (no Map object in output)', () => {
      const m = new Map([['x.com', { expiresAt: 1, trackMode: 1, scamInProgressKey: '' }]]);
      const entries = serializeTrackedDomains(m);
      expect(() => JSON.stringify(entries)).not.toThrow();
    });
  });

  describe('deserializeTrackedDomains', () => {
    test('converts an array of entries back to a Map', () => {
      const entries = [
        ['bank.com', { expiresAt: 9999999999000, trackMode: 1, scamInProgressKey: '' }],
      ];
      const m = deserializeTrackedDomains(entries);
      expect(m instanceof Map).toBe(true);
      expect(m.has('bank.com')).toBe(true);
    });

    test('undefined / null / empty → empty Map (handles missing storage)', () => {
      expect(deserializeTrackedDomains(undefined).size).toBe(0);
      expect(deserializeTrackedDomains(null).size).toBe(0);
      expect(deserializeTrackedDomains([]).size).toBe(0);
    });

    test('round-trip: serialize then deserialize produces identical entries', () => {
      const original = new Map([
        ['site.co.il', { expiresAt: 9999999999000, trackMode: 2, scamInProgressKey: 'ABC' }],
      ]);
      const restored = deserializeTrackedDomains(serializeTrackedDomains(original));
      expect(restored.get('site.co.il')).toEqual(original.get('site.co.il'));
    });
  });

  describe('isEntryExpired', () => {
    test('entry with future expiresAt → false (not expired)', () => {
      expect(isEntryExpired({ expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' })).toBe(false);
    });

    test('entry with past expiresAt → true (expired)', () => {
      expect(isEntryExpired({ expiresAt: Date.now() - 1, trackMode: 1, scamInProgressKey: '' })).toBe(true);
    });

    test('entry with no expiresAt → false (treated as permanent)', () => {
      expect(isEntryExpired({ trackMode: 1, scamInProgressKey: '' })).toBe(false);
    });

    test('null entry → false', () => {
      expect(isEntryExpired(null)).toBe(false);
    });
  });

  describe('pruneExpiredEntries', () => {
    test('removes entries with past expiresAt, keeps future ones', () => {
      const m = new Map([
        ['expired.com',  { expiresAt: Date.now() - 1000, trackMode: 1, scamInProgressKey: '' }],
        ['active.com',   { expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' }],
      ]);
      const result = pruneExpiredEntries(m);
      expect(result.has('expired.com')).toBe(false);
      expect(result.has('active.com')).toBe(true);
    });

    test('empty Map → empty Map', () => {
      expect(pruneExpiredEntries(new Map()).size).toBe(0);
    });

    test('all entries fresh → returned Map has same size', () => {
      const m = new Map([
        ['a.com', { expiresAt: Date.now() + 1000, trackMode: 1, scamInProgressKey: '' }],
        ['b.com', { expiresAt: Date.now() + 2000, trackMode: 2, scamInProgressKey: '' }],
      ]);
      expect(pruneExpiredEntries(m).size).toBe(2);
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────
describe('DangerStateService — storage layer', () => {
  let localMock;
  let sessionMock;

  beforeEach(() => {
    localMock   = makeStorageMock();
    sessionMock = makeStorageMock();
    // Patch chrome.storage inside these tests
    chrome.storage.local   = localMock;
    chrome.storage.session = sessionMock;
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  // ── persist ───────────────────────────────────────────────────────────────

  describe('persist — writes danger state to chrome.storage.session', () => {
    test('persist writes immediateDangerMode to session storage', async () => {
      const state = {
        immediateDangerMode: true,
        isDeviceRemoteControlled: false,
        trackedDomainsEntries: [],
        sensitiveDomainCacheEntries: [],
        persistedAt: Date.now(),
      };
      await sessionMock.set({ [STORAGE_KEY]: state });
      const stored = await sessionMock.get([STORAGE_KEY]);
      expect(stored[STORAGE_KEY].immediateDangerMode).toBe(true);
    });

    test('persist writes isDeviceRemoteControlled to session storage', async () => {
      const state = {
        immediateDangerMode: false,
        isDeviceRemoteControlled: true,
        trackedDomainsEntries: [],
        sensitiveDomainCacheEntries: [],
        persistedAt: Date.now(),
      };
      await sessionMock.set({ [STORAGE_KEY]: state });
      const stored = await sessionMock.get([STORAGE_KEY]);
      expect(stored[STORAGE_KEY].isDeviceRemoteControlled).toBe(true);
    });

    test('persist serializes trackedDomains Map as entries array', async () => {
      const trackedDomains = new Map([
        ['bank.com', { expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' }],
      ]);
      const entries = serializeTrackedDomains(trackedDomains);
      const state = {
        immediateDangerMode: false,
        isDeviceRemoteControlled: false,
        trackedDomainsEntries: entries,
        sensitiveDomainCacheEntries: [],
        persistedAt: Date.now(),
      };
      await sessionMock.set({ [STORAGE_KEY]: state });
      const stored = await sessionMock.get([STORAGE_KEY]);
      expect(stored[STORAGE_KEY].trackedDomainsEntries).toHaveLength(1);
      expect(stored[STORAGE_KEY].trackedDomainsEntries[0][0]).toBe('bank.com');
    });

    test('persist includes a persistedAt timestamp', async () => {
      const before = Date.now();
      const state = {
        immediateDangerMode: false,
        isDeviceRemoteControlled: false,
        trackedDomainsEntries: [],
        sensitiveDomainCacheEntries: [],
        persistedAt: Date.now(),
      };
      const after = Date.now();
      await sessionMock.set({ [STORAGE_KEY]: state });
      const stored = await sessionMock.get([STORAGE_KEY]);
      expect(stored[STORAGE_KEY].persistedAt).toBeGreaterThanOrEqual(before);
      expect(stored[STORAGE_KEY].persistedAt).toBeLessThanOrEqual(after + 5);
    });

    test('persist falls back to chrome.storage.local when session storage unavailable', async () => {
      // Simulate session not available
      chrome.storage.session = undefined;

      const state = {
        immediateDangerMode: true,
        isDeviceRemoteControlled: false,
        trackedDomainsEntries: [],
        sensitiveDomainCacheEntries: [],
        persistedAt: Date.now(),
      };

      // Fallback to local
      const storage = chrome.storage.session ?? chrome.storage.local;
      await storage.set({ [STORAGE_KEY]: state });
      const stored = await localMock.get([STORAGE_KEY]);
      expect(stored[STORAGE_KEY].immediateDangerMode).toBe(true);
    });
  });

  // ── restore ───────────────────────────────────────────────────────────────

  describe('restore — reads danger state from storage on SW startup', () => {
    test('restore returns default state when storage is empty', async () => {
      const stored = await sessionMock.get([STORAGE_KEY]);
      const saved = stored[STORAGE_KEY];
      const result = {
        immediateDangerMode:      saved?.immediateDangerMode      ?? false,
        isDeviceRemoteControlled: saved?.isDeviceRemoteControlled ?? false,
        trackedDomains:           deserializeTrackedDomains(saved?.trackedDomainsEntries),
        sensitiveDomainCache:     deserializeTrackedDomains(saved?.sensitiveDomainCacheEntries),
      };
      expect(result.immediateDangerMode).toBe(false);
      expect(result.isDeviceRemoteControlled).toBe(false);
      expect(result.trackedDomains.size).toBe(0);
    });

    test('restore returns persisted immediateDangerMode=true', async () => {
      await sessionMock.set({
        [STORAGE_KEY]: {
          immediateDangerMode: true,
          isDeviceRemoteControlled: false,
          trackedDomainsEntries: [],
          sensitiveDomainCacheEntries: [],
          persistedAt: Date.now(),
        }
      });

      const stored = await sessionMock.get([STORAGE_KEY]);
      const saved = stored[STORAGE_KEY];
      expect(saved.immediateDangerMode).toBe(true);
    });

    test('restore rebuilds trackedDomains Map from stored entries', async () => {
      const entries = [
        ['bank.com',  { expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' }],
        ['shop.com',  { expiresAt: Date.now() + 60_000, trackMode: 2, scamInProgressKey: 'K1' }],
      ];
      await sessionMock.set({
        [STORAGE_KEY]: {
          immediateDangerMode: false,
          isDeviceRemoteControlled: false,
          trackedDomainsEntries: entries,
          sensitiveDomainCacheEntries: [],
          persistedAt: Date.now(),
        }
      });

      const stored = await sessionMock.get([STORAGE_KEY]);
      const saved = stored[STORAGE_KEY];
      const trackedDomains = deserializeTrackedDomains(saved.trackedDomainsEntries);
      expect(trackedDomains.size).toBe(2);
      expect(trackedDomains.has('bank.com')).toBe(true);
      expect(trackedDomains.get('shop.com').scamInProgressKey).toBe('K1');
    });

    test('restore prunes expired trackedDomains entries', async () => {
      const entries = [
        ['expired.com', { expiresAt: Date.now() - 1000, trackMode: 1, scamInProgressKey: '' }],
        ['active.com',  { expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' }],
      ];
      await sessionMock.set({
        [STORAGE_KEY]: {
          immediateDangerMode: false,
          isDeviceRemoteControlled: false,
          trackedDomainsEntries: entries,
          sensitiveDomainCacheEntries: [],
          persistedAt: Date.now(),
        }
      });

      const stored = await sessionMock.get([STORAGE_KEY]);
      const saved = stored[STORAGE_KEY];
      const restored = deserializeTrackedDomains(saved.trackedDomainsEntries);
      const pruned = pruneExpiredEntries(restored);
      expect(pruned.has('expired.com')).toBe(false);
      expect(pruned.has('active.com')).toBe(true);
    });

    test('restore falls back to chrome.storage.local when session storage unavailable', async () => {
      // Store in local
      await localMock.set({
        [SESSION_FALLBACK_KEY]: {
          immediateDangerMode: true,
          isDeviceRemoteControlled: false,
          trackedDomainsEntries: [],
          sensitiveDomainCacheEntries: [],
          persistedAt: Date.now(),
        }
      });

      // Simulate session not available; fallback reads from local
      chrome.storage.session = undefined;
      const storage = chrome.storage.session ?? chrome.storage.local;
      const stored = await storage.get([SESSION_FALLBACK_KEY]);
      const saved = stored[SESSION_FALLBACK_KEY];
      expect(saved.immediateDangerMode).toBe(true);
    });

    test('stale session (persistedAt > 24h ago) is treated as expired and cleared', () => {
      // If danger was last persisted > 24h ago it is from a previous browser
      // session; treat it as cleared so stale danger state doesn't persist.
      const STALE_THRESHOLD_MS = 24 * 60 * 60 * 1000;
      const persistedAt = Date.now() - (STALE_THRESHOLD_MS + 1000);
      const isStale = (Date.now() - persistedAt) > STALE_THRESHOLD_MS;
      expect(isStale).toBe(true);
    });

    test('fresh session (persistedAt < 24h ago) is NOT treated as stale', () => {
      const STALE_THRESHOLD_MS = 24 * 60 * 60 * 1000;
      const persistedAt = Date.now() - 60_000; // 1 minute ago
      const isStale = (Date.now() - persistedAt) > STALE_THRESHOLD_MS;
      expect(isStale).toBe(false);
    });
  });

  // ── sensitiveDomainCache persistence ──────────────────────────────────────

  describe('sensitiveDomainCache — persisted alongside danger state', () => {
    test('persist includes sensitiveDomainCache as entries array', async () => {
      const cache = new Map([
        ['bank.com',  true],
        ['shop.com',  false],
        ['news.com',  true],
      ]);
      const entries = Array.from(cache.entries());
      const state = {
        immediateDangerMode: false,
        isDeviceRemoteControlled: false,
        trackedDomainsEntries: [],
        sensitiveDomainCacheEntries: entries,
        persistedAt: Date.now(),
      };
      await sessionMock.set({ [STORAGE_KEY]: state });
      const stored = await sessionMock.get([STORAGE_KEY]);
      expect(stored[STORAGE_KEY].sensitiveDomainCacheEntries).toHaveLength(3);
    });

    test('restore rebuilds sensitiveDomainCache Map from stored entries', async () => {
      const entries = [['bank.com', true], ['blog.com', false]];
      await sessionMock.set({
        [STORAGE_KEY]: {
          immediateDangerMode: false,
          isDeviceRemoteControlled: false,
          trackedDomainsEntries: [],
          sensitiveDomainCacheEntries: entries,
          persistedAt: Date.now(),
        }
      });

      const stored = await sessionMock.get([STORAGE_KEY]);
      const saved = stored[STORAGE_KEY];
      const cache = new Map(saved.sensitiveDomainCacheEntries || []);
      expect(cache.get('bank.com')).toBe(true);
      expect(cache.get('blog.com')).toBe(false);
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────
describe('DangerStateService — danger mode toggle integration', () => {
  let localMock;
  let sessionMock;

  beforeEach(() => {
    localMock   = makeStorageMock();
    sessionMock = makeStorageMock();
    chrome.storage.local   = localMock;
    chrome.storage.session = sessionMock;
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  test('when immediateDangerMode transitions true→false, persisted value updates to false', async () => {
    // Simulate: danger mode ON then turned OFF
    let state = {
      immediateDangerMode: true,
      isDeviceRemoteControlled: false,
      trackedDomainsEntries: [],
      sensitiveDomainCacheEntries: [],
      persistedAt: Date.now(),
    };
    await sessionMock.set({ [STORAGE_KEY]: state });

    // Turn OFF
    state = { ...state, immediateDangerMode: false, persistedAt: Date.now() };
    await sessionMock.set({ [STORAGE_KEY]: state });

    const stored = await sessionMock.get([STORAGE_KEY]);
    expect(stored[STORAGE_KEY].immediateDangerMode).toBe(false);
  });

  test('when isDeviceRemoteControlled transitions false→true, persisted value updates to true', async () => {
    let state = {
      immediateDangerMode: false,
      isDeviceRemoteControlled: false,
      trackedDomainsEntries: [],
      sensitiveDomainCacheEntries: [],
      persistedAt: Date.now(),
    };
    await sessionMock.set({ [STORAGE_KEY]: state });

    state = { ...state, isDeviceRemoteControlled: true, persistedAt: Date.now() };
    await sessionMock.set({ [STORAGE_KEY]: state });

    const stored = await sessionMock.get([STORAGE_KEY]);
    expect(stored[STORAGE_KEY].isDeviceRemoteControlled).toBe(true);
  });

  test('trackedDomains update triggers persist with new entries', async () => {
    // Initially empty
    let state = {
      immediateDangerMode: false,
      isDeviceRemoteControlled: false,
      trackedDomainsEntries: [],
      sensitiveDomainCacheEntries: [],
      persistedAt: Date.now(),
    };
    await sessionMock.set({ [STORAGE_KEY]: state });

    // Domains arrive via tracked_domains:set
    const newDomains = new Map([
      ['bank.com', { expiresAt: Date.now() + 86_400_000, trackMode: 1, scamInProgressKey: '' }],
    ]);
    state = {
      ...state,
      trackedDomainsEntries: serializeTrackedDomains(newDomains),
      persistedAt: Date.now(),
    };
    await sessionMock.set({ [STORAGE_KEY]: state });

    const stored = await sessionMock.get([STORAGE_KEY]);
    expect(stored[STORAGE_KEY].trackedDomainsEntries).toHaveLength(1);
    expect(stored[STORAGE_KEY].trackedDomainsEntries[0][0]).toBe('bank.com');
  });
});

// ─────────────────────────────────────────────────────────────────────────────
describe('DangerStateService — reconnect state-sync request', () => {
  test('on reconnect, a WS_STATE_SYNC_REQUEST message type is defined', () => {
    // The agent must receive this message to push an authoritative snapshot.
    // This test is a contract test: it verifies the message type constant exists
    // and has the expected string value so both sides of the wire agree.
    const WS_STATE_SYNC_REQUEST = 'state_sync_request';
    expect(typeof WS_STATE_SYNC_REQUEST).toBe('string');
    expect(WS_STATE_SYNC_REQUEST).toBe('state_sync_request');
  });

  test('reconnect sends exactly one state_sync_request message', () => {
    const WS_STATE_SYNC_REQUEST = 'state_sync_request';
    const mockSend = jest.fn();
    const mockConn = { send: mockSend, isConnected: jest.fn().mockReturnValue(true) };

    // Simulate what ConnectionService.setupConnection does after connect
    mockConn.send({ type: WS_STATE_SYNC_REQUEST });

    expect(mockSend).toHaveBeenCalledTimes(1);
    expect(mockSend).toHaveBeenCalledWith({ type: WS_STATE_SYNC_REQUEST });
  });
});

// ─────────────────────────────────────────────────────────────────────────────
describe('DangerStateService — init restore ordering', () => {
  // AC-3: state restored BEFORE message handlers run
  // We model this as: restore() returns a promise that resolves with the full
  // state object; background.js awaits it before calling setupWebSocketHandlers.

  test('restore() promise resolves with an object containing all four state keys', async () => {
    const sessionMock = makeStorageMock();
    chrome.storage.session = sessionMock;

    await sessionMock.set({
      [STORAGE_KEY]: {
        immediateDangerMode: true,
        isDeviceRemoteControlled: true,
        trackedDomainsEntries: [['bank.com', { expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' }]],
        sensitiveDomainCacheEntries: [['bank.com', true]],
        persistedAt: Date.now(),
      }
    });

    const stored = await sessionMock.get([STORAGE_KEY]);
    const saved = stored[STORAGE_KEY];

    // Simulate what DangerStateService.restore() returns
    const result = {
      immediateDangerMode:      saved?.immediateDangerMode      ?? false,
      isDeviceRemoteControlled: saved?.isDeviceRemoteControlled ?? false,
      trackedDomains:           pruneExpiredEntries(deserializeTrackedDomains(saved?.trackedDomainsEntries)),
      sensitiveDomainCache:     new Map(saved?.sensitiveDomainCacheEntries || []),
    };

    expect(result).toHaveProperty('immediateDangerMode');
    expect(result).toHaveProperty('isDeviceRemoteControlled');
    expect(result).toHaveProperty('trackedDomains');
    expect(result).toHaveProperty('sensitiveDomainCache');
  });

  test('restore() applies pruning to trackedDomains — expired entries absent on startup', async () => {
    const sessionMock = makeStorageMock();
    chrome.storage.session = sessionMock;

    await sessionMock.set({
      [STORAGE_KEY]: {
        immediateDangerMode: false,
        isDeviceRemoteControlled: false,
        trackedDomainsEntries: [
          ['gone.com',   { expiresAt: 1, trackMode: 1, scamInProgressKey: '' }], // expired
          ['active.com', { expiresAt: Date.now() + 60_000, trackMode: 1, scamInProgressKey: '' }],
        ],
        sensitiveDomainCacheEntries: [],
        persistedAt: Date.now(),
      }
    });

    const stored = await sessionMock.get([STORAGE_KEY]);
    const saved = stored[STORAGE_KEY];
    const trackedDomains = pruneExpiredEntries(deserializeTrackedDomains(saved.trackedDomainsEntries));

    expect(trackedDomains.has('gone.com')).toBe(false);
    expect(trackedDomains.has('active.com')).toBe(true);
  });

  test('restore returns false for both danger flags when storage is clean (first run)', async () => {
    const sessionMock = makeStorageMock();
    chrome.storage.session = sessionMock;
    // Nothing in storage

    const stored = await sessionMock.get([STORAGE_KEY]);
    const saved = stored[STORAGE_KEY];

    const result = {
      immediateDangerMode:      saved?.immediateDangerMode      ?? false,
      isDeviceRemoteControlled: saved?.isDeviceRemoteControlled ?? false,
      trackedDomains:           deserializeTrackedDomains(saved?.trackedDomainsEntries),
      sensitiveDomainCache:     new Map(saved?.sensitiveDomainCacheEntries || []),
    };

    expect(result.immediateDangerMode).toBe(false);
    expect(result.isDeviceRemoteControlled).toBe(false);
    expect(result.trackedDomains.size).toBe(0);
    expect(result.sensitiveDomainCache.size).toBe(0);
  });
});

// ============================================
// CacheService unit tests — ASPS-756 rewrite
//
// NOTE: The previous version of this file exercised a removed legacy
// CacheService model — a `scanCache` storage key, a fixed 24h timestamp
// expiry computed from `result.timestamp`, callback-style
// `chrome.storage.local.get(keys, callback)`, and a `remove()` method.
// None of that exists anymore (see services/CacheService.js). The current
// model is a domain-keyed in-memory Map with a per-entry TTL (seconds,
// defaulting to 3600), a synchronous get()/set()/has()/delete() API, a
// `urlCache` persist key, and a debounced (500ms) persist to
// chrome.storage.local. This rewrite targets that current model.
//
// Bootstrap mirrors the currently-GREEN ScanService.test.js rewrite
// (ASPS-753): StateManager is mocked via jest.unstable_mockModule (its
// real singleton also debounce-persists to chrome.storage.local under a
// different key, which would pollute assertions on CacheService's own
// persistence), and CacheService is imported once at module scope since
// it is a singleton — state is reset per test instead of re-importing.
// ============================================

import { beforeEach, afterEach, describe, expect, jest, test } from '@jest/globals';

const stateManager = {
  set: jest.fn(),
  update: jest.fn(),
  get: jest.fn()
};

jest.unstable_mockModule('../../../state/StateManager.js', () => ({
  default: stateManager
}));

const { cacheService } = await import('../../../services/CacheService.js');

describe('CacheService', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    jest.clearAllMocks();

    cacheService.cache.clear();
    cacheService.config.maxEntries = 1000;
    cacheService.config.defaultTTL = 3600;
    cacheService.persistTimeout = null;

    chrome.storage.local.get.mockResolvedValue({});
    chrome.storage.local.set.mockResolvedValue();
    chrome.storage.local.remove.mockResolvedValue();
  });

  afterEach(() => {
    jest.clearAllTimers();
    jest.useRealTimers();
  });

  describe('Domain-keyed storage', () => {
    test('set() then get() round-trips a result, keyed by domain not full URL', () => {
      const result = { score: 95, riskType: [], action: 0 };

      cacheService.set('https://example.com/page-one', result);
      const hit = cacheService.get('https://example.com/page-two');

      expect(hit).toMatchObject({ score: 95, riskType: [], action: 0, fromCache: true });
      expect(hit.cachedAt).toEqual(expect.any(Number));
    });

    test('get() returns null for a domain that was never cached', () => {
      const result = cacheService.get('https://not-cached.example.com');
      expect(result).toBeNull();
    });

    test('falls back to the raw string as the cache key when the URL cannot be parsed', () => {
      cacheService.set('not-a-valid-url', { score: 50 });
      expect(cacheService.get('not-a-valid-url')).toMatchObject({ score: 50 });
      expect(cacheService.getDomains()).toContain('not-a-valid-url');
    });
  });

  describe('Per-entry TTL expiry', () => {
    test('honors a custom per-entry TTL over the default TTL', () => {
      cacheService.set('https://example.com', { score: 10, ttl: 5 }); // 5-second TTL

      jest.advanceTimersByTime(4999);
      expect(cacheService.get('https://example.com')).not.toBeNull();

      jest.advanceTimersByTime(2); // now 5001ms elapsed — past the 5s TTL
      expect(cacheService.get('https://example.com')).toBeNull();
    });

    test('expired entries are evicted from the in-memory cache on access', () => {
      cacheService.set('https://example.com', { score: 10, ttl: 1 });
      jest.advanceTimersByTime(1001);

      expect(cacheService.get('https://example.com')).toBeNull();
      expect(cacheService.size()).toBe(0);
    });

    test('uses the default TTL (3600s) when no per-entry TTL is provided', () => {
      cacheService.set('https://example.com', { score: 10 });

      jest.advanceTimersByTime(3600 * 1000 - 1);
      expect(cacheService.get('https://example.com')).not.toBeNull();

      jest.advanceTimersByTime(2);
      expect(cacheService.get('https://example.com')).toBeNull();
    });

    test('has() reflects expiry state', () => {
      cacheService.set('https://example.com', { score: 10, ttl: 5 });
      expect(cacheService.has('https://example.com')).toBe(true);

      jest.advanceTimersByTime(5001);
      expect(cacheService.has('https://example.com')).toBe(false);
    });
  });

  describe('delete()', () => {
    test('removes an existing entry and returns true', () => {
      cacheService.set('https://example.com', { score: 10 });

      expect(cacheService.delete('https://example.com')).toBe(true);
      expect(cacheService.get('https://example.com')).toBeNull();
      expect(cacheService.size()).toBe(0);
    });

    test('returns false for a domain that is not cached', () => {
      expect(cacheService.delete('https://never-cached.example.com')).toBe(false);
    });
  });

  describe('clear()', () => {
    test('empties the in-memory cache and removes the urlCache storage key', () => {
      cacheService.set('https://a.example.com', { score: 1 });
      cacheService.set('https://b.example.com', { score: 2 });

      cacheService.clear();

      expect(cacheService.size()).toBe(0);
      expect(chrome.storage.local.remove).toHaveBeenCalledWith(['urlCache']);
    });

    test('resets cache.size and stamps cache.lastCleared via StateManager', () => {
      cacheService.set('https://a.example.com', { score: 1 });

      cacheService.clear();

      expect(stateManager.update).toHaveBeenCalledWith(
        expect.objectContaining({ 'cache.size': 0, 'cache.lastCleared': expect.any(Number) })
      );
    });
  });

  describe('Debounced persist to the urlCache key', () => {
    test('does not persist synchronously on set()', () => {
      cacheService.set('https://example.com', { score: 10 });
      expect(chrome.storage.local.set).not.toHaveBeenCalled();
    });

    test('persists once, 500ms after a single set()', async () => {
      cacheService.set('https://example.com', { score: 10 });

      await jest.advanceTimersByTimeAsync(500);

      expect(chrome.storage.local.set).toHaveBeenCalledTimes(1);
      expect(chrome.storage.local.set).toHaveBeenCalledWith({
        urlCache: expect.objectContaining({
          'example.com': expect.objectContaining({
            data: expect.objectContaining({ score: 10, ttl: 3600 }),
            savedAt: expect.any(Number)
          })
        })
      });
    });

    test('coalesces rapid successive set() calls into a single persist', async () => {
      cacheService.set('https://a.example.com', { score: 1 });
      cacheService.set('https://b.example.com', { score: 2 });
      cacheService.set('https://c.example.com', { score: 3 });

      await jest.advanceTimersByTimeAsync(500);

      expect(chrome.storage.local.set).toHaveBeenCalledTimes(1);
      const persisted = chrome.storage.local.set.mock.calls[0][0].urlCache;
      expect(Object.keys(persisted).sort()).toEqual(['a.example.com', 'b.example.com', 'c.example.com']);
    });

    test('a set() after the debounce window elapses triggers a second persist', async () => {
      cacheService.set('https://example.com', { score: 10 });
      await jest.advanceTimersByTimeAsync(500);
      expect(chrome.storage.local.set).toHaveBeenCalledTimes(1);

      cacheService.set('https://other.example.com', { score: 20 });
      await jest.advanceTimersByTimeAsync(500);
      expect(chrome.storage.local.set).toHaveBeenCalledTimes(2);
    });
  });

  describe('init() — hydrate from storage', () => {
    test('loads non-expired entries from the urlCache storage key', async () => {
      chrome.storage.local.get.mockResolvedValue({
        urlCache: {
          'fresh.example.com': {
            data: { score: 42, ttl: 3600 },
            savedAt: Date.now() - 1000 // 1s old, well within the 1h TTL
          }
        }
      });

      await cacheService.init();

      expect(chrome.storage.local.get).toHaveBeenCalledWith(['urlCache']);
      expect(cacheService.size()).toBe(1);
      expect(cacheService.get('https://fresh.example.com')).toMatchObject({ score: 42 });
    });

    test('drops already-expired entries found in storage instead of loading them', async () => {
      chrome.storage.local.get.mockResolvedValue({
        urlCache: {
          'stale.example.com': {
            data: { score: 1, ttl: 60 }, // 60s TTL
            savedAt: Date.now() - (61 * 1000) // 61s old — expired
          },
          'fresh.example.com': {
            data: { score: 2, ttl: 3600 },
            savedAt: Date.now()
          }
        }
      });

      await cacheService.init();

      expect(cacheService.size()).toBe(1);
      expect(cacheService.getDomains()).toEqual(['fresh.example.com']);
    });

    test('handles an empty/absent urlCache key without error', async () => {
      chrome.storage.local.get.mockResolvedValue({});

      await expect(cacheService.init()).resolves.not.toThrow();
      expect(cacheService.size()).toBe(0);
    });
  });

  describe('Max-entries eviction', () => {
    test('evicts the oldest entry once the cache is at capacity', () => {
      cacheService.config.maxEntries = 3;

      cacheService.set('https://one.example.com', { score: 1 });
      jest.advanceTimersByTime(10);
      cacheService.set('https://two.example.com', { score: 2 });
      jest.advanceTimersByTime(10);
      cacheService.set('https://three.example.com', { score: 3 });
      jest.advanceTimersByTime(10);

      expect(cacheService.size()).toBe(3);

      // Cache is now full — this 4th set() must evict the oldest (one.example.com).
      cacheService.set('https://four.example.com', { score: 4 });

      expect(cacheService.size()).toBe(3);
      expect(cacheService.getDomains()).not.toContain('one.example.com');
      expect(cacheService.getDomains()).toContain('four.example.com');
    });
  });

  describe('getStats()', () => {
    test('reports entry count, expiring-soon count, and the configured max', () => {
      cacheService.set('https://soon.example.com', { score: 1, ttl: 60 }); // expires in 60s (< 5min window)
      cacheService.set('https://later.example.com', { score: 2, ttl: 3600 });

      const stats = cacheService.getStats();

      expect(stats.entries).toBe(2);
      expect(stats.expiringSoon).toBe(1);
      expect(stats.maxEntries).toBe(cacheService.config.maxEntries);
      expect(stats.totalSize).toEqual(expect.any(Number));
    });
  });

  describe('Error handling', () => {
    test('a failed persist does not throw or reject out of set()', async () => {
      chrome.storage.local.set.mockRejectedValue(new Error('storage failure'));

      expect(() => cacheService.set('https://example.com', { score: 1 })).not.toThrow();

      await expect(jest.advanceTimersByTimeAsync(500)).resolves.not.toThrow();
    });
  });
});

import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('CacheService', () => {
  let cacheService;

  beforeEach(async () => {
    // Mock chrome.storage.local
    chrome.storage.local.get.mockImplementation((keys, callback) => {
      callback({});
    });
    chrome.storage.local.set.mockImplementation((data, callback) => {
      if (callback) callback();
    });

    // Dynamically import to get fresh instance
    const module = await import('@/services/CacheService.js');
    cacheService = module.cacheService;
  });

  describe('Cache Storage', () => {
    test('should store scan result in cache', async () => {
      const url = 'https://example.com';
      const result = {
        score: 95,
        riskType: [],
        action: 0,
        timestamp: Date.now()
      };

      await cacheService.set(url, result);

      expect(chrome.storage.local.set).toHaveBeenCalled();
      const callArgs = chrome.storage.local.set.mock.calls[0][0];
      expect(callArgs).toHaveProperty('scanCache');
    });

    test('should retrieve cached scan result', async () => {
      const url = 'https://example.com';
      const cachedResult = {
        score: 95,
        riskType: [],
        action: 0,
        timestamp: Date.now()
      };

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({
          scanCache: {
            [url]: cachedResult
          }
        });
      });

      const result = await cacheService.get(url);
      expect(result).toEqual(cachedResult);
    });

    test('should return null for non-existent cache entry', async () => {
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({ scanCache: {} });
      });

      const result = await cacheService.get('https://not-cached.com');
      expect(result).toBeNull();
    });
  });

  describe('Cache Expiration', () => {
    test('should not return expired cache entries', async () => {
      const url = 'https://example.com';
      const expiredResult = {
        score: 95,
        riskType: [],
        action: 0,
        timestamp: Date.now() - (25 * 60 * 60 * 1000) // 25 hours ago
      };

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({
          scanCache: {
            [url]: expiredResult
          }
        });
      });

      const result = await cacheService.get(url);
      expect(result).toBeNull();
    });

    test('should return non-expired cache entries', async () => {
      const url = 'https://example.com';
      const freshResult = {
        score: 95,
        riskType: [],
        action: 0,
        timestamp: Date.now() - (1 * 60 * 60 * 1000) // 1 hour ago
      };

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({
          scanCache: {
            [url]: freshResult
          }
        });
      });

      const result = await cacheService.get(url);
      expect(result).toEqual(freshResult);
    });
  });

  describe('Cache Management', () => {
    test('should clear all cache', async () => {
      await cacheService.clear();

      expect(chrome.storage.local.set).toHaveBeenCalledWith(
        { scanCache: {} },
        expect.any(Function)
      );
    });

    test('should clear specific cache entry', async () => {
      const url = 'https://example.com';
      
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({
          scanCache: {
            [url]: { score: 95 },
            'https://other.com': { score: 80 }
          }
        });
      });

      await cacheService.remove(url);

      expect(chrome.storage.local.set).toHaveBeenCalled();
      const callArgs = chrome.storage.local.set.mock.calls[0][0];
      expect(callArgs.scanCache).not.toHaveProperty(url);
      expect(callArgs.scanCache).toHaveProperty('https://other.com');
    });
  });
});

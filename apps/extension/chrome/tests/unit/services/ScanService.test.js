import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('ScanService', () => {
  let scanService;
  let mockConnectionService;
  let mockCacheService;

  beforeEach(async () => {
    // Mock dependencies
    mockConnectionService = {
      send: jest.fn(),
      onMessage: jest.fn(),
      isConnected: jest.fn(() => true)
    };

    mockCacheService = {
      get: jest.fn(() => null),
      set: jest.fn()
    };

    // Mock chrome APIs
    chrome.storage.local.get.mockImplementation((keys, callback) => {
      callback({});
    });
    chrome.storage.local.set.mockImplementation((data, callback) => {
      if (callback) callback();
    });

    // Mock modules
    jest.unstable_mockModule('@/services/ConnectionService.js', () => ({
      connectionService: mockConnectionService
    }));
    jest.unstable_mockModule('@/services/CacheService.js', () => ({
      cacheService: mockCacheService
    }));

    // Import after mocks are set up
    const module = await import('@/services/ScanService.js');
    scanService = module.scanService;
  });

  describe('URL Scanning', () => {
    test('should initiate URL scan', async () => {
      const url = 'https://example.com';
      const tabId = 123;

      await scanService.scanUrl(url, tabId);

      expect(mockConnectionService.send).toHaveBeenCalledWith(
        expect.objectContaining({
          type: expect.stringContaining('scan'),
          data: expect.objectContaining({ url })
        })
      );
    });

    test('should use cached result if available', async () => {
      const url = 'https://example.com';
      const cachedResult = {
        score: 95,
        riskType: [],
        action: 0,
        timestamp: Date.now()
      };

      mockCacheService.get.mockResolvedValue(cachedResult);

      const result = await scanService.scanUrl(url, 123);

      expect(result).toEqual(cachedResult);
      expect(mockConnectionService.send).not.toHaveBeenCalled();
    });

    test('should cache scan result after successful scan', async () => {
      const url = 'https://example.com';
      const scanResult = {
        score: 85,
        riskType: [1],
        action: 1
      };

      // Simulate scan response
      mockConnectionService.onMessage.mockImplementation((msgType, handler) => {
        if (msgType.includes('scan:response')) {
          setTimeout(() => handler(scanResult), 100);
        }
      });

      await scanService.scanUrl(url, 123);

      expect(mockCacheService.set).toHaveBeenCalledWith(
        url,
        expect.objectContaining({
          score: scanResult.score,
          riskType: scanResult.riskType
        })
      );
    });
  });

  describe('Page Info Collection', () => {
    test('should collect page information from tab', async () => {
      const tabId = 123;
      const pageInfo = {
        url: 'https://example.com',
        title: 'Example Page',
        fbPixels: ['123456789'],
        ga4: ['G-XXXXXXXX']
      };

      chrome.tabs.sendMessage.mockImplementation((id, msg, callback) => {
        callback(pageInfo);
      });

      const result = await scanService.collectPageInfo(tabId);

      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: expect.stringContaining('page:info:request')
        }),
        expect.any(Function)
      );
      expect(result).toEqual(pageInfo);
    });

    test('should handle page info collection errors', async () => {
      const tabId = 123;

      chrome.tabs.sendMessage.mockImplementation((id, msg, callback) => {
        callback(null);
      });

      const result = await scanService.collectPageInfo(tabId);

      expect(result).toBeNull();
    });
  });

  describe('Risk Assessment', () => {
    test('should correctly assess safe page (score >= 80)', () => {
      const result = {
        score: 95,
        riskType: [],
        action: 0
      };

      const assessment = scanService.assessRisk(result);

      expect(assessment.level).toBe('safe');
      expect(assessment.shouldBlock).toBe(false);
      expect(assessment.shouldWarn).toBe(false);
    });

    test('should correctly assess warning page (50 <= score < 80)', () => {
      const result = {
        score: 65,
        riskType: [1],
        action: 1
      };

      const assessment = scanService.assessRisk(result);

      expect(assessment.level).toBe('warning');
      expect(assessment.shouldWarn).toBe(true);
      expect(assessment.shouldBlock).toBe(false);
    });

    test('should correctly assess dangerous page (score < 50)', () => {
      const result = {
        score: 30,
        riskType: [1, 2],
        action: 2
      };

      const assessment = scanService.assessRisk(result);

      expect(assessment.level).toBe('danger');
      expect(assessment.shouldBlock).toBe(true);
    });

    test('should handle unknown risk types', () => {
      const result = {
        score: 40,
        riskType: [5], // Unknown risk
        action: 1
      };

      const assessment = scanService.assessRisk(result);

      expect(assessment.riskLabels).toContain('Unknown Risk');
    });
  });

  describe('Scan State Management', () => {
    test('should track scanning state', async () => {
      const url = 'https://example.com';

      await scanService.scanUrl(url, 123);

      expect(chrome.storage.local.set).toHaveBeenCalledWith(
        expect.objectContaining({
          currentPageScanning: true
        })
      );
    });

    test('should clear scanning state after completion', async () => {
      const url = 'https://example.com';
      const scanResult = { score: 95, riskType: [], action: 0 };

      mockConnectionService.onMessage.mockImplementation((msgType, handler) => {
        if (msgType.includes('scan:response')) {
          setTimeout(() => handler(scanResult), 100);
        }
      });

      await scanService.scanUrl(url, 123);

      // Wait for async completion
      await new Promise(resolve => setTimeout(resolve, 200));

      expect(chrome.storage.local.set).toHaveBeenCalledWith(
        expect.objectContaining({
          currentPageScanning: false
        })
      );
    });
  });

  describe('Error Handling', () => {
    test('should handle disconnected state', async () => {
      mockConnectionService.isConnected.mockReturnValue(false);

      const result = await scanService.scanUrl('https://example.com', 123);

      expect(result).toBeNull();
      expect(mockConnectionService.send).not.toHaveBeenCalled();
    });

    test('should handle scan timeout', async () => {
      jest.useFakeTimers();

      const scanPromise = scanService.scanUrl('https://example.com', 123);

      // Fast-forward time
      jest.advanceTimersByTime(31000); // 31 seconds (timeout is 30s)

      const result = await scanPromise;

      expect(result).toBeNull();

      jest.useRealTimers();
    });
  });
});

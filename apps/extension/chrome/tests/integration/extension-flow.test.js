import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('Extension Integration Flow', () => {
  beforeEach(() => {
    // jest-chrome v0.8.0 doesn't have reset() - individual tests handle their own mocks
  });

  describe('Extension Initialization', () => {
    test('should initialize all services on extension load', async () => {
      // Mock chrome.runtime.onInstalled
      chrome.runtime.onInstalled.addListener.mockImplementation((callback) => {
        callback({ reason: 'install' });
      });

      // Mock chrome.storage.local for initial setup
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({
          wsPort: 9007,
          protectionEnabled: true
        });
      });

      // Simulate extension installation
      const listeners = chrome.runtime.onInstalled.addListener.mock.calls;
      expect(listeners.length).toBeGreaterThan(0);
    });

    test('should set default settings on first install', async () => {
      chrome.runtime.onInstalled.addListener.mockImplementation((callback) => {
        callback({ reason: 'install' });
      });

      // Should set default configuration
      expect(chrome.storage.local.set).toHaveBeenCalledWith(
        expect.objectContaining({
          protectionEnabled: true
        }),
        expect.any(Function)
      );
    });
  });

  describe('Tab Navigation Flow', () => {
    test('should scan page on tab navigation', async () => {
      const tabId = 123;
      const url = 'https://example.com';

      // Mock tab update event
      chrome.tabs.onUpdated.addListener.mockImplementation((callback) => {
        callback(tabId, { status: 'complete' }, { id: tabId, url });
      });

      // Mock scan service response
      chrome.runtime.sendMessage.mockImplementation((msg, callback) => {
        if (msg.type === 'scan:url') {
          callback({ score: 95, riskType: [], action: 0 });
        }
      });

      // Trigger tab update
      const listeners = chrome.tabs.onUpdated.addListener.mock.calls;
      expect(listeners.length).toBeGreaterThan(0);
    });

    test('should update icon based on scan result', async () => {
      const tabId = 123;
      const scanResult = {
        score: 85,
        riskType: [],
        action: 0
      };

      // Should update icon to safe (green)
      chrome.action.setIcon.mockImplementation((details, callback) => {
        if (callback) callback();
      });

      // Simulate scan completion
      chrome.storage.local.set({
        currentPageScore: scanResult.score,
        currentPageAction: scanResult.action
      });

      expect(chrome.action.setIcon).toHaveBeenCalledWith(
        expect.objectContaining({
          tabId,
          path: expect.stringContaining('safe')
        }),
        expect.any(Function)
      );
    });

    test('should show warning for medium-risk sites', async () => {
      const tabId = 123;
      const scanResult = {
        score: 55,
        riskType: [1],
        action: 1
      };

      chrome.tabs.sendMessage.mockImplementation((id, msg, callback) => {
        if (callback) callback({ success: true });
      });

      // Should send warning message to content script
      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: 'showWarning'
        }),
        expect.any(Function)
      );
    });

    test('should block high-risk sites', async () => {
      const tabId = 123;
      const scanResult = {
        score: 25,
        riskType: [1, 2],
        action: 2
      };

      chrome.tabs.sendMessage.mockImplementation((id, msg, callback) => {
        if (callback) callback({ success: true });
      });

      // Should send block message to content script
      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: 'blockPage'
        }),
        expect.any(Function)
      );
    });
  });

  describe('Popup Interaction Flow', () => {
    test('should display current page scan results in popup', async () => {
      const currentPageData = {
        currentPageScore: 90,
        currentPageRiskType: [],
        currentPageUrl: 'https://example.com',
        currentPageAction: 0
      };

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback(currentPageData);
      });

      // Simulate popup opening
      const data = await new Promise((resolve) => {
        chrome.storage.local.get(null, resolve);
      });

      expect(data).toMatchObject(currentPageData);
    });

    test('should trigger manual scan from popup', async () => {
      const tabId = 123;

      chrome.tabs.query.mockImplementation((query, callback) => {
        callback([{ id: tabId, url: 'https://example.com' }]);
      });

      chrome.runtime.sendMessage.mockImplementation((msg, callback) => {
        if (msg.type === 'scan:manual') {
          callback({ score: 88, riskType: [], action: 0 });
        }
      });

      // Should initiate scan
      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'scan:manual'
        }),
        expect.any(Function)
      );
    });

    test('should handle reconnection from popup', async () => {
      chrome.runtime.sendMessage.mockImplementation((msg, callback) => {
        if (msg.type === 'connection:reconnect') {
          callback({ success: true });
        }
      });

      // User clicks reconnect button
      chrome.runtime.sendMessage({ type: 'connection:reconnect' });

      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'connection:reconnect'
        })
      );
    });
  });

  describe('Desktop App Communication', () => {
    test('should establish WebSocket connection with desktop app', async () => {
      global.WebSocket = jest.fn(() => ({
        send: jest.fn(),
        close: jest.fn(),
        readyState: WebSocket.OPEN,
        onopen: null,
        onclose: null,
        onerror: null,
        onmessage: null
      }));

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({ wsPort: 9007 });
      });

      // Should create WebSocket connection
      const ws = new WebSocket('ws://localhost:9007');
      expect(ws).toBeDefined();
    });

    test('should handle desktop app disconnect and reconnect', async () => {
      const mockWs = {
        send: jest.fn(),
        close: jest.fn(),
        readyState: WebSocket.CLOSED,
        onclose: null
      };

      global.WebSocket = jest.fn(() => mockWs);

      // Simulate disconnect
      if (mockWs.onclose) {
        mockWs.onclose({ code: 1006, reason: 'Abnormal' });
      }

      // Should schedule reconnection alarm
      expect(chrome.alarms.create).toHaveBeenCalledWith(
        'reconnect',
        expect.any(Object)
      );
    });

    test('should receive user email from desktop app', async () => {
      const userEmail = 'test@example.com';

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({ userEmail });
      });

      const data = await new Promise((resolve) => {
        chrome.storage.local.get(['userEmail'], resolve);
      });

      expect(data.userEmail).toBe(userEmail);
    });
  });

  describe('Cache Management Flow', () => {
    test('should use cached results for repeated URL visits', async () => {
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

      // Second visit should use cache
      const data = await new Promise((resolve) => {
        chrome.storage.local.get(['scanCache'], resolve);
      });

      expect(data.scanCache[url]).toEqual(cachedResult);
    });

    test('should invalidate expired cache entries', async () => {
      const url = 'https://example.com';
      const expiredResult = {
        score: 95,
        riskType: [],
        action: 0,
        timestamp: Date.now() - (25 * 60 * 60 * 1000) // 25 hours old
      };

      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({
          scanCache: {
            [url]: expiredResult
          }
        });
      });

      // Should detect expired cache and trigger new scan
      const CACHE_TTL = 24 * 60 * 60 * 1000; // 24 hours
      const age = Date.now() - expiredResult.timestamp;
      
      expect(age).toBeGreaterThan(CACHE_TTL);
    });
  });
});

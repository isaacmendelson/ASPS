import { beforeEach, describe, expect, jest, test } from '@jest/globals';

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
  get: jest.fn(),
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

describe('ScanService messaging v1', () => {
  beforeEach(() => {
    scanService.clearPending();
    jest.clearAllMocks();
    chrome.tabs.query.mockResolvedValue([]);
    chrome.storage.local.set.mockResolvedValue();
  });

  test('loads as an ES module and resolves a matching v1 result by requestId', () => {
    const requestId = '33333333-3333-4333-8333-333333333333';
    const correlationId = '22222222-2222-4222-8222-222222222222';
    const url = 'https://example.com/';
    const resolve = jest.fn();

    scanService.pendingScans.set(requestId, {
      resolve,
      timeoutId: setTimeout(() => {}, 10000),
      url,
      tabId: '12',
      correlationId
    });

    const result = scanService.handleResult({
      schemaVersion: '1.0',
      messageId: '44444444-4444-4444-8444-444444444444',
      correlationId,
      requestId,
      messageType: 'url_scan.result',
      sentAt: '2026-07-28T12:00:00.000Z',
      source: 'desktop',
      context: { deviceId: 'device-1', tabId: '12', url },
      outcome: {
        status: 'success',
        result: { score: 91, riskType: [], protectiveAction: 0, ttl: 60 }
      },
      payload: {}
    });

    expect(result).toEqual({
      score: 91,
      riskType: [],
      protectiveAction: 0,
      fromCache: false,
      tabId: 12
    });
    expect(resolve).toHaveBeenCalledWith(result);
    expect(scanService.pendingScans.has(requestId)).toBe(false);
  });

  test('resolves concurrent same-URL scans delivered in reverse order', () => {
    const url = 'https://example.com/';
    const first = {
      requestId: '33333333-3333-4333-8333-333333333333',
      correlationId: '22222222-2222-4222-8222-222222222222',
      tabId: '12',
      resolve: jest.fn()
    };
    const second = {
      requestId: '55555555-5555-4555-8555-555555555555',
      correlationId: '66666666-6666-4666-8666-666666666666',
      tabId: '13',
      resolve: jest.fn()
    };
    for (const pending of [first, second]) {
      scanService.pendingScans.set(pending.requestId, {
        ...pending,
        timeoutId: setTimeout(() => {}, 10000),
        url
      });
    }
    const resultEnvelope = (pending, messageId, score) => ({
      schemaVersion: '1.0',
      messageId,
      correlationId: pending.correlationId,
      requestId: pending.requestId,
      messageType: 'url_scan.result',
      sentAt: '2026-07-28T12:00:00.000Z',
      source: 'desktop',
      context: { deviceId: 'device-1', tabId: pending.tabId, url },
      outcome: {
        status: 'success',
        result: { score, riskType: [], protectiveAction: 0, ttl: 60 }
      },
      payload: {}
    });

    scanService.handleResult(resultEnvelope(
      second, '77777777-7777-4777-8777-777777777777', 82));
    scanService.handleResult(resultEnvelope(
      first, '44444444-4444-4444-8444-444444444444', 91));

    expect(second.resolve).toHaveBeenCalledWith(
      expect.objectContaining({ score: 82 }));
    expect(first.resolve).toHaveBeenCalledWith(
      expect.objectContaining({ score: 91 }));
    expect(scanService.pendingScans.size).toBe(0);
  });
});

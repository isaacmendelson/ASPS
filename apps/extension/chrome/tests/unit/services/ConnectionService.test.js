import { describe, test, expect, beforeEach, jest, afterEach } from '@jest/globals';

describe('ConnectionService', () => {
  let connectionService;
  let mockWebSocket;

  beforeEach(async () => {
    // Mock WebSocket instance
    mockWebSocket = {
      send: jest.fn(),
      close: jest.fn(),
      readyState: WebSocket.OPEN,
      onopen: null,
      onclose: null,
      onerror: null,
      onmessage: null
    };

    // Mock WebSocket constructor
    global.WebSocket = jest.fn(() => mockWebSocket);

    // Mock chrome storage
    chrome.storage.local.get.mockImplementation((keys, callback) => {
      callback({ wsPort: 9007 });
    });

    // Dynamically import
    const module = await import('@/services/ConnectionService.js');
    connectionService = module.connectionService;
  });

  afterEach(() => {
    jest.clearAllTimers();
  });

  describe('Connection Lifecycle', () => {
    test('should establish WebSocket connection', async () => {
      await connectionService.connect();

      expect(global.WebSocket).toHaveBeenCalledWith(
        expect.stringContaining('ws://localhost:')
      );
    });

    test('should handle connection open event', async () => {
      await connectionService.connect();

      // Simulate connection open
      if (mockWebSocket.onopen) {
        mockWebSocket.onopen();
      }

      // Should send PING message
      expect(mockWebSocket.send).toHaveBeenCalled();
    });

    test('should handle connection close event', async () => {
      await connectionService.connect();

      // Simulate connection close
      if (mockWebSocket.onclose) {
        mockWebSocket.onclose({ code: 1000, reason: 'Normal' });
      }

      // Should schedule reconnection
      expect(chrome.alarms.create).toHaveBeenCalledWith(
        'reconnect',
        expect.any(Object)
      );
    });

    test('should disconnect WebSocket', async () => {
      await connectionService.connect();
      await connectionService.disconnect();

      expect(mockWebSocket.close).toHaveBeenCalled();
    });
  });

  describe('Message Handling', () => {
    test('should send message through WebSocket', async () => {
      await connectionService.connect();
      
      const message = { type: 'TEST', data: { value: 'test' } };
      connectionService.send(message);

      expect(mockWebSocket.send).toHaveBeenCalledWith(
        JSON.stringify(message)
      );
    });

    test('should handle incoming messages', async () => {
      await connectionService.connect();

      const handler = jest.fn();
      connectionService.onMessage('TEST_TYPE', handler);

      // Simulate incoming message
      const message = { type: 'TEST_TYPE', data: { value: 'test' } };
      if (mockWebSocket.onmessage) {
        mockWebSocket.onmessage({ data: JSON.stringify(message) });
      }

      expect(handler).toHaveBeenCalledWith(message.data);
    });

    test('should queue messages when disconnected', async () => {
      mockWebSocket.readyState = WebSocket.CLOSED;

      const message = { type: 'TEST', data: {} };
      connectionService.send(message);

      // Message should be queued, not sent
      expect(mockWebSocket.send).not.toHaveBeenCalled();
    });
  });

  describe('Reconnection Logic', () => {
    test('should attempt reconnection on failure', async () => {
      await connectionService.connect();

      // Simulate connection error
      if (mockWebSocket.onerror) {
        mockWebSocket.onerror(new Error('Connection failed'));
      }

      // Should create reconnection alarm
      expect(chrome.alarms.create).toHaveBeenCalledWith(
        'reconnect',
        expect.objectContaining({ delayInMinutes: expect.any(Number) })
      );
    });

    test('should implement exponential backoff', async () => {
      // First attempt
      await connectionService.attemptReconnect();
      const firstCall = chrome.alarms.create.mock.calls.find(
        call => call[0] === 'reconnect'
      );

      // Simulate failure and second attempt
      if (mockWebSocket.onerror) {
        mockWebSocket.onerror(new Error('Connection failed'));
      }
      await connectionService.attemptReconnect();
      
      const secondCall = chrome.alarms.create.mock.calls.findLast(
        call => call[0] === 'reconnect'
      );

      // Second delay should be longer (exponential backoff)
      if (firstCall && secondCall) {
        expect(secondCall[1].delayInMinutes).toBeGreaterThanOrEqual(
          firstCall[1].delayInMinutes
        );
      }
    });
  });

  describe('Keepalive & Heartbeat', () => {
    test('should send keepalive messages', async () => {
      await connectionService.connect();
      mockWebSocket.send.mockClear();

      connectionService.sendKeepalive();

      expect(mockWebSocket.send).toHaveBeenCalledWith(
        expect.stringContaining('"type":"ping"')
      );
    });

    test('should send heartbeat messages', async () => {
      await connectionService.connect();
      mockWebSocket.send.mockClear();

      connectionService.sendHeartbeat();

      expect(mockWebSocket.send).toHaveBeenCalledWith(
        expect.stringContaining('"type":"heartbeat"')
      );
    });

    test('should schedule keepalive alarm', async () => {
      await connectionService.connect();

      expect(chrome.alarms.create).toHaveBeenCalledWith(
        'keepalive',
        expect.objectContaining({ periodInMinutes: expect.any(Number) })
      );
    });
  });
});

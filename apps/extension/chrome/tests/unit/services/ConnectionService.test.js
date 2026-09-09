import { beforeEach, afterEach, describe, expect, jest, test } from '@jest/globals';

// ============================================
// ConnectionService unit tests — ASPS-757 rewrite
//
// NOTE: The previous version of this file mocked a *single* static
// WebSocket instance (`global.WebSocket = jest.fn(() => mockWebSocket)`)
// that never fired `onopen`/`onerror`/`onclose` on its own. The real
// ConnectionService.connect() (services/ConnectionService.js) scans a
// *list* of ports ([8080, 8181, 8282, 8383, 8484]), constructing a new
// `WebSocket` per port attempt inside `tryPort()` and racing its `onopen`/
// `onerror` against a 2000ms `connectionTimeout` (via `setTimeout`) — a
// port only "succeeds" when `onopen` fires, and `tryPort()` only resolves
// (with `null`, advancing to the next port) when `onerror` fires or the
// timeout elapses. Because the old mock never called any of those handlers
// and the tests used real timers, `await connectionService.connect()` hung
// until every port's real 2000ms timeout fired (5 ports x 2s = 10s),
// blowing past Jest's default 5s test timeout — the "hang" described in
// ASPS-757.
//
// This rewrite replaces the static mock with a scriptable `MockWebSocket`
// that models the real multi-port handshake per port: a port can be
// configured to 'open' (fires `onopen` on a real microtask, mirroring an
// async WebSocket connect), 'error' (fires `onerror`), or 'timeout' (fires
// neither, relying on ConnectionService's own `connectionTimeout` via
// `setTimeout`). `jest.useFakeTimers()` is used for every test so the
// `connectionTimeout`/reconnect/ping/keepalive/heartbeat `setInterval`s
// never fire on real wall-clock time; the one test that exercises the
// timeout path explicitly advances fake time with
// `jest.advanceTimersByTimeAsync()`. Because the 'open'/'error' outcomes
// are driven by a genuine native Promise microtask (not `queueMicrotask`,
// which Jest's modern fake timers *do* fake), `await connectionService
// .connect()` resolves deterministically without any timer advance at all
// for the non-timeout paths — this is what avoids the hang.
//
// Bootstrap mirrors the currently-GREEN CacheService.test.js / ScanService
// .test.js rewrites (ASPS-756 / ASPS-753): StateManager and
// MessageQueueService are mocked via jest.unstable_mockModule (their real
// singletons would pollute assertions with their own persistence/queueing
// side effects), and ConnectionService is imported once at module scope
// since it is a singleton — state is reset per test instead of
// re-importing.
// ============================================

const stateManager = {
  set: jest.fn(),
  update: jest.fn(),
  get: jest.fn()
};

const messageQueueService = {
  enqueue: jest.fn(),
  dequeueOne: jest.fn(() => null),
  clear: jest.fn(),
  hasMessages: false,
  size: 0
};

jest.unstable_mockModule('../../../state/StateManager.js', () => ({
  default: stateManager
}));
jest.unstable_mockModule('../../../services/MessageQueueService.js', () => ({
  messageQueueService
}));

const { connectionService } = await import('../../../services/ConnectionService.js');
const { MSG } = await import('../../../messaging/MessageTypes.js');

// ---- Scriptable multi-port WebSocket mock -------------------------------
// `portScript` maps a port number to an outcome:
//   'open'    -> fires onopen() on a native microtask (successful connect)
//   'error'   -> fires onerror() on a native microtask (port unavailable)
//   'timeout' -> fires neither; the real connectionTimeout setTimeout in
//                ConnectionService.tryPort() is what resolves this port.
// Any port not present in the script defaults to 'error' so an
// unconfigured port never hangs a test.
let portScript;
let createdSockets;

class MockWebSocket {
  constructor(url) {
    this.url = url;
    this.readyState = MockWebSocket.CONNECTING;
    this.onopen = null;
    this.onclose = null;
    this.onerror = null;
    this.onmessage = null;
    this.send = jest.fn();
    this.close = jest.fn(() => {
      this.readyState = MockWebSocket.CLOSED;
    });

    createdSockets.push(this);

    const port = Number(new URL(url).port);
    const outcome = portScript[port] ?? 'error';

    if (outcome === 'open') {
      // Native Promise microtask (NOT queueMicrotask, which Jest's modern
      // fake timers fake) — resolves deterministically without a timer
      // advance, matching real async WebSocket connect semantics.
      Promise.resolve().then(() => {
        this.readyState = MockWebSocket.OPEN;
        this.onopen?.();
      });
    } else if (outcome === 'error') {
      Promise.resolve().then(() => {
        this.onerror?.(new Event('error'));
      });
    }
    // 'timeout' -> intentionally left pending; ConnectionService's own
    // connectionTimeout setTimeout (config.connectionTimeout = 2000ms)
    // calls ws.close() and resolves the port as failed.
  }

  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSING = 2;
  static CLOSED = 3;
}

function configurePorts(script) {
  portScript = script;
}

describe('ConnectionService', () => {
  let savedPortStore;

  beforeEach(() => {
    jest.useFakeTimers();
    jest.clearAllMocks();
    jest.restoreAllMocks();

    portScript = {};
    createdSockets = [];
    savedPortStore = {};

    global.WebSocket = MockWebSocket;
    // WebSocket.OPEN etc are referenced by ConnectionService (e.g.
    // `this.websocket.readyState === WebSocket.OPEN`) against the global
    // constructor, so they must resolve to the same numeric constants the
    // mock instances use.
    global.WebSocket.OPEN = MockWebSocket.OPEN;
    global.WebSocket.CONNECTING = MockWebSocket.CONNECTING;
    global.WebSocket.CLOSING = MockWebSocket.CLOSING;
    global.WebSocket.CLOSED = MockWebSocket.CLOSED;

    // Reset singleton state between tests (ConnectionService is a true
    // singleton — re-importing would return the same cached instance).
    connectionService.websocket = null;
    connectionService.messageHandlers = new Map();
    connectionService.missedHeartbeats = 0;
    connectionService.deviceIpAddress = null;
    connectionService.pingTimer = null;
    connectionService.keepaliveTimer = null;
    connectionService.heartbeatTimer = null;

    // getSavedPort()/savePort() use callback-style chrome.storage.local
    // get/set — jest-chrome's stubs have no default implementation, so
    // without this, connect() would hang waiting on an un-invoked callback
    // (part of why the original suite hung).
    // Supports both call shapes ConnectionService uses: callback-style
    // (getSavedPort()) and Promise-style, un-awaited callback
    // (sendStoredEmail() -> chrome.storage.local.get(['userEmail'])).
    chrome.storage.local.get.mockImplementation((keys, callback) => {
      const result = { ...savedPortStore };
      if (typeof callback === 'function') {
        callback(result);
        return undefined;
      }
      return Promise.resolve(result);
    });
    chrome.storage.local.set.mockImplementation((obj, callback) => {
      Object.assign(savedPortStore, obj);
      callback && callback();
    });

    chrome.alarms.create.mockResolvedValue();
    chrome.alarms.clear.mockResolvedValue();

    stateManager.get.mockReturnValue(undefined);
  });

  afterEach(() => {
    jest.clearAllTimers();
    jest.useRealTimers();
  });

  describe('Multi-port connect flow', () => {
    test('connects on the first port when it opens successfully', async () => {
      configurePorts({ 8080: 'open' });

      const result = await connectionService.connect();

      expect(result).toBe(true);
      expect(createdSockets).toHaveLength(1);
      expect(createdSockets[0].url).toBe('ws://localhost:8080');
      expect(stateManager.update).toHaveBeenCalledWith({
        'connection.desktop': true,
        'connection.port': 8080,
        'connection.reconnectAttempts': 0,
        'connection.reconnecting': false
      });
    });

    test('falls back through failing ports until one opens', async () => {
      configurePorts({ 8080: 'error', 8181: 'error', 8282: 'open' });

      const result = await connectionService.connect();

      expect(result).toBe(true);
      expect(createdSockets.map((s) => s.url)).toEqual([
        'ws://localhost:8080',
        'ws://localhost:8181',
        'ws://localhost:8282'
      ]);
      expect(stateManager.update).toHaveBeenCalledWith(
        expect.objectContaining({ 'connection.port': 8282 })
      );
    });

    test('tries the saved port first and skips the full port scan when it opens', async () => {
      savedPortStore.connectedPort = 8383;
      configurePorts({ 8383: 'open' });

      const result = await connectionService.connect();

      expect(result).toBe(true);
      expect(createdSockets).toHaveLength(1);
      expect(createdSockets[0].url).toBe('ws://localhost:8383');
      // The saved-port success path does not go through savePort() again —
      // only the full-scan branch does.
      expect(chrome.storage.local.set).not.toHaveBeenCalled();
    });

    test('falls back to the full port scan when the saved port fails to open', async () => {
      savedPortStore.connectedPort = 8080;
      configurePorts({ 8080: 'error', 8181: 'open' });

      const result = await connectionService.connect();

      expect(result).toBe(true);
      // Real (if a little redundant) current behavior: the saved-port
      // attempt at 8080 is not excluded from the subsequent full scan, so
      // 8080 is tried twice before 8181 succeeds.
      expect(createdSockets.map((s) => s.url)).toEqual([
        'ws://localhost:8080',
        'ws://localhost:8080',
        'ws://localhost:8181'
      ]);
      expect(chrome.storage.local.set).toHaveBeenCalledWith(
        { connectedPort: 8181 },
        expect.any(Function)
      );
    });

    test('returns false and marks the connection disconnected when every port fails', async () => {
      configurePorts({}); // every configured port defaults to 'error'

      const result = await connectionService.connect();

      expect(result).toBe(false);
      expect(createdSockets).toHaveLength(5);
      expect(stateManager.update).toHaveBeenLastCalledWith({
        'connection.desktop': false,
        'connection.port': null
      });
    });

    test('treats a port with no response as a timeout and advances to the next port', async () => {
      configurePorts({ 8080: 'timeout', 8181: 'open' });

      const connectPromise = connectionService.connect();
      // Drives ConnectionService's own connectionTimeout (2000ms) forward;
      // the 'open' resolution on 8181 is a native microtask flushed as
      // part of the async timer advance.
      await jest.advanceTimersByTimeAsync(2000);

      const result = await connectPromise;

      expect(result).toBe(true);
      expect(createdSockets.map((s) => s.url)).toEqual([
        'ws://localhost:8080',
        'ws://localhost:8181'
      ]);
      expect(createdSockets[0].close).toHaveBeenCalled();
    });
  });

  describe('setupConnection lifecycle', () => {
    test('a successful connect sends the state-sync request and an initial ping', async () => {
      configurePorts({ 8080: 'open' });

      await connectionService.connect();
      const ws = createdSockets[0];

      const sentTypes = ws.send.mock.calls.map((call) => JSON.parse(call[0]).type);
      expect(sentTypes).toContain(MSG.WS_STATE_SYNC_REQUEST);
      expect(sentTypes).toContain(MSG.WS_PING);
    });

    test('isConnected() reflects the live websocket readyState', async () => {
      // Real behavior: `this.websocket && this.websocket.readyState ===
      // WebSocket.OPEN` returns `null` (not `false`) when there is no
      // socket yet — assert falsy rather than strict `false`.
      expect(connectionService.isConnected()).toBeFalsy();

      configurePorts({ 8080: 'open' });
      await connectionService.connect();

      expect(connectionService.isConnected()).toBe(true);
    });
  });

  describe('Message handling', () => {
    async function connectOnFirstPort() {
      configurePorts({ 8080: 'open' });
      await connectionService.connect();
      return createdSockets[0];
    }

    test('dispatches parsed messages to handlers registered for that type', async () => {
      const ws = await connectOnFirstPort();
      const handler = jest.fn();
      connectionService.onMessage('scan:result', handler);

      const message = { type: 'scan:result', data: { score: 10 } };
      ws.onmessage({ data: JSON.stringify(message) });

      expect(handler).toHaveBeenCalledWith(message);
    });

    test('wildcard handlers receive every message with (data, type)', async () => {
      const ws = await connectOnFirstPort();
      const handler = jest.fn();
      connectionService.onMessage('*', handler);

      const message = { type: 'custom_event', foo: 'bar' };
      ws.onmessage({ data: JSON.stringify(message) });

      expect(handler).toHaveBeenCalledWith(message, 'custom_event');
    });

    test('the unsubscribe function returned by onMessage removes the handler', async () => {
      const ws = await connectOnFirstPort();
      const handler = jest.fn();
      const unsubscribe = connectionService.onMessage('scan:result', handler);
      unsubscribe();

      ws.onmessage({ data: JSON.stringify({ type: 'scan:result' }) });

      expect(handler).not.toHaveBeenCalled();
    });

    test('intercepts heartbeat_pong before it reaches registered handlers and resets the missed count', async () => {
      const ws = await connectOnFirstPort();
      connectionService.missedHeartbeats = 2;
      const handler = jest.fn();
      connectionService.onMessage('heartbeat_pong', handler);

      ws.onmessage({ data: JSON.stringify({ type: 'heartbeat_pong' }) });

      expect(handler).not.toHaveBeenCalled();
      expect(connectionService.missedHeartbeats).toBe(0);
    });

    test('malformed JSON on an incoming message is caught and does not throw or dispatch', async () => {
      const ws = await connectOnFirstPort();
      const handler = jest.fn();
      connectionService.onMessage('*', handler);

      expect(() => ws.onmessage({ data: 'not-json' })).not.toThrow();
      expect(handler).not.toHaveBeenCalled();
    });
  });

  describe('send() / queueing', () => {
    test('send() writes JSON over the socket when it is open', async () => {
      configurePorts({ 8080: 'open' });
      await connectionService.connect();
      const ws = createdSockets[0];
      ws.send.mockClear();

      const message = { type: 'TEST', data: { value: 'test' } };
      const result = connectionService.send(message);

      expect(result).toBe(true);
      expect(ws.send).toHaveBeenCalledWith(JSON.stringify(message));
      expect(messageQueueService.enqueue).not.toHaveBeenCalled();
    });

    test('send() queues the message via messageQueueService when there is no open socket', () => {
      const message = { type: 'TEST', data: {} };

      const result = connectionService.send(message);

      expect(result).toBe(false);
      expect(messageQueueService.enqueue).toHaveBeenCalledWith(message);
    });
  });

  describe('disconnect() / reconnect()', () => {
    test('disconnect() closes the socket, clears alarms, and clears the message queue', async () => {
      configurePorts({ 8080: 'open' });
      await connectionService.connect();
      const ws = createdSockets[0];

      await connectionService.disconnect();

      expect(ws.close).toHaveBeenCalled();
      expect(chrome.alarms.clear).toHaveBeenCalledWith('reconnect');
      expect(chrome.alarms.clear).toHaveBeenCalledWith('keepalive');
      expect(messageQueueService.clear).toHaveBeenCalled();
      expect(connectionService.websocket).toBeNull();
      expect(stateManager.update).toHaveBeenLastCalledWith({
        'connection.desktop': false,
        'connection.port': null
      });
    });

    test('reconnect() force-closes any existing socket and re-invokes connect()', async () => {
      configurePorts({ 8080: 'open' });
      await connectionService.connect();
      const ws = createdSockets[0];

      const connectSpy = jest.spyOn(connectionService, 'connect').mockResolvedValue(true);

      const result = await connectionService.reconnect();

      expect(ws.close).toHaveBeenCalled();
      expect(stateManager.set).toHaveBeenCalledWith('connection.reconnectAttempts', 0);
      expect(connectSpy).toHaveBeenCalledTimes(1);
      expect(result).toBe(true);
    });
  });

  describe('Reconnection scheduling', () => {
    test('the first reconnect attempt (0 prior attempts) triggers an immediate attemptReconnect() instead of scheduling an alarm', async () => {
      stateManager.get.mockReturnValue(0);
      const attemptReconnectSpy = jest
        .spyOn(connectionService, 'attemptReconnect')
        .mockImplementation(() => {});

      await connectionService.scheduleReconnect();

      expect(attemptReconnectSpy).toHaveBeenCalledTimes(1);
      expect(chrome.alarms.create).not.toHaveBeenCalledWith('reconnect', expect.anything());
    });

    test('onclose with prior reconnect attempts schedules an exponential-backoff alarm', async () => {
      configurePorts({ 8080: 'open' });
      await connectionService.connect();
      const ws = createdSockets[0];

      stateManager.get.mockReturnValue(2); // attempts=2 -> delayMs = 1000 * 2^1 = 2000ms -> 0.5min (floor)
      ws.onclose({ code: 1006, reason: 'lost' });

      expect(chrome.alarms.create).toHaveBeenCalledWith(
        'reconnect',
        expect.objectContaining({ delayInMinutes: expect.any(Number) })
      );
      expect(stateManager.update).toHaveBeenCalledWith({
        'connection.desktop': false,
        'connection.port': null
      });
    });
  });
});

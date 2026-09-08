// Jest setup for Chrome Extension testing
Object.assign(global, require('jest-chrome'));

// jest-environment-jsdom's window.crypto does not implement randomUUID()
// (jsdom's WebCrypto surface is incomplete). Production code
// (generated/messaging/v1/message-envelope.js, used by ScanService.scan())
// calls crypto.randomUUID() directly, so polyfill it from Node's real
// implementation rather than mocking/weakening the code under test.
const nodeCrypto = require('crypto');
if (global.crypto && typeof global.crypto.randomUUID !== 'function') {
  global.crypto.randomUUID = () => nodeCrypto.randomUUID();
}

// jest-chrome does not include chrome.storage.session (MV3-only API).
// Add a minimal jest.fn()-based mock so tests that exercise MessageQueueService
// (which uses storage.session for queue persistence) can run.
if (global.chrome && global.chrome.storage && !global.chrome.storage.session) {
  global.chrome.storage.session = {
    set: jest.fn(() => Promise.resolve()),
    get: jest.fn(() => Promise.resolve({})),
    remove: jest.fn(() => Promise.resolve()),
    clear: jest.fn(() => Promise.resolve()),
  };
}

// Mock window.location
if (typeof window !== 'undefined') {
  delete window.location;
  window.location = {
    hostname: 'example.com',
    href: 'https://example.com/page'
  };
}

// Mock localStorage
const localStorageMock = {
  getItem: () => null,
  setItem: () => {},
  removeItem: () => {},
  clear: () => {},
};
global.localStorage = localStorageMock;

// Mock WebSocket
global.WebSocket = class WebSocket {
  constructor(url) {
    this.url = url;
    this.readyState = WebSocket.CONNECTING;
    this.onopen = null;
    this.onclose = null;
    this.onerror = null;
    this.onmessage = null;
  }

  send(data) {
    // Mock implementation
  }

  close() {
    this.readyState = WebSocket.CLOSED;
    if (this.onclose) {
      this.onclose({ code: 1000, reason: 'Normal closure' });
    }
  }

  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSING = 2;
  static CLOSED = 3;
};

// Mock fetch
global.fetch = () =>
  Promise.resolve({
    ok: true,
    json: () => Promise.resolve({}),
    text: () => Promise.resolve(''),
  });

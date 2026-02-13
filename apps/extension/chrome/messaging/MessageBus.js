// ============================================
// AntiScam Extension - Message Bus
// Centralized message handling and routing
// ============================================

import { MSG } from './MessageTypes.js';

class MessageBus {
  constructor() {
    this.handlers = new Map();
    this.middlewares = [];
    this.initialized = false;
  }

  // Initialize the message bus
  init() {
    if (this.initialized) return;

    chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
      this.handleMessage(message, sender, sendResponse);
      return true; // Keep channel open for async responses
    });

    this.initialized = true;
    console.log('[MessageBus] Initialized');
  }

  // Register a handler for a message type
  on(type, handler) {
    if (!this.handlers.has(type)) {
      this.handlers.set(type, new Set());
    }
    this.handlers.get(type).add(handler);

    // Return unsubscribe function
    return () => {
      this.handlers.get(type)?.delete(handler);
    };
  }

  // Register multiple handlers at once
  registerHandlers(handlerMap) {
    const unsubscribes = [];
    for (const [type, handler] of Object.entries(handlerMap)) {
      unsubscribes.push(this.on(type, handler));
    }
    return () => unsubscribes.forEach(fn => fn());
  }

  // Add middleware
  use(middleware) {
    this.middlewares.push(middleware);
    return () => {
      const index = this.middlewares.indexOf(middleware);
      if (index > -1) {
        this.middlewares.splice(index, 1);
      }
    };
  }

  // Handle incoming message
  async handleMessage(message, sender, sendResponse) {
    const { type, payload } = this.normalizeMessage(message);

    console.log('[MessageBus] Received:', type);

    // Run middlewares
    for (const middleware of this.middlewares) {
      try {
        const shouldContinue = await middleware(type, payload, sender);
        if (shouldContinue === false) {
          sendResponse({ blocked: true });
          return;
        }
      } catch (e) {
        console.error('[MessageBus] Middleware error:', e);
      }
    }

    // Get handlers
    const handlers = this.handlers.get(type);

    if (!handlers || handlers.size === 0) {
      console.warn('[MessageBus] No handler for:', type);
      sendResponse({ error: `No handler for message type: ${type}` });
      return;
    }

    // Execute handlers
    try {
      const results = [];
      for (const handler of handlers) {
        const result = await handler(payload, sender);
        if (result !== undefined) {
          results.push(result);
        }
      }

      // Return first result or aggregate
      sendResponse(results.length === 1 ? results[0] : { results });
    } catch (e) {
      console.error('[MessageBus] Handler error:', e);
      sendResponse({ error: e.message });
    }
  }

  // Normalize message format
  normalizeMessage(message) {
    if (typeof message === 'string') {
      return { type: message, payload: {} };
    }

    if (message.type) {
      const { type, ...payload } = message;
      return { type, payload };
    }

    return { type: 'unknown', payload: message };
  }

  // Send message to background script
  async send(type, payload = {}) {
    try {
      return await chrome.runtime.sendMessage({ type, ...payload });
    } catch (e) {
      console.error('[MessageBus] Send error:', e);
      throw e;
    }
  }

  // Send message to a specific tab
  async sendToTab(tabId, type, payload = {}) {
    try {
      return await chrome.tabs.sendMessage(tabId, { type, ...payload });
    } catch (e) {
      console.error('[MessageBus] SendToTab error:', e);
      throw e;
    }
  }

  // Send message to active tab
  async sendToActiveTab(type, payload = {}) {
    try {
      const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
      if (tabs[0]) {
        return await this.sendToTab(tabs[0].id, type, payload);
      }
      throw new Error('No active tab');
    } catch (e) {
      console.error('[MessageBus] SendToActiveTab error:', e);
      throw e;
    }
  }

  // Broadcast to all tabs
  async broadcast(type, payload = {}) {
    try {
      const tabs = await chrome.tabs.query({});
      const results = [];

      for (const tab of tabs) {
        if (tab.url?.startsWith('http')) {
          try {
            const result = await this.sendToTab(tab.id, type, payload);
            results.push({ tabId: tab.id, result });
          } catch (e) {
            // Tab might not have content script
          }
        }
      }

      return results;
    } catch (e) {
      console.error('[MessageBus] Broadcast error:', e);
      throw e;
    }
  }
}

// Singleton instance
export const messageBus = new MessageBus();
export default messageBus;

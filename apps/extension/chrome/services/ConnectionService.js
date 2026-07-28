// ============================================
// AntiScam Extension - Connection Service
// Manages WebSocket connection to desktop app
// ============================================

import stateManager from '../state/StateManager.js';
import { MSG } from '../messaging/MessageTypes.js';
import { messageQueueService } from './MessageQueueService.js';

class ConnectionService {
  constructor() {
    this.websocket = null;
    this.config = {
      ports: [8080, 8181, 8282, 8383, 8484],
      reconnectDelay: 5000,
      maxReconnectAttempts: 10,
      pingInterval: 30000,
      connectionTimeout: 2000,
      keepaliveInterval: 20000,  // 20 seconds - within 30s service worker window
      heartbeatInterval: 10000,  // 10 seconds (user decision)
      maxMissedHeartbeats: 3     // Dead after 3 missed (30 seconds total - user decision)
    };
    this.pingTimer = null;
    this.keepaliveTimer = null;
    this.heartbeatTimer = null;
    this.missedHeartbeats = 0;
    this.messageHandlers = new Map();
    this.deviceIpAddress = null;   // IP address of the local device (received from agent)
  }

  // Store device IP address received from the desktop agent
  setDeviceIpAddress(ip) {
    this.deviceIpAddress = ip || null;
  }

  // Get stored device IP address
  getDeviceIpAddress() {
    return this.deviceIpAddress || '';
  }

  // Register handler for WebSocket messages
  onMessage(type, handler) {
    if (!this.messageHandlers.has(type)) {
      this.messageHandlers.set(type, new Set());
    }
    this.messageHandlers.get(type).add(handler);

    return () => {
      this.messageHandlers.get(type)?.delete(handler);
    };
  }

  // Connect to desktop app
  async connect() {
    console.log('[ConnectionService] Attempting to connect...');

    // Try saved port first
    const savedPort = await this.getSavedPort();
    if (savedPort) {
      const result = await this.tryPort(savedPort);
      if (result) {
        await this.setupConnection(result.ws, result.port);
        return true;
      }
    }

    // Try all ports
    for (const port of this.config.ports) {
      const result = await this.tryPort(port);
      if (result) {
        await this.savePort(port);
        await this.setupConnection(result.ws, result.port);
        return true;
      }
    }

    console.log('[ConnectionService] Could not connect to desktop app');
    this.updateDisconnectedState();
    return false;
  }

  // Try connecting to a specific port
  async tryPort(port) {
    return new Promise((resolve) => {
      try {
        console.log(`[ConnectionService] Trying port ${port}...`);
        const ws = new WebSocket(`ws://localhost:${port}`);

        const timeout = setTimeout(() => {
          ws.close();
          resolve(null);
        }, this.config.connectionTimeout);

        ws.onopen = () => {
          clearTimeout(timeout);
          console.log(`[ConnectionService] Port ${port} connected!`);
          resolve({ ws, port });
        };

        ws.onerror = () => {
          clearTimeout(timeout);
          resolve(null);
        };
      } catch (e) {
        resolve(null);
      }
    });
  }

  // Setup WebSocket connection handlers
  async setupConnection(ws, port) {
    this.websocket = ws;

    // Update state
    stateManager.update({
      'connection.desktop': true,
      'connection.port': port,
      'connection.reconnectAttempts': 0,
      'connection.reconnecting': false  // Clear reconnecting state on successful connection
    });

    console.log('[ConnectionService] WebSocket connected, setting up handlers...');

    ws.onclose = () => {
      console.log('[ConnectionService] Connection closed');
      this.handleDisconnect();
    };

    ws.onerror = (error) => {
      console.error('[ConnectionService] WebSocket error:', error);
    };

    ws.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);

        // Handle heartbeat pong specially
        if (data.type === 'heartbeat_pong') {
          this.receivedHeartbeatPong();
          return; // Don't propagate to other handlers
        }

        this.handleMessage(data);
      } catch (e) {
        console.error('[ConnectionService] Error parsing message:', e);
      }
    };

    // Start ping interval
    this.startPing();

    // Start keepalive to keep service worker alive
    this.startKeepalive();

    // Create alarm-based keepalive backup (survives SW termination)
    chrome.alarms.create('keepalive', { periodInMinutes: 0.5 });

    // Start heartbeat for dead connection detection
    this.startHeartbeat();

    // Flush queued messages
    await this.flushQueue();

    // Re-send stored email to agent on every connect/reconnect
    await this.sendStoredEmail();

    // Request authoritative state snapshot from the agent so the extension
    // can reconcile drift caused by a service-worker restart mid-session.
    // The agent responds by re-pushing WS_IMMEDIATE_DANGER_STARTED/_ENDED,
    // WS_SET_REMOTE_CONTROLLED, and tracked_domains:set as appropriate.
    this.send({ type: MSG.WS_STATE_SYNC_REQUEST });

    // Send initial ping
    this.send({ type: MSG.WS_PING });
  }

  // Handle incoming WebSocket message
  handleMessage(data) {
    const type = data.type || data.jsonTypeName;
    console.log('[ConnectionService] Received:', type);

    const handlers = this.messageHandlers.get(type);
    if (handlers) {
      handlers.forEach(handler => {
        try {
          handler(data);
        } catch (e) {
          console.error('[ConnectionService] Handler error:', e);
        }
      });
    }

    // Also trigger wildcard handlers
    const wildcardHandlers = this.messageHandlers.get('*');
    if (wildcardHandlers) {
      wildcardHandlers.forEach(handler => {
        try {
          handler(data, type);
        } catch (e) {
          console.error('[ConnectionService] Wildcard handler error:', e);
        }
      });
    }
  }

  // Handle disconnect
  handleDisconnect() {
    this.websocket = null;
    this.stopPing();
    this.stopKeepalive();
    chrome.alarms.clear('keepalive');
    this.stopHeartbeat();
    this.updateDisconnectedState();
    this.scheduleReconnect();
  }

  // Update state when disconnected
  updateDisconnectedState() {
    stateManager.update({
      'connection.desktop': false,
      'connection.port': null
    });
  }

  // Schedule reconnection attempt using chrome.alarms (survives service worker termination)
  async scheduleReconnect() {
    const attempts = stateManager.get('connection.reconnectAttempts') || 0;
    stateManager.set('connection.reconnectAttempts', attempts + 1);

    // Set reconnecting state for badge
    stateManager.set('connection.reconnecting', true);

    if (attempts === 0) {
      // Immediate first retry (user decision from CONTEXT.md)
      console.log('[ConnectionService] Immediate reconnect attempt');
      this.attemptReconnect();
      return;
    }

    // Exponential backoff: 1s, 2s, 4s, 8s... up to 30s max
    const delayMs = Math.min(30000, 1000 * Math.pow(2, attempts - 1));
    // Chrome alarms minimum is 30 seconds in production, but we use calculated delay for logging
    // The alarm will fire after delayInMinutes (minimum 0.5 = 30 seconds in production)
    const delayMinutes = Math.max(0.5, delayMs / 60000);

    console.log(`[ConnectionService] Scheduling reconnect in ${delayMs}ms (alarm: ${delayMinutes.toFixed(2)} min, attempt ${attempts + 1})`);
    await chrome.alarms.create('reconnect', { delayInMinutes: delayMinutes });
  }

  // Attempt reconnect - called by alarm listener in background.js
  async attemptReconnect() {
    console.log('[ConnectionService] Attempting reconnect from alarm...');
    const connected = await this.connect();
    if (!connected) {
      this.scheduleReconnect();
    } else {
      // Clear any pending reconnect alarm on success
      await chrome.alarms.clear('reconnect');
      stateManager.set('connection.reconnectAttempts', 0);
    }
  }

  // Send message to desktop app
  send(message) {
    if (this.websocket && this.websocket.readyState === WebSocket.OPEN) {
      console.log('[ConnectionService] Sending:', message.type);
      this.websocket.send(JSON.stringify(message));
      return true;
    }

    // Queue message for later delivery
    console.log('[ConnectionService] Disconnected - queueing:', message.type);
    messageQueueService.enqueue(message);
    return false;
  }

  /**
   * Send a message and wait for a specific response type.
   *
   * Registers a one-shot handler for `responseType`, sends `message`, and
   * resolves with the response data.  Rejects after `timeoutMs` or when the
   * WebSocket is not connected.
   *
   * @param {Object} message       - Message to send (must have a `type` field).
   * @param {string} responseType  - The `type` value expected in the reply.
   * @param {number} [timeoutMs=5000]
   * @returns {Promise<Object>}    - Resolves with the response payload.
   */
  sendAndWait(message, responseType, timeoutMs = 5000) {
    return new Promise((resolve, reject) => {
      if (!this.websocket || this.websocket.readyState !== WebSocket.OPEN) {
        reject(new Error('Not connected to desktop agent'));
        return;
      }

      let settled = false;
      let timer = null;

      const cleanup = this.onMessage(responseType, (data) => {
        if (settled) return;
        settled = true;
        clearTimeout(timer);
        cleanup(); // unregister the one-shot handler
        resolve(data);
      });

      timer = setTimeout(() => {
        if (settled) return;
        settled = true;
        cleanup();
        reject(new Error(`Timeout waiting for ${responseType}`));
      }, timeoutMs);

      // Send after registering the handler so we cannot miss a fast reply
      try {
        this.websocket.send(JSON.stringify(message));
        console.log('[ConnectionService] sendAndWait sending:', message.type);
      } catch (err) {
        settled = true;
        clearTimeout(timer);
        cleanup();
        reject(err);
      }
    });
  }

  // Start ping interval
  startPing() {
    this.stopPing();
    this.pingTimer = setInterval(() => {
      this.send({ type: MSG.WS_PING });
    }, this.config.pingInterval);
  }

  // Stop ping interval
  stopPing() {
    if (this.pingTimer) {
      clearInterval(this.pingTimer);
      this.pingTimer = null;
    }
  }

  // Start keepalive interval (keeps service worker alive while connected)
  startKeepalive() {
    this.stopKeepalive();
    // 20 seconds - within 30s service worker window (research recommendation)
    this.keepaliveTimer = setInterval(() => {
      if (this.websocket?.readyState === WebSocket.OPEN) {
        this.websocket.send(JSON.stringify({ type: 'keepalive' }));
        console.log('[ConnectionService] Keepalive sent');
      } else {
        this.stopKeepalive();
      }
    }, this.config.keepaliveInterval);
  }

  // Stop keepalive interval
  stopKeepalive() {
    if (this.keepaliveTimer) {
      clearInterval(this.keepaliveTimer);
      this.keepaliveTimer = null;
    }
  }

  // Send keepalive via alarm backup (called from background.js alarm handler)
  sendKeepalive() {
    if (this.websocket && this.websocket.readyState === WebSocket.OPEN) {
      this.websocket.send(JSON.stringify({ type: 'keepalive' }));
      console.log('[ConnectionService] Keepalive sent (alarm backup)');
    }
  }

  // Start heartbeat interval (detects dead connections)
  startHeartbeat() {
    this.stopHeartbeat();
    this.missedHeartbeats = 0;

    this.heartbeatTimer = setInterval(() => {
      this.sendHeartbeat();
    }, this.config.heartbeatInterval);

    console.log('[ConnectionService] Heartbeat started (10s interval)');
  }

  // Stop heartbeat interval
  stopHeartbeat() {
    if (this.heartbeatTimer) {
      clearInterval(this.heartbeatTimer);
      this.heartbeatTimer = null;
    }
    this.missedHeartbeats = 0;
  }

  // Send heartbeat ping and track missed responses
  sendHeartbeat() {
    if (!this.websocket || this.websocket.readyState !== WebSocket.OPEN) {
      console.log('[ConnectionService] Heartbeat skipped - not connected');
      this.stopHeartbeat();
      return;
    }

    // Increment missed counter BEFORE sending
    this.missedHeartbeats++;
    console.log(`[ConnectionService] Heartbeat ping (missed: ${this.missedHeartbeats}/${this.config.maxMissedHeartbeats})`);

    if (this.missedHeartbeats >= this.config.maxMissedHeartbeats) {
      console.log('[ConnectionService] Connection dead - too many missed heartbeats');
      this.handleDeadConnection();
      return;
    }

    // Send heartbeat ping
    try {
      this.websocket.send(JSON.stringify({ type: 'heartbeat_ping' }));
    } catch (e) {
      console.error('[ConnectionService] Error sending heartbeat:', e);
      this.handleDeadConnection();
    }
  }

  // Handle dead connection (detected via missed heartbeats)
  handleDeadConnection() {
    console.log('[ConnectionService] Forcing disconnect due to dead connection');
    this.stopHeartbeat();
    this.stopKeepalive();
    this.stopPing();

    // Force close the socket
    if (this.websocket) {
      try {
        this.websocket.close();
      } catch (e) {
        // Ignore close errors
      }
      this.websocket = null;
    }

    this.updateDisconnectedState();
    this.scheduleReconnect();
  }

  // Called when heartbeat pong is received
  receivedHeartbeatPong() {
    console.log('[ConnectionService] Heartbeat pong received - connection alive');
    this.missedHeartbeats = 0;
  }

  // Flush queued messages after reconnection
  async flushQueue() {
    if (!messageQueueService.hasMessages) {
      return;
    }

    const messages = messageQueueService.flush();
    console.log(`[ConnectionService] Flushing ${messages.length} queued messages`);

    for (const message of messages) {
      if (this.websocket?.readyState === WebSocket.OPEN) {
        console.log('[ConnectionService] Sending queued:', message.type);
        this.websocket.send(JSON.stringify(message));
        // Small delay to avoid flooding
        await new Promise(resolve => setTimeout(resolve, 50));
      } else {
        // Connection lost during flush, re-queue remaining
        console.log('[ConnectionService] Connection lost during flush, re-queueing');
        messageQueueService.enqueue(message);
        break;
      }
    }
  }

  // Re-send stored email to desktop agent after connect/reconnect
  async sendStoredEmail() {
    try {
      const data = await chrome.storage.local.get(['userEmail']);
      if (data.userEmail) {
        this.send({
          type: 'user_auth',
          email: data.userEmail
        });
        console.log('[ConnectionService] Sent stored email on reconnect:', data.userEmail);
      }
    } catch (e) {
      console.error('[ConnectionService] Error sending stored email:', e);
    }
  }

  // Force reconnect
  async reconnect() {
    console.log('[ConnectionService] Force reconnect requested');

    // Close existing connection
    if (this.websocket) {
      this.websocket.close();
    }

    // Reset attempts
    stateManager.set('connection.reconnectAttempts', 0);

    // Connect
    return await this.connect();
  }

  // Check if connected
  isConnected() {
    return this.websocket && this.websocket.readyState === WebSocket.OPEN;
  }

  // Port storage
  async getSavedPort() {
    return new Promise((resolve) => {
      chrome.storage.local.get(['connectedPort'], (result) => {
        resolve(result.connectedPort || null);
      });
    });
  }

  async savePort(port) {
    return new Promise((resolve) => {
      chrome.storage.local.set({ connectedPort: port }, resolve);
    });
  }

  // Disconnect (clears all timers and alarms)
  async disconnect() {
    await chrome.alarms.clear('reconnect');
    await chrome.alarms.clear('keepalive');
    this.stopPing();
    this.stopKeepalive();
    this.stopHeartbeat();
    messageQueueService.clear(); // Clear queue on intentional disconnect
    if (this.websocket) {
      this.websocket.close();
      this.websocket = null;
    }
    this.updateDisconnectedState();
  }
}

// Singleton instance
export const connectionService = new ConnectionService();
export default connectionService;

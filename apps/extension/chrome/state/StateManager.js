// ============================================
// AntiScam Extension - State Manager
// Centralized state management with reactive updates
// ============================================

class StateManager {
  constructor() {
    this.state = {
      connection: {
        desktop: false,
        port: null,
        reconnectAttempts: 0
      },
      scan: {
        currentUrl: null,
        score: null,
        riskType: [],
        loading: false,
        error: null
      },
      user: {
        loggedIn: false,
        email: null,
        name: null,
        picture: null
      },
      cache: {
        size: 0,
        lastCleared: null
      },
      config: {
        version: '0.1.0'
      }
    };

    this.listeners = new Map();
    this.initialized = false;
  }

  // Initialize state from storage
  async init() {
    if (this.initialized) return;

    try {
      const stored = await chrome.storage.local.get(['appState']);
      if (stored.appState) {
        this.state = this.mergeDeep(this.state, stored.appState);
      }
      this.initialized = true;
      console.log('[StateManager] Initialized with state:', this.state);
    } catch (e) {
      console.error('[StateManager] Init error:', e);
    }
  }

  // Get current state or a path within it
  get(path = null) {
    if (!path) return { ...this.state };

    const parts = path.split('.');
    let value = this.state;

    for (const part of parts) {
      if (value === undefined || value === null) return undefined;
      value = value[part];
    }

    return value;
  }

  // Set state at a specific path
  set(path, value) {
    const parts = path.split('.');
    const lastKey = parts.pop();
    let target = this.state;

    for (const part of parts) {
      if (!(part in target)) {
        target[part] = {};
      }
      target = target[part];
    }

    const oldValue = target[lastKey];
    target[lastKey] = value;

    // Notify listeners
    this.notifyListeners(path, value, oldValue);

    // Persist to storage (debounced)
    this.schedulePersist();

    return value;
  }

  // Update multiple values at once
  update(updates) {
    for (const [path, value] of Object.entries(updates)) {
      this.set(path, value);
    }
  }

  // Subscribe to state changes
  subscribe(path, callback) {
    if (!this.listeners.has(path)) {
      this.listeners.set(path, new Set());
    }
    this.listeners.get(path).add(callback);

    // Return unsubscribe function
    return () => {
      this.listeners.get(path)?.delete(callback);
    };
  }

  // Subscribe to any state change
  subscribeAll(callback) {
    return this.subscribe('*', callback);
  }

  // Notify listeners of state change
  notifyListeners(path, newValue, oldValue) {
    // Notify specific path listeners
    if (this.listeners.has(path)) {
      this.listeners.get(path).forEach(cb => {
        try {
          cb(newValue, oldValue, path);
        } catch (e) {
          console.error('[StateManager] Listener error:', e);
        }
      });
    }

    // Notify parent path listeners
    const parts = path.split('.');
    while (parts.length > 1) {
      parts.pop();
      const parentPath = parts.join('.');
      if (this.listeners.has(parentPath)) {
        this.listeners.get(parentPath).forEach(cb => {
          try {
            cb(this.get(parentPath), null, path);
          } catch (e) {
            console.error('[StateManager] Listener error:', e);
          }
        });
      }
    }

    // Notify global listeners
    if (this.listeners.has('*')) {
      this.listeners.get('*').forEach(cb => {
        try {
          cb(this.state, path);
        } catch (e) {
          console.error('[StateManager] Listener error:', e);
        }
      });
    }
  }

  // Persist state to storage (debounced)
  persistTimeout = null;
  schedulePersist() {
    if (this.persistTimeout) {
      clearTimeout(this.persistTimeout);
    }
    this.persistTimeout = setTimeout(() => {
      this.persist();
    }, 100);
  }

  async persist() {
    try {
      await chrome.storage.local.set({ appState: this.state });
    } catch (e) {
      console.error('[StateManager] Persist error:', e);
    }
  }

  // Deep merge helper
  mergeDeep(target, source) {
    const result = { ...target };

    for (const key of Object.keys(source)) {
      if (source[key] instanceof Object && key in target && target[key] instanceof Object) {
        result[key] = this.mergeDeep(target[key], source[key]);
      } else {
        result[key] = source[key];
      }
    }

    return result;
  }

  // Reset state to defaults
  reset() {
    this.state = {
      connection: { desktop: false, port: null, reconnectAttempts: 0 },
      scan: { currentUrl: null, score: null, riskType: [], loading: false, error: null },
      user: { loggedIn: false, email: null, name: null, picture: null },
      cache: { size: 0, lastCleared: null },
      config: { version: '0.1.0' }
    };
    this.persist();
    this.notifyListeners('*', this.state, null);
  }
}

// Singleton instance
export const stateManager = new StateManager();
export default stateManager;

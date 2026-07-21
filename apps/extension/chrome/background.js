// ============================================
// AntiScam Extension - Background Service Worker
// Refactored with modular architecture
// ============================================

import { stateManager } from './state/StateManager.js';
import { messageBus, MSG } from './messaging/index.js';
import { REMOTE_TOOL_NAMES, REMOTE_TOOL } from './messaging/MessageTypes.js';
import { connectionService } from './services/ConnectionService.js';
import { cacheService } from './services/CacheService.js';
import { scanService } from './services/ScanService.js';
import { protectionService } from './services/ProtectionService.js';
import { iconService } from './services/IconService.js';
import { authService } from './services/AuthService.js';
import { messageQueueService } from './services/MessageQueueService.js';

// ============================================
// Connection Status for Badge
// ============================================

const ConnectionStatus = {
  CONNECTED: 'connected',
  DISCONNECTED: 'disconnected',
  RECONNECTING: 'reconnecting'
};

const BADGE_CONFIG = {
  [ConnectionStatus.CONNECTED]: { color: '#22C55E', text: '' },      // Green, no text
  [ConnectionStatus.DISCONNECTED]: { color: '#EF4444', text: '!' },  // Red, exclamation
  [ConnectionStatus.RECONNECTING]: { color: '#F59E0B', text: '' }    // Yellow/amber
};

// ============================================
// Loading State Helpers
// ============================================

function startLoadingState() {
  iconService.startLoadingAnimation();
  // Clear old score and set scanning flag for popup
  chrome.storage.local.set({
    currentPageScanning: true,
    currentPageScore: null,  // Clear old score immediately
    currentPageRiskType: [],
    currentPageAction: 0
  });
}

function stopLoadingState() {
  iconService.stopLoadingAnimation();
  chrome.storage.local.set({ currentPageScanning: false });
}

async function updateConnectionBadge(status) {
  const config = BADGE_CONFIG[status] || BADGE_CONFIG[ConnectionStatus.DISCONNECTED];
  await chrome.action.setBadgeBackgroundColor({ color: config.color });
  await chrome.action.setBadgeText({ text: config.text });
  console.log(`[Background] Badge updated: ${status}`);
}

// ============================================
// Alarm Listener (MUST be at top level for MV3)
// Registered synchronously when service worker starts
// ============================================

chrome.alarms.onAlarm.addListener((alarm) => {
  console.log('[Background] Alarm triggered:', alarm.name);
  switch (alarm.name) {
    case 'reconnect':
      connectionService.attemptReconnect();
      break;
    case 'keepalive':
      connectionService.sendKeepalive();
      break;
    case 'heartbeat':
      connectionService.sendHeartbeat();
      break;
  }
});

// ============================================
// Configuration
// ============================================

const manifest = chrome.runtime.getManifest();
const CONFIG = {
  VERSION: manifest.version
};

// ============================================
// WebSocket Message Handlers
// ============================================

function setupWebSocketHandlers() {
  // Handle pong - store email and device IP from agent
  connectionService.onMessage(MSG.WS_PONG, async (data) => {
    console.log('[Background] Connection verified with desktop app');
    if (data && data.email) {
      await chrome.storage.local.set({ userEmail: data.email });
      console.log('[Background] Email received from agent:', data.email);
    }
    if (data && data.ipAddress) {
      connectionService.setDeviceIpAddress(data.ipAddress);
      console.log('[Background] Device IP received from agent:', data.ipAddress);
    }
  });

  // Handle URL result
  connectionService.onMessage(MSG.WS_URL_RESULT, (data) => {
    handleUrlResult(data);
  });

  // Handle errors
  connectionService.onMessage(MSG.WS_ERROR, (data) => {
    console.error('[Background] Error from desktop:', data.message);
  });

  // Handle notifications
  connectionService.onMessage(MSG.WS_NOTIFICATION, (data) => {
    handleNotification(data);
  });

  // Handle browser tabs request from desktop agent
  connectionService.onMessage(MSG.WS_GET_BROWSER_TABS, async (data) => {
    console.log('[Background] Browser tabs requested by desktop agent');
    try {
      const tabs = await chrome.tabs.query({});
      const visibleTabs = tabs.filter(
        tab => tab.url && !tab.url.startsWith('chrome://') && !tab.url.startsWith('chrome-extension://')
      );

      // Per-tab logged-in detection (cookies + DOM scan), in parallel.
      // Each detector may return null for "unknown".
      const tabData = await Promise.all(
        visibleTabs.map(async (tab) => {
          const [cookieResult, domResult] = await Promise.all([
            checkLoggedInByCookies(tab.url),
            checkLoggedInByDom(tab.id),
          ]);

          const { loggedIn, confidence, signals } = combineLoggedInSignals(
            cookieResult,
            domResult
          );

          return {
            title:               tab.title     || '',
            url:                 tab.url       || '',
            isActive:            tab.active    || false,
            userAgent:           navigator.userAgent,
            timestamp:           tab.lastAccessed
                                   ? new Date(tab.lastAccessed).toISOString()
                                   : new Date().toISOString(),
            loggedIn:            loggedIn,            // true | false | null
            loggedInConfidence:  confidence,          // 'high' | 'medium' | 'low' | null
            loggedInSignals:     signals,             // ['cookie', 'dom'] subset
          };
        })
      );

      connectionService.send({
        type:      MSG.WS_BROWSER_TABS_RESPONSE,
        requestId: data.requestId,
        tabs:      tabData
      });
      console.log(`[Background] Sent ${tabData.length} tabs to desktop agent`);
    } catch (err) {
      console.error('[Background] Error querying browser tabs:', err);
      connectionService.send({
        type:      MSG.WS_BROWSER_TABS_RESPONSE,
        requestId: data.requestId,
        tabs:      []
      });
    }
  });

  // ─── ImmediateDanger lifecycle ────────────────────────────────────────
  // While `immediateDangerMode` is true, every tab close generates a
  // TabClosedAlert sent to the agent (the agent forwards it to the backend
  // for re-evaluation of the danger condition).
  connectionService.onMessage(MSG.WS_IMMEDIATE_DANGER_STARTED, () => {
    immediateDangerMode = true;
    console.log('[Background] ImmediateDanger mode ON');
  });
  connectionService.onMessage(MSG.WS_IMMEDIATE_DANGER_ENDED, () => {
    immediateDangerMode = false;
    console.log('[Background] ImmediateDanger mode OFF');
  });

  // ─── IsDeviceRemoteControlled gate ──────────────────────────────────
  // Pushed by the agent each time its RA flag toggles. While true, every
  // URL change to a sensitive site (or login transition) emits
  // WS_TAB_CHANGED_ALERT; sensitive tab closes emit WS_TAB_CLOSED_ALERT.
  connectionService.onMessage(MSG.WS_SET_REMOTE_CONTROLLED, async (data) => {
    const next = !!(data && data.isDeviceRemoteControlled);
    if (next === isDeviceRemoteControlled) return;
    isDeviceRemoteControlled = next;
    console.log('[Background] IsDeviceRemoteControlled =', next);

    // On transition to remote-controlled: scan all already-open tabs and
    // report any sensitive ones immediately. Without this, a bank tab that
    // was open BEFORE AnyDesk connected is never reported to the backend,
    // so ImmediateDanger is never raised.
    if (next) {
      try {
        const allTabs = await chrome.tabs.query({});
        for (const tab of allTabs) {
          if (!tab.url || !/^https?:/i.test(tab.url)) continue;
          const snap = await _refreshTabControl(tab.id, tab.url);
          // Report every http/https tab — backend filters by SensitiveSites table.
          // Skipping based on local sensitiveDomainCache would miss banks not yet
          // seen this session.
          if (snap.isSensitiveWebsite !== false) {
            _sendTabChangedAlert(tab.id, snap);
          }
        }
      } catch (e) {
        console.warn('[Background] Existing-tab scan on remote-controlled transition failed:', e);
      }
    }
  });

  // Handle remote access alert from desktop
  connectionService.onMessage(MSG.REMOTE_ACCESS_ALERT, async (data) => {
    console.log('[Background] Remote access alert received:', data);

    // Only show warning for incoming connections (dangerous)
    if (data.direction !== 'incoming') {
      console.log('[Background] Outgoing connection - no warning needed');
      return;
    }

    // Store active warning state for multi-tab coordination
    stateManager.update({
      'warning.active': true,
      'warning.toolId': data.toolId || data.remote_app,
      'warning.toolName': REMOTE_TOOL_NAMES[data.toolId] || REMOTE_TOOL_NAMES[REMOTE_TOOL.UNKNOWN],
      'warning.direction': data.direction,
      'warning.remoteCountry': data.remote_country || ''
    });

    // Send warning to all tabs
    const tabs = await chrome.tabs.query({});

    for (const tab of tabs) {
      // Skip non-injectable URLs
      if (!tab.url ||
          tab.url.startsWith('chrome://') ||
          tab.url.startsWith('chrome-extension://') ||
          tab.url.startsWith('edge://') ||
          tab.url.startsWith('about:')) {
        continue;
      }

      try {
        await chrome.tabs.sendMessage(tab.id, {
          type: MSG.REMOTE_ACCESS_WARNING_SHOW,
          toolId: data.toolId || data.remote_app,
          toolName: REMOTE_TOOL_NAMES[data.toolId] || REMOTE_TOOL_NAMES[REMOTE_TOOL.UNKNOWN],
          direction: data.direction,
          remoteCountry: data.remote_country
        });
      } catch (e) {
        // Content script not loaded in this tab - expected for some tabs
        console.debug('[Background] Could not send to tab:', tab.id, e.message);
      }
    }
  });

  // Handle session end from desktop
  connectionService.onMessage(MSG.REMOTE_ACCESS_SESSION_END, async (data) => {
    console.log('[Background] Remote access session ended:', data);

    // Clear warning state
    stateManager.update({ 'warning.active': false });

    // Dismiss warning on all tabs
    const tabs = await chrome.tabs.query({});
    for (const tab of tabs) {
      try {
        await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
      } catch (e) {
        // Ignore - tab may not have content script
      }
    }
  });

  // Handle app closed from desktop
  connectionService.onMessage(MSG.REMOTE_ACCESS_APP_CLOSED, async (data) => {
    console.log('[Background] Remote access app closed:', data);

    // Clear warning state
    stateManager.update({ 'warning.active': false });

    // Dismiss warning on all tabs
    const tabs = await chrome.tabs.query({});
    for (const tab of tabs) {
      try {
        await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
      } catch (e) {
        // Ignore - tab may not have content script
      }
    }
  });

  // Handle SetTrackedDomains from backend
  connectionService.onMessage('tracked_domains:set', async (data) => {
    console.log('[Background] Tracked domains updated:', data);

    // Store tracked domains
    if (data.domains && Array.isArray(data.domains)) {
      await chrome.storage.local.set({
        trackedDomains: data.domains,
        alwaysSendFormSubmits: data.alwaysSendFormSubmits || false
      });

      console.log(`[Background] Stored ${data.domains.length} tracked domains`);

      // Rebuild the in-memory trackedDomains Map so navigation-based
      // TrackUrlAlert (getTrackingInfo → Surf/Click) sees centrally-managed
      // domains too. Previously this Map was ONLY populated by the
      // protective-action path (enableUrlTracking/setTrackMode), so
      // backend-pushed SetTrackedDomains never triggered navigation
      // tracking — only content.js form-submit tracking.
      //
      // This list is authoritative: clear then refill. Each entry is keyed
      // by ROOT domain (getTrackingInfo looks up by root). Centrally-managed
      // domains have no per-entry duration, so use a long TTL that self-heals
      // if backend stops syncing; every push refreshes it.
      const CENTRAL_TRACK_TTL_MS = 24 * 60 * 60 * 1000; // 24h
      trackedDomains.clear();
      for (const item of data.domains) {
        const rawDomain = item.Domain || item.domain;
        if (!rawDomain) continue;
        let rootDomain;
        try {
          rootDomain = getRootDomain(
            rawDomain.replace(/^https?:\/\//, '').split('/')[0]
          );
        } catch (_) {
          rootDomain = String(rawDomain).toLowerCase();
        }
        trackedDomains.set(rootDomain, {
          expiresAt: Date.now() + CENTRAL_TRACK_TTL_MS,
          trackMode: typeof item.TrackMode === 'number'
            ? item.TrackMode
            : (item.trackMode ?? TrackMode.Surf),
          scamInProgressKey: item.ScamInProgressKey || item.scamInProgressKey || '',
        });
      }
      console.log(`[Background] In-memory trackedDomains Map rebuilt: ${trackedDomains.size} root domain(s)`);

      // Notify all content scripts to reload tracked domains
      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        if (tab.url && tab.url.startsWith('http')) {
          try {
            await chrome.tabs.sendMessage(tab.id, {
              type: 'tracked_domains:updated',
              domains: data.domains,
              alwaysSendFormSubmits: data.alwaysSendFormSubmits || false
            });
          } catch (e) {
            // Content script not loaded - ignore
          }
        }
      }
    }
  });
}

function handleUrlResult(data) {
  console.log('[Background] URL result received:', { url: data.url, score: data.score, action: data.protectiveAction, analyzing: data.analyzing });

  // Cache `isSensitiveWebsite` per root domain — used by the TabChangedAlert
  // pipeline to decide whether navigation events for a domain matter.
  // We only OVERWRITE on definitive true/false; null/undefined leaves cache.
  if (data && data.url && typeof data.isSensitiveWebsite === 'boolean') {
    const root = _safeRootDomain(data.url);
    if (root) {
      sensitiveDomainCache.set(root, data.isSensitiveWebsite);
      // Back-fill any open tab on the same root so a still-open bank tab
      // becomes "known sensitive" once the analysis result lands.
      for (const [tid, ctrl] of tabControls) {
        if (ctrl.root === root) {
          ctrl.isSensitiveWebsite = data.isSensitiveWebsite;
        }
      }
    }
  }

  // If server is still analyzing, keep loading state and wait for final result
  if (data.analyzing === true) {
    console.log('[Background] Analysis in progress, waiting for final result...');
    return; // Don't stop loading, don't update UI - wait for real result
  }

  // Stop loading animation when we have a final result
  stopLoadingState();

  // Check for EnableUrlTracking and SetTrackMode in protective actions
  if (data.protectiveActions && Array.isArray(data.protectiveActions)) {
    for (const action of data.protectiveActions) {
      if (action.message) {
        // EnableUrlTracking|domain|durationMinutes
        if (action.message.startsWith('EnableUrlTracking|')) {
          const parts = action.message.split('|');
          if (parts.length >= 3) {
            const domain = parts[1];
            const durationMinutes = parseInt(parts[2], 10) || 30;
            enableUrlTracking(domain, durationMinutes, TrackMode.Surf);
          }
        }
        // SetTrackMode|domain|trackMode|scamInProgressKey|durationMinutes
        else if (action.message.startsWith('SetTrackMode|')) {
          const parts = action.message.split('|');
          if (parts.length >= 3) {
            const domain = parts[1];
            const mode = parseInt(parts[2], 10) || TrackMode.Surf;
            const scamKey = parts[3] || '';
            const durationMinutes = parseInt(parts[4], 10) || 30;
            setTrackMode(domain, mode, scamKey, durationMinutes);
          }
        }
      }
    }
  }

  const result = scanService.handleResult(data);

  if (result) {
    // Update icon based on protective action from server (with score fallback)
    iconService.setColorByAction(result.protectiveAction, result.score);

    // Execute protective action from server
    protectionService.executeAction(
      result.protectiveAction,
      result.riskType,
      result.score
    );
  }
}

function handleNotification(data) {
  console.log('[Background] Notification received:', data);

  chrome.notifications.create({
    type: 'basic',
    iconUrl: 'icons/icon128.png',
    title: data.title || 'AntiScam Alert',
    message: data.message || 'New notification from server'
  });
}

// ============================================
// Message Bus Handlers
// ============================================

function setupMessageHandlers() {
  messageBus.init();

  // Get status - includes all server values
  messageBus.on(MSG.STATUS_GET, () => {
    return {
      isConnectedToDesktop: connectionService.isConnected(),
      isConnectedToBackend: false,
      connectedPort: stateManager.get('connection.port'),
      cacheSize: cacheService.size(),
      version: CONFIG.VERSION,
      currentPageScore: stateManager.get('scan.score'),
      currentPageRiskType: stateManager.get('scan.riskType'),
      currentPageAction: stateManager.get('scan.protectiveAction'),
      userEmail: stateManager.get('user.email'),
      warningActive: stateManager.get('warning.active') || false,
      warningToolName: stateManager.get('warning.toolName') || null,
      warningDirection: stateManager.get('warning.direction') || null,
      warningRemoteCountry: stateManager.get('warning.remoteCountry') || null
    };
  });

  // Legacy: getStatus
  messageBus.on('getStatus', () => {
    return {
      isConnectedToDesktop: connectionService.isConnected(),
      isConnectedToBackend: false,
      connectedPort: stateManager.get('connection.port'),
      cacheSize: cacheService.size(),
      version: CONFIG.VERSION,
      currentPageScore: stateManager.get('scan.score'),
      currentPageRiskType: stateManager.get('scan.riskType'),
      currentPageAction: stateManager.get('scan.protectiveAction'),
      userEmail: stateManager.get('user.email'),
      warningActive: stateManager.get('warning.active') || false,
      warningToolName: stateManager.get('warning.toolName') || null,
      warningDirection: stateManager.get('warning.direction') || null,
      warningRemoteCountry: stateManager.get('warning.remoteCountry') || null
    };
  });

  // Reconnect
  messageBus.on(MSG.CONNECTION_RECONNECT, async () => {
    const connected = await connectionService.reconnect();
    return { success: connected };
  });

  // Legacy: reconnect
  messageBus.on('reconnect', async () => {
    const connected = await connectionService.reconnect();
    return { success: connected };
  });

  // Clear cache
  messageBus.on(MSG.CACHE_CLEAR, () => {
    cacheService.clear();
    return { success: true };
  });

  // Legacy: clearCache
  messageBus.on('clearCache', () => {
    cacheService.clear();
    return { success: true };
  });

  // Scan current page
  messageBus.on(MSG.SCAN_CURRENT, async () => {
    startLoadingState();
    const result = await scanService.scanCurrentTab();
    // If cache hit, update icon immediately
    if (result) {
      stopLoadingState();
      iconService.setColorByAction(result.protectiveAction, result.score);
    }
    return { success: true };
  });

  // Legacy: scanCurrentPage
  messageBus.on('scanCurrentPage', async () => {
    startLoadingState();
    const result = await scanService.scanCurrentTab();
    // If cache hit, update icon immediately
    if (result) {
      stopLoadingState();
      iconService.setColorByAction(result.protectiveAction, result.score);
    }
    return { success: true };
  });

  // User signed in
  messageBus.on(MSG.AUTH_SIGN_IN, async (payload) => {
    console.log('[Background] User signed in:', payload.email);

    stateManager.update({
      'user.loggedIn': true,
      'user.email': payload.email
    });

    connectionService.send({
      type: MSG.WS_USER_AUTH,
      email: payload.email
    });

    return { success: true };
  });

  // Legacy: userSignedIn
  messageBus.on('userSignedIn', async (payload) => {
    console.log('[Background] User signed in:', payload.email);

    stateManager.update({
      'user.loggedIn': true,
      'user.email': payload.email
    });

    connectionService.send({
      type: MSG.WS_USER_AUTH,
      email: payload.email
    });

    return { success: true };
  });

  // User signed out
  messageBus.on(MSG.AUTH_SIGN_OUT, async () => {
    console.log('[Background] User signed out');

    stateManager.update({
      'user.loggedIn': false,
      'user.email': null
    });

    connectionService.send({ type: MSG.WS_USER_SIGNOUT });

    return { success: true };
  });

  // Legacy: userSignedOut
  messageBus.on('userSignedOut', async () => {
    console.log('[Background] User signed out');

    stateManager.update({
      'user.loggedIn': false,
      'user.email': null
    });

    connectionService.send({ type: MSG.WS_USER_SIGNOUT });

    return { success: true };
  });

  // Handle warning dismiss request from content script
  messageBus.on(MSG.REMOTE_ACCESS_WARNING_DISMISS, async () => {
    console.log('[Background] Warning dismiss requested');
    stateManager.update({ 'warning.active': false });

    // Notify desktop that warning was dismissed
    connectionService.send({ type: MSG.REMOTE_ACCESS_WARNING_DISMISS });

    // Dismiss on all other tabs
    const tabs = await chrome.tabs.query({});
    for (const tab of tabs) {
      try {
        await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
      } catch (e) {
        // Ignore
      }
    }

    return { success: true };
  });

  // Handle close session request from content script
  messageBus.on(MSG.REMOTE_ACCESS_CLOSE_SESSION, async () => {
    console.log('[Background] Close session requested');

    // Send to desktop app
    connectionService.send({ type: MSG.REMOTE_ACCESS_CLOSE_SESSION });

    return { success: true };
  });

  // Handle user continued anyway (acknowledged risk)
  messageBus.on(MSG.REMOTE_ACCESS_CONTINUED, async () => {
    console.log('[Background] User continued anyway');
    stateManager.update({ 'warning.active': false });

    // Notify desktop
    connectionService.send({ type: MSG.REMOTE_ACCESS_CONTINUED });

    // Dismiss on all tabs
    const tabs = await chrome.tabs.query({});
    for (const tab of tabs) {
      try {
        await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
      } catch (e) {
        // Ignore
      }
    }

    return { success: true };
  });

  // Handle form submission from content script
  messageBus.on('form:submitted', async (payload) => {
    console.log('[Background] Form submitted:', payload.data);

    // Get IP address and timezone
    const ipAddress = connectionService.getDeviceIpAddress();
    const timezoneOffset = new Date().getTimezoneOffset() / -60;
    const timezone = `UTC${timezoneOffset >= 0 ? '+' : ''}${timezoneOffset}`;

    // Get current tab ID
    let tabId = '';
    try {
      const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
      if (tabs[0]) {
        tabId = tabs[0].id.toString();
      }
    } catch (e) {
      console.error('[Background] Error getting tab ID:', e);
    }

    // Create TrackUrlAlert with form data
    const alert = {
      type: MSG.WS_TRACK_URL_ALERT,
      Url: payload.data.url,
      FromUrl: payload.data.fromUrl,
      Duration: 0, // No duration for form submit
      ScamInProgressKey: payload.data.scamInProgressKey || '',
      IPAddress: ipAddress,
      UserAgent: navigator.userAgent,
      TabId: tabId,
      Timezone: timezone,
      // Form-specific fields
      FormAction: payload.data.formAction,
      Method: payload.data.method,
      HasPasswordField: payload.data.hasPasswordField,
      HasEmailField: payload.data.hasEmailField,
      HasCreditCard: payload.data.hasCreditCard,
      FieldCount: payload.data.fieldCount
    };

    console.log('[Background] Sending TrackUrlAlert with form data:', alert);
    connectionService.send(alert);

    return { success: true };
  });

}

// ============================================
// URL Tracking Helper
// ============================================

// Track URL navigation history per tab
const tabNavigationHistory = new Map();
const tabActivationTimes = new Map();

// ─── ImmediateDanger / RemoteControlled state ─────────────────────────
// `immediateDangerMode` is flipped by WS_IMMEDIATE_DANGER_STARTED/_ENDED.
// `isDeviceRemoteControlled` is flipped by WS_SET_REMOTE_CONTROLLED — set
// to true while the agent's last RemoteAccessAlert reports
// session=open + direction=incoming. While EITHER is true, tab close on a
// previously-sensitive tab fires WS_TAB_CLOSED_ALERT. While
// `isDeviceRemoteControlled` is true, every URL change to a sensitive site
// (or login transition on an already-sensitive tab) fires
// WS_TAB_CHANGED_ALERT — so the backend can re-evaluate ImmediateDanger
// without waiting for the next 10s RA poll.
let immediateDangerMode = false;
let isDeviceRemoteControlled = false;

// tabId → { url, root, isSensitiveWebsite, isLoggedIn }. The richer
// per-tab state needed to decide when a TabChangedAlert is meaningful.
const tabControls = new Map();

// rootDomain → bool. Cached from the backend's `isSensitiveWebsite` flag
// in WS_URL_RESULT. New tabs/onUpdated events look up sensitivity here
// without a round-trip.
const sensitiveDomainCache = new Map();

function _safeRootDomain(url) {
  try {
    const u = new URL(url);
    return getRootDomain(u.hostname);
  } catch (_) {
    return '';
  }
}

function _isSensitiveByCache(url) {
  const root = _safeRootDomain(url);
  if (!root) return undefined; // unknown
  return sensitiveDomainCache.get(root); // true / false / undefined
}

async function _detectLoggedInForTab(tab) {
  // Returns true | false | null. Re-uses the existing combineLoggedInSignals
  // pipeline (cookies + DOM scan).
  try {
    if (!tab || !tab.url || !/^https?:/i.test(tab.url)) return null;
    const [cookieResult, domResult] = await Promise.all([
      checkLoggedInByCookies(tab.url),
      checkLoggedInByDom(tab.id),
    ]);
    return combineLoggedInSignals(cookieResult, domResult).loggedIn;
  } catch (_) {
    return null;
  }
}

async function _refreshTabControl(tabId, url) {
  // Build/refresh per-tab cached state. Returns the snapshot.
  const root = _safeRootDomain(url);
  const isSensitive = sensitiveDomainCache.get(root);
  let isLoggedIn = null;
  if (isSensitive === true) {
    // Only pay the cookies+DOM cost when the tab is on a sensitive domain.
    try {
      const tab = await chrome.tabs.get(tabId);
      isLoggedIn = await _detectLoggedInForTab(tab);
    } catch (_) { /* tab may have closed */ }
  }
  const snap = { url, root, isSensitiveWebsite: isSensitive ?? null, isLoggedIn };
  tabControls.set(tabId, snap);
  return snap;
}

function _sendTabChangedAlert(tabId, snap) {
  if (typeof connectionService === 'undefined') return;
  if (!connectionService.isConnected || !connectionService.isConnected()) return;
  try {
    connectionService.send({
      type:               MSG.WS_TAB_CHANGED_ALERT,
      tabId:              String(tabId),
      url:                snap.url || '',
      isSensitiveWebsite: snap.isSensitiveWebsite,
      isLoggedIn:         snap.isLoggedIn,
      timestamp:          new Date().toISOString(),
    });
    console.log('[Background] TabChangedAlert sent:',
      tabId, snap.url, 'sensitive=', snap.isSensitiveWebsite, 'loggedIn=', snap.isLoggedIn);
  } catch (e) {
    console.warn('[Background] TabChangedAlert send failed:', e);
  }
}

function _sendTabClosedAlert(tabId, url) {
  if (typeof connectionService === 'undefined') return;
  if (!connectionService.isConnected || !connectionService.isConnected()) return;
  try {
    connectionService.send({
      type:      MSG.WS_TAB_CLOSED_ALERT,
      tabId:     String(tabId),
      url:       url || '',
      timestamp: new Date().toISOString(),
    });
    console.log('[Background] TabClosedAlert sent:', tabId, url);
  } catch (e) {
    console.warn('[Background] TabClosedAlert send failed:', e);
  }
}

chrome.tabs.onUpdated.addListener(async (tabId, changeInfo, tab) => {
  if (!tab || !tab.url) return;
  // We only care about URL changes for the Tab*Alert flow. status='complete'
  // also gives us a chance to re-check loggedIn after the page finished
  // loading (cookies/DOM are fully set by then).
  const urlChanged = !!changeInfo.url;
  const completed  = changeInfo.status === 'complete';
  if (!urlChanged && !completed) return;

  const prev = tabControls.get(tabId);
  const snap = await _refreshTabControl(tabId, tab.url);

  if (!isDeviceRemoteControlled) return;

  // Fire TabChangedAlert when:
  //  - The URL changed AND the new (or old) URL is sensitive, OR
  //  - The tab is on a sensitive site AND loggedIn just transitioned
  //    (covers SPA logins and post-load cookie-set logins).
  const wasSensitive = prev && prev.isSensitiveWebsite === true;
  const nowSensitive = snap.isSensitiveWebsite === true;
  const loggedInTransition = nowSensitive
    && prev
    && prev.isLoggedIn !== snap.isLoggedIn;

  if ((urlChanged && (nowSensitive || wasSensitive)) || loggedInTransition) {
    _sendTabChangedAlert(tabId, snap);
  }
});

chrome.tabs.onCreated.addListener(async (tab) => {
  if (!tab || tab.id == null || !tab.url) return;
  const snap = await _refreshTabControl(tab.id, tab.url);
  if (isDeviceRemoteControlled && snap.isSensitiveWebsite === true) {
    _sendTabChangedAlert(tab.id, snap);
  }
});

chrome.tabs.onRemoved.addListener((tabId /*, removeInfo */) => {
  // Capture state BEFORE we forget it.
  const prev = tabControls.get(tabId);
  tabControls.delete(tabId);

  if (!prev) return;

  // Two paths trigger TabClosedAlert:
  //  - immediateDangerMode (legacy): every close, regardless of sensitivity
  //    (backend filters; UDUserAnalyzer trims by url+tabId)
  //  - isDeviceRemoteControlled: only when the closed tab was sensitive
  //    (avoids noise during normal browsing under an active RA session)
  const shouldSend =
    (immediateDangerMode) ||
    (isDeviceRemoteControlled && prev.isSensitiveWebsite === true);
  if (!shouldSend) return;

  _sendTabClosedAlert(tabId, prev.url);
});

// ─── Cookie-driven loggedIn re-evaluation ─────────────────────────────
// `chrome.tabs.onUpdated` doesn't fire when a bank's logout is implemented
// purely client-side (XHR → cookie removal → DOM update, no navigation).
// Bridge that gap by listening to chrome.cookies.onChanged: whenever an
// auth-shaped cookie on a sensitive domain is added/removed, re-evaluate
// loggedIn for every open tab on that root domain and emit
// TabChangedAlert when the verdict actually changed.
//
// Coalescing: a single logout can fire many cookie events. We debounce per
// root domain so the heavy cookie+DOM scan runs at most once per ~400ms.
const _cookieCoalesceTimers = new Map(); // rootDomain -> timeoutId
const _COOKIE_DEBOUNCE_MS = 400;

function _cookieDomainToRoot(rawDomain) {
  if (!rawDomain) return '';
  // Leading dot is convention for cookies set on a parent domain
  let h = rawDomain.toLowerCase();
  if (h.startsWith('.')) h = h.slice(1);
  return getRootDomain(h);
}

async function _reevaluateLoggedInForRoot(root) {
  // Find every open tab on this root and re-run the loggedIn pipeline.
  // Fire TabChangedAlert only when the snapshot changed AND we're under
  // remote control (extension's gate is the same as for URL events).
  for (const [tabId, prev] of tabControls) {
    if (prev.root !== root) continue;
    let tab;
    try { tab = await chrome.tabs.get(tabId); } catch { continue; }
    if (!tab || !tab.url) continue;

    const snap = await _refreshTabControl(tabId, tab.url);
    const loggedInChanged = prev.isLoggedIn !== snap.isLoggedIn;
    if (!loggedInChanged) continue;
    if (!isDeviceRemoteControlled) continue;
    if (snap.isSensitiveWebsite !== true) continue;

    _sendTabChangedAlert(tabId, snap);
  }
}

chrome.cookies.onChanged.addListener((info) => {
  try {
    const cookie = info && info.cookie;
    if (!cookie) return;

    // Only care about auth-shaped cookies (same filter as the loggedIn detector).
    if (!AUTH_COOKIE_NAME_PATTERN.test(cookie.name)) return;

    const root = _cookieDomainToRoot(cookie.domain);
    if (!root) return;

    // Only care about domains we've classified as sensitive.
    if (sensitiveDomainCache.get(root) !== true) return;

    // Coalesce: many cookie events can fire in quick succession during
    // login/logout; we want one scan per burst.
    const existing = _cookieCoalesceTimers.get(root);
    if (existing) clearTimeout(existing);
    const tid = setTimeout(() => {
      _cookieCoalesceTimers.delete(root);
      _reevaluateLoggedInForRoot(root).catch(e =>
        console.warn('[Background] cookie-driven re-eval failed:', e));
    }, _COOKIE_DEBOUNCE_MS);
    _cookieCoalesceTimers.set(root, tid);
  } catch (e) {
    console.warn('[Background] cookies.onChanged handler error:', e);
  }
});

// TrackMode enum - matches backend Common.Enums.TrackMode
const TrackMode = {
  None: 0,    // No tracking
  Surf: 1,    // Default - send UrlAlert once per domain with silence interval
  Click: 2    // Send TrackUrlAlert on every click
};

// Domains that should be tracked (set by backend EnableUrlTracking)
// Map of domain -> { expiresAt: timestamp, trackMode: TrackMode, scamInProgressKey: string }
const trackedDomains = new Map();

/**
 * Check if a domain should be tracked
 * @param {string} url - URL to check
 * @returns {{shouldTrack: boolean, trackMode: number, scamInProgressKey: string}} - Tracking info
 */
/**
 * Extract root domain from hostname (e.g., www.news.example.com -> example.com)
 */
function getRootDomain(hostname) {
  let h = hostname.toLowerCase();
  
  // Remove www prefix
  if (h.startsWith('www.')) {
    h = h.substring(4);
  }
  
  const parts = h.split('.');
  if (parts.length <= 2) {
    return h;
  }
  
  // Check for known two-part TLDs
  const knownTwoPartTlds = ['co.uk', 'com.au', 'co.nz', 'co.jp', 'com.br', 'co.il', 'org.uk', 'net.au'];
  const lastTwo = `${parts[parts.length - 2]}.${parts[parts.length - 1]}`;
  
  if (knownTwoPartTlds.includes(lastTwo)) {
    // Return last 3 parts (e.g., example.co.uk)
    return parts.length >= 3 ? `${parts[parts.length - 3]}.${lastTwo}` : h;
  }
  
  // Return last 2 parts (e.g., example.com)
  return lastTwo;
}

function getTrackingInfo(url) {
  const defaultResult = { shouldTrack: false, trackMode: TrackMode.None, scamInProgressKey: '' };
  
  try {
    const urlObj = new URL(url);
    const rootDomain = getRootDomain(urlObj.hostname);
    
    // Check if root domain is tracked
    const trackInfo = trackedDomains.get(rootDomain);
    if (!trackInfo) {
      return defaultResult;
    }
    
    // Check if tracking has expired
    if (Date.now() > trackInfo.expiresAt) {
      trackedDomains.delete(domain);
      console.log(`[Background] URL tracking expired for domain: ${domain}`);
      return defaultResult;
    }
    
    return {
      shouldTrack: true,
      trackMode: trackInfo.trackMode || TrackMode.Surf,
      scamInProgressKey: trackInfo.scamInProgressKey || ''
    };
  } catch (e) {
    return defaultResult;
  }
}

/**
 * Legacy function for backwards compatibility
 * @param {string} url - URL to check
 * @returns {boolean} - True if domain is in tracked list and not expired
 */
function shouldTrackDomain(url) {
  return getTrackingInfo(url).shouldTrack;
}

/**
 * Enable URL tracking for a domain
 * @param {string} domain - Domain to track
 * @param {number} durationMinutes - How long to track (in minutes)
 * @param {number} trackMode - TrackMode (Surf=1, Click=2)
 * @param {string} scamInProgressKey - Optional ScamInProgress key
 */
function enableUrlTracking(domain, durationMinutes, trackMode = TrackMode.Surf, scamInProgressKey = '') {
  const expiresAt = Date.now() + (durationMinutes * 60 * 1000);
  trackedDomains.set(domain, {
    expiresAt,
    trackMode,
    scamInProgressKey
  });
  const modeName = trackMode === TrackMode.Click ? 'Click' : 'Surf';
  console.log(`[Background] URL tracking enabled for domain: ${domain} (${durationMinutes} min, mode: ${modeName})`);
}

/**
 * Set track mode for a domain (called by backend SetTrackMode command)
 * @param {string} domain - Domain to set mode for
 * @param {number} trackMode - TrackMode (Surf=1, Click=2)
 * @param {string} scamInProgressKey - ScamInProgress key
 * @param {number} durationMinutes - Duration in minutes (default 30)
 */
function setTrackMode(domain, trackMode, scamInProgressKey = '', durationMinutes = 30) {
  enableUrlTracking(domain, durationMinutes, trackMode, scamInProgressKey);
}

/**
 * Send TrackUrlAlert to desktop app via WebSocket
 * @param {number} tabId - Browser tab ID
 * @param {string} currentUrl - Current URL being visited
 * @param {string} fromUrl - Previous URL (referrer)
 * @param {number} duration - Time spent on previous page (seconds)
 * @param {string} scamInProgressKey - ScamInProgress key from tracking info
 */
async function sendTrackUrlAlert(tabId, currentUrl, fromUrl = '', duration = 0, scamInProgressKey = '') {
  // Only track http/https URLs
  if (!currentUrl || !currentUrl.startsWith('http')) {
    return;
  }

  // Get IP address from connection service (received from desktop app)
  const ipAddress = connectionService.getDeviceIpAddress();
  
  // Get timezone offset in hours
  const timezoneOffset = new Date().getTimezoneOffset() / -60;
  const timezone = `UTC${timezoneOffset >= 0 ? '+' : ''}${timezoneOffset}`;
  
  // Create TrackUrlAlert message
  const alert = {
    type: MSG.WS_TRACK_URL_ALERT,
    Url: currentUrl,
    FromUrl: fromUrl,
    Duration: duration,
    ScamInProgressKey: scamInProgressKey,
    IPAddress: ipAddress,
    UserAgent: navigator.userAgent,
    TabId: tabId.toString(),
    Timezone: timezone
  };

  console.log('[Background] Sending TrackUrlAlert:', { 
    url: currentUrl, 
    fromUrl, 
    duration,
    tabId,
    ipAddress,
    timezone 
  });

  // Send via WebSocket
  connectionService.send(alert);
}

/**
 * Track tab navigation and calculate duration
 * @param {number} tabId - Browser tab ID
 * @param {string} url - New URL
 */
function trackTabNavigation(tabId, url) {
  // Skip non-http URLs
  if (!url || !url.startsWith('http')) {
    return;
  }

  const now = Date.now();
  
  // Get tracking info for current URL
  const currentTrackInfo = getTrackingInfo(url);
  
  // Get previous navigation for this tab
  const previousNav = tabNavigationHistory.get(tabId);
  
  if (previousNav) {
    // Calculate duration on previous page (in seconds)
    const duration = Math.floor((now - previousNav.timestamp) / 1000);
    const prevTrackInfo = getTrackingInfo(previousNav.url);
    
    // Determine if we should send TrackUrlAlert based on TrackMode
    let shouldSendAlert = false;
    let scamKey = '';
    
    if (currentTrackInfo.shouldTrack) {
      // Current URL is tracked
      if (currentTrackInfo.trackMode === TrackMode.Click) {
        // Click mode: always send on every navigation
        shouldSendAlert = true;
        scamKey = currentTrackInfo.scamInProgressKey;
      } else {
        // Surf mode: send only if different from previous
        shouldSendAlert = previousNav.url !== url;
        scamKey = currentTrackInfo.scamInProgressKey;
      }
    } else if (prevTrackInfo.shouldTrack && prevTrackInfo.trackMode === TrackMode.Click) {
      // Previous URL was in Click mode - track navigation away
      shouldSendAlert = true;
      scamKey = prevTrackInfo.scamInProgressKey;
    }
    
    if (shouldSendAlert) {
      sendTrackUrlAlert(tabId, url, previousNav.url, duration, scamKey);
    }
  } else {
    // First navigation for this tab - only track if domain is in tracked list
    if (currentTrackInfo.shouldTrack) {
      sendTrackUrlAlert(tabId, url, '', 0, currentTrackInfo.scamInProgressKey);
    }
  }
  
  // Update history for this tab
  tabNavigationHistory.set(tabId, {
    url: url,
    timestamp: now
  });
}

// ============================================
// Tab Event Handlers
// ============================================

function setupTabListeners() {
  // Track last scanned URL per tab to avoid duplicate scans
  const lastScannedUrl = new Map();

  // Helper to trigger scan with loading state and timeout
  async function triggerScan(tabId, url) {
    console.log('[Background] triggerScan called:', { tabId, url });

    // Skip non-http URLs
    if (!url || !url.startsWith('http')) {
      console.log('[Background] Skipping non-http URL:', url);
      return;
    }

    // Check if tab still exists (avoid scanning when tab is closing)
    try {
      await chrome.tabs.get(tabId);
    } catch (e) {
      console.log('[Background] Tab no longer exists, skipping scan:', tabId);
      return;
    }

    // Skip if we just scanned this URL for this tab
    if (lastScannedUrl.get(tabId) === url) {
      console.log('[Background] Skipping duplicate scan for:', url);
      return;
    }

    lastScannedUrl.set(tabId, url);
    console.log('[Background] Auto-scan triggered for:', url);

    // Start loading animation and clear old scores for this tab
    startLoadingState();
    chrome.storage.local.set({
      [`tab_${tabId}_score`]: null,
      [`tab_${tabId}_riskType`]: [],
      [`tab_${tabId}_action`]: 0
    });

    // Scan and handle result
    const result = await scanService.scan(tabId, url);

    // If we got an immediate result (cache hit), stop loading and update icon
    if (result) {
      console.log('[Background] Immediate result (cache hit):', result);
      stopLoadingState();
      iconService.setColorByAction(result.protectiveAction, result.score);
      return;
    }

    // No immediate result - set up timeout for server response
    // 30-second timeout for scan (user decision: show neutral gray on timeout)
    setTimeout(() => {
      // Only trigger if still in loading state for this tab
      chrome.storage.local.get(['currentPageScanning'], (data) => {
        if (data.currentPageScanning) {
          stopLoadingState();
          iconService.setColor('gray');
          chrome.storage.local.set({
            currentPageScore: null,
            currentPageScanning: false
          });
          console.log('[Background] Scan timeout - showing neutral state');
        }
      });
    }, 30000);
  }

  // Tab updated - fires on status change AND URL change
  chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
    console.log('[Background] tabs.onUpdated:', { tabId, changeInfo: JSON.stringify(changeInfo), url: tab?.url });

    // Track URL navigation when URL changes
    if (changeInfo.url) {
      trackTabNavigation(tabId, changeInfo.url);
    }

    // Trigger scan when page finishes loading
    if (changeInfo.status === 'complete' && tab.url) {
      console.log('[Background] Triggering scan (status complete):', tab.url);
      triggerScan(tabId, tab.url);
    }
    // Also trigger scan when URL changes (catches SPA navigations)
    else if (changeInfo.url) {
      console.log('[Background] Triggering scan (URL change):', changeInfo.url);
      triggerScan(tabId, changeInfo.url);
    }
  });

  // Tab closed - cleanup per-tab storage
  chrome.tabs.onRemoved.addListener((tabId) => {
    lastScannedUrl.delete(tabId);
    
    // Clean up navigation tracking
    tabNavigationHistory.delete(tabId);
    tabActivationTimes.delete(tabId);
    
    chrome.storage.local.remove([
      `tab_${tabId}`,
      `tab_${tabId}_score`,
      `tab_${tabId}_riskType`,
      `tab_${tabId}_action`
    ]);
    console.log(`[Background] Cleaned up storage for closed tab ${tabId}`);
  });

  // Navigation completed - more reliable than tabs.onUpdated for some navigations
  chrome.webNavigation.onCompleted.addListener((details) => {
    console.log('[Background] webNavigation.onCompleted:', { tabId: details.tabId, frameId: details.frameId, url: details.url });
    // Only handle main frame navigations (not iframes)
    if (details.frameId === 0) {
      // Track URL navigation
      trackTabNavigation(details.tabId, details.url);
      
      console.log('[Background] Triggering scan (webNavigation):', details.url);
      triggerScan(details.tabId, details.url);
    }
  });

  // History state updated - catches SPA navigations via pushState/replaceState
  chrome.webNavigation.onHistoryStateUpdated.addListener((details) => {
    // Only handle main frame
    if (details.frameId === 0) {
      triggerScan(details.tabId, details.url);
    }
  });

  // Tab activated - only load cached score, never trigger new scan
  // Assumption: tab was already scanned when it was first opened (onUpdated/onCompleted)
  chrome.tabs.onActivated.addListener(async (activeInfo) => {
    try {
      const tabId = activeInfo.tabId;
      const tab = await chrome.tabs.get(tabId);

      // Load per-tab score into global state (so popup shows correct score)
      const data = await chrome.storage.local.get([
        `tab_${tabId}_score`,
        `tab_${tabId}_riskType`,
        `tab_${tabId}_action`
      ]);

      if (data[`tab_${tabId}_score`] !== undefined && data[`tab_${tabId}_score`] !== null) {
        // We have cached score for this tab - clear any stale loading state and update global state
        stopLoadingState();
        chrome.storage.local.set({
          currentPageScore: data[`tab_${tabId}_score`],
          currentPageRiskType: data[`tab_${tabId}_riskType`] || [],
          currentPageAction: data[`tab_${tabId}_action`] || 0,
          currentPageScanning: false
        });
        console.log(`[Background] Loaded cached score for tab ${tabId}: ${data[`tab_${tabId}_score`]}`);
      } else if (tab.url && tab.url.startsWith('http')) {
        // No per-tab score - check URL cache
        const cached = cacheService.get(tab.url);
        if (cached && cached.score !== undefined) {
          // Found in URL cache - clear any stale loading state, save to per-tab and global
          stopLoadingState();
          chrome.storage.local.set({
            [`tab_${tabId}_score`]: cached.score,
            [`tab_${tabId}_riskType`]: cached.riskType || [],
            [`tab_${tabId}_action`]: cached.protectiveAction || 0,
            currentPageScore: cached.score,
            currentPageRiskType: cached.riskType || [],
            currentPageAction: cached.protectiveAction || 0,
            currentPageScanning: false
          });
          console.log(`[Background] Loaded URL cache for tab ${tabId}: ${cached.score}`);
        } else {
          // No cache - tab should have been scanned when opened, don't scan on focus change
          console.log(`[Background] No cache for tab ${tabId}, not scanning on focus change`);
          chrome.storage.local.set({
            currentPageScore: null,
            currentPageRiskType: [],
            currentPageAction: 0,
            currentPageScanning: false
          });
        }
      }
    } catch (e) {
      console.error('[Background] Tab activation error:', e);
    }
  });
}

// ============================================
// State Change Handlers
// ============================================

function setupStateListeners() {
  // Connection status changes
  stateManager.subscribe('connection.desktop', (connected) => {
    if (connected) {
      console.log('[Background] Connected to desktop app');
      chrome.storage.local.set({ notFullyProtected: false });
      updateConnectionBadge(ConnectionStatus.CONNECTED);
    } else {
      console.log('[Background] Disconnected from desktop app');
      chrome.storage.local.set({
        notFullyProtected: true,
        warningMessage: 'Desktop app not running. Please start AntiScam Desktop for protection.'
      });
      updateConnectionBadge(ConnectionStatus.DISCONNECTED);
    }
  });

  // Reconnecting status (set from ConnectionService)
  stateManager.subscribe('connection.reconnecting', (reconnecting) => {
    if (reconnecting) {
      updateConnectionBadge(ConnectionStatus.RECONNECTING);
    }
    // When reconnecting becomes false, the connection.desktop handler will set the appropriate color
  });
}

// ============================================
// Initialization
// ============================================

async function init() {
  console.log(`[Background] AntiScam Extension v${CONFIG.VERSION} starting...`);

  // Initialize state manager
  await stateManager.init();

  // Initialize cache
  await cacheService.init();

  // Initialize auth
  await authService.init();

  // Setup handlers
  setupWebSocketHandlers();
  setupMessageHandlers();
  setupTabListeners();
  setupStateListeners();

  // Restore queued messages from session storage (survives SW termination)
  await messageQueueService.restore();

  // Connect to desktop app
  await connectionService.connect();

  // Set initial badge state
  const connected = connectionService.isConnected();
  updateConnectionBadge(connected ? ConnectionStatus.CONNECTED : ConnectionStatus.DISCONNECTED);

  // Initialize icon
  iconService.update();

  console.log('[Background] Extension initialized');
}

// ============================================
// Logged-In Detection — cookies + DOM (per-tab)
// ============================================

// Strict auth-cookie name patterns. The earlier broad list (session/sid/csrf/
// any httpOnly+secure) caused false positives on banking sites that set
// session/CSRF cookies BEFORE login (e.g., bankhapoalim.co.il). We now only
// match names that are conventional for *authenticated* sessions:
//
//  - Web framework session IDs that are typically set on login: PHPSESSID,
//    JSESSIONID, connect.sid, ASP.NET_SessionId, _session_id (Rails)
//  - Explicit auth tokens: auth_token, access_token, id_token, refresh_token,
//    bearer_token, jwt
//  - Modern cookie prefixes that REQUIRE Secure: __Secure-, __Host-
//    (treated as a strong signal — they're the canonical shape of auth cookies)
//
// We deliberately DO NOT match: csrf, xsrf, _ga, _gid, ai_session, AWSALB,
// ASLB-* (load-balancer affinity), incap_ses_, visid_incap_, etc. — those
// are routinely set pre-login.
const AUTH_COOKIE_NAME_PATTERN = new RegExp(
  '^(' + [
    // Framework session cookies (set on login by these stacks)
    'phpsessid', 'jsessionid', 'connect\\.sid', 'asp\\.net.?sessionid',
    '_session_id', 'rack\\.session', 'laravel_session', 'symfony',
    'ci_session', 'codeigniter_session',
    // Explicit auth/token cookies
    '(remember|auth|access|refresh|bearer|id)[_-]?token',
    '(remember|auth|user)[_-]?session',
    'jwt',
    'authorization',
    'sessionid',  // exact match only (not as a substring of '_ga_sessionid')
    // Modern secure-prefix cookies
    '__secure-.+',
    '__host-.+',
  ].join('|') + ')$',
  'i'
);

async function checkLoggedInByCookies(url) {
  // Returns: { loggedIn: true|false|null, hasCookies: bool }
  if (!url || !/^https?:/i.test(url)) {
    return { loggedIn: null, hasCookies: false };
  }
  try {
    const cookies = await chrome.cookies.getAll({ url });
    if (!cookies || cookies.length === 0) {
      return { loggedIn: false, hasCookies: false };
    }
    // Only an exact name match counts. We also require the value to be
    // long enough (>= 16 chars) to look like a real session token rather
    // than a bool flag like "session=1".
    const sessionLike = cookies.some(c =>
      AUTH_COOKIE_NAME_PATTERN.test(c.name) && (c.value || '').length >= 16
    );
    return { loggedIn: sessionLike, hasCookies: true };
  } catch (e) {
    // Cookies permission missing or runtime error — unknown, not false.
    console.warn('[Background] checkLoggedInByCookies failed:', e?.message || e);
    return { loggedIn: null, hasCookies: false };
  }
}

async function checkLoggedInByDom(tabId) {
  // Returns: true | false | null   (null = no content script / timeout)
  if (typeof tabId !== 'number') return null;
  try {
    const resp = await Promise.race([
      chrome.tabs.sendMessage(tabId, { type: MSG.CHECK_LOGGED_IN_REQUEST }),
      new Promise(resolve => setTimeout(() => resolve(null), 1500)),
    ]);
    if (resp && typeof resp.loggedIn !== 'undefined') {
      return resp.loggedIn;
    }
    return null;
  } catch (e) {
    // No content script in this tab (e.g., chrome:// URL filtered earlier,
    // or page hasn't injected yet). Treat as unknown.
    return null;
  }
}

function combineLoggedInSignals(cookieResult, domResult) {
  const cookieSays = cookieResult?.loggedIn;   // true | false | null
  const domSays    = domResult;                 // true | false | null
  const signals = [];
  if (cookieSays !== null) signals.push('cookie');
  if (domSays    !== null) signals.push('dom');

  // Both unknown → unknown
  if (cookieSays === null && domSays === null) {
    return { loggedIn: null, confidence: null, signals: [] };
  }

  // Both agree → high confidence
  if (cookieSays === true  && domSays === true)  return { loggedIn: true,  confidence: 'high',   signals };
  if (cookieSays === false && domSays === false) return { loggedIn: false, confidence: 'medium', signals };

  // One says yes, one says no → conflict; lean on the positive (false negatives
  // would suppress an alert that should fire; safer to err toward "logged in")
  if (cookieSays === true  || domSays === true) return { loggedIn: true,  confidence: 'medium', signals };

  // One says false, the other unknown → low-confidence false
  return { loggedIn: false, confidence: 'low', signals };
}

// Start
init();

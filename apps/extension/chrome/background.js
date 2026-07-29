// ============================================
// AntiScam Extension - Background Service Worker
// Thin router: receive messages → delegate to services
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
import { dangerStateService } from './services/DangerStateService.js';
import { trackingService, TrackMode } from './services/TrackingService.js';
import { loggedInDetectorService } from './services/LoggedInDetectorService.js';
import { tabAlertService } from './services/TabAlertService.js';

// ============================================
// Connection Status for Badge
// ============================================

const ConnectionStatus = {
  CONNECTED: 'connected',
  DISCONNECTED: 'disconnected',
  RECONNECTING: 'reconnecting'
};

const BADGE_CONFIG = {
  [ConnectionStatus.CONNECTED]:    { color: '#22C55E', text: '' },
  [ConnectionStatus.DISCONNECTED]: { color: '#EF4444', text: '!' },
  [ConnectionStatus.RECONNECTING]: { color: '#F59E0B', text: '' }
};

// ============================================
// Loading State Helpers
// ============================================

function startLoadingState() {
  iconService.startLoadingAnimation();
  chrome.storage.local.set({
    currentPageScanning: true,
    currentPageScore: null,
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
  console.debug('[Background] Badge updated: %s', status);
}

// ============================================
// ImmediateDanger / RemoteControlled flags
// Restored from storage before any handler is registered (ASPS-622).
// ============================================

let immediateDangerMode      = false;
let isDeviceRemoteControlled = false;

// ============================================
// Alarm Listener (MUST be at top level for MV3)
// ============================================

chrome.alarms.onAlarm.addListener((alarm) => {
  console.debug('[Background] Alarm triggered: %s', alarm.name);
  switch (alarm.name) {
    case 'reconnect':  connectionService.attemptReconnect(); break;
    case 'keepalive':  connectionService.sendKeepalive();    break;
    case 'heartbeat':  connectionService.sendHeartbeat();    break;
  }
});

// ============================================
// Configuration
// ============================================

const manifest = chrome.runtime.getManifest();
const CONFIG = { VERSION: manifest.version };

// ============================================
// WebSocket Message Handlers
// ============================================

function setupWebSocketHandlers() {
  // Pong: email and device IP from agent
  connectionService.onMessage(MSG.WS_PONG, async (data) => {
    console.debug('[Background] Connection verified with desktop app');
    try {
      if (data?.email) {
        await chrome.storage.local.set({ userEmail: data.email });
        console.debug('[Background] Email received from agent');
      }
      if (data?.ipAddress) {
        connectionService.setDeviceIpAddress(data.ipAddress);
        console.debug('[Background] Device IP received from agent');
      }
    } catch (e) {
      console.error('[Background] WS_PONG handler error:', e);
    }
  });

  // URL scan result
  connectionService.onMessage(MSG.WS_URL_RESULT, (data) => {
    try {
      handleUrlResult(data);
    } catch (e) {
      console.error('[Background] WS_URL_RESULT handler error:', e);
    }
  });

  // Errors from desktop
  connectionService.onMessage(MSG.WS_ERROR, (data) => {
    console.error('[Background] Error from desktop:', data.message);
  });

  // Notification from desktop
  connectionService.onMessage(MSG.WS_NOTIFICATION, (data) => {
    try {
      handleNotification(data);
    } catch (e) {
      console.error('[Background] WS_NOTIFICATION handler error:', e);
    }
  });

  // Browser tabs request from desktop agent
  connectionService.onMessage(MSG.WS_GET_BROWSER_TABS, async (data) => {
    console.debug('[Background] Browser tabs requested by desktop agent');
    try {
      const tabs = await chrome.tabs.query({});
      const visibleTabs = tabs.filter(
        tab => tab.url && !tab.url.startsWith('chrome://') && !tab.url.startsWith('chrome-extension://')
      );

      const tabData = await Promise.all(
        visibleTabs.map(async (tab) => {
          const result = await loggedInDetectorService.detect(tab.id, tab.url);
          return {
            title:              tab.title     || '',
            url:                tab.url       || '',
            isActive:           tab.active    || false,
            userAgent:          navigator.userAgent,
            timestamp:          tab.lastAccessed
                                  ? new Date(tab.lastAccessed).toISOString()
                                  : new Date().toISOString(),
            loggedIn:           result.loggedIn,
            loggedInConfidence: result.confidence,
            loggedInSignals:    result.signals,
          };
        })
      );

      connectionService.send({
        type:      MSG.WS_BROWSER_TABS_RESPONSE,
        requestId: data.requestId,
        tabs:      tabData
      });
      console.debug('[Background] Sent %d tabs to desktop agent', tabData.length);
    } catch (err) {
      console.error('[Background] Error querying browser tabs:', err);
      connectionService.send({
        type:      MSG.WS_BROWSER_TABS_RESPONSE,
        requestId: data.requestId,
        tabs:      []
      });
    }
  });

  // ImmediateDanger lifecycle
  connectionService.onMessage(MSG.WS_IMMEDIATE_DANGER_STARTED, () => {
    try {
      immediateDangerMode = true;
      console.debug('[Background] ImmediateDanger mode ON');
      dangerStateService.persist({
        immediateDangerMode, isDeviceRemoteControlled,
        trackedDomains: trackingService.getMap(),
        sensitiveDomainCache: tabAlertService.getSensitiveDomainCache()
      });
    } catch (e) {
      console.error('[Background] WS_IMMEDIATE_DANGER_STARTED handler error:', e);
    }
  });

  connectionService.onMessage(MSG.WS_IMMEDIATE_DANGER_ENDED, () => {
    try {
      immediateDangerMode = false;
      console.debug('[Background] ImmediateDanger mode OFF');
      dangerStateService.persist({
        immediateDangerMode, isDeviceRemoteControlled,
        trackedDomains: trackingService.getMap(),
        sensitiveDomainCache: tabAlertService.getSensitiveDomainCache()
      });
    } catch (e) {
      console.error('[Background] WS_IMMEDIATE_DANGER_ENDED handler error:', e);
    }
  });

  // Remote-controlled gate
  connectionService.onMessage(MSG.WS_SET_REMOTE_CONTROLLED, async (data) => {
    try {
      const next = !!(data?.isDeviceRemoteControlled);
      if (next === isDeviceRemoteControlled) return;
      isDeviceRemoteControlled = next;
      console.debug('[Background] IsDeviceRemoteControlled = %s', next);
      dangerStateService.persist({
        immediateDangerMode, isDeviceRemoteControlled,
        trackedDomains: trackingService.getMap(),
        sensitiveDomainCache: tabAlertService.getSensitiveDomainCache()
      });

      if (next) {
        const allTabs = await chrome.tabs.query({});
        for (const tab of allTabs) {
          if (!tab.url || !/^https?:/i.test(tab.url)) continue;
          try {
            const snap = await tabAlertService.refreshTabControl(tab.id, tab.url);
            if (snap.isSensitiveWebsite !== false) {
              tabAlertService.sendTabChangedAlert(tab.id, snap, connectionService);
            }
          } catch (e) {
            console.warn('[Background] Existing-tab scan on remote-controlled transition failed:', e);
          }
        }
      }
    } catch (e) {
      console.error('[Background] WS_SET_REMOTE_CONTROLLED handler error:', e);
    }
  });

  // Remote access alert from desktop
  connectionService.onMessage(MSG.REMOTE_ACCESS_ALERT, async (data) => {
    try {
      if (data.direction !== 'incoming') {
        console.debug('[Background] Outgoing connection — no warning needed');
        return;
      }

      stateManager.update({
        'warning.active':        true,
        'warning.toolId':        data.toolId || data.remote_app,
        'warning.toolName':      REMOTE_TOOL_NAMES[data.toolId] || REMOTE_TOOL_NAMES[REMOTE_TOOL.UNKNOWN],
        'warning.direction':     data.direction,
        'warning.remoteCountry': data.remote_country || ''
      });

      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        if (!tab.url ||
            tab.url.startsWith('chrome://') ||
            tab.url.startsWith('chrome-extension://') ||
            tab.url.startsWith('edge://') ||
            tab.url.startsWith('about:')) {
          continue;
        }
        try {
          await chrome.tabs.sendMessage(tab.id, {
            type:          MSG.REMOTE_ACCESS_WARNING_SHOW,
            toolId:        data.toolId || data.remote_app,
            toolName:      REMOTE_TOOL_NAMES[data.toolId] || REMOTE_TOOL_NAMES[REMOTE_TOOL.UNKNOWN],
            direction:     data.direction,
            remoteCountry: data.remote_country
          });
        } catch (e) {
          console.debug('[Background] Could not send warning to tab %s: %s', tab.id, e.message);
        }
      }
    } catch (e) {
      console.error('[Background] REMOTE_ACCESS_ALERT handler error:', e);
    }
  });

  // Session end from desktop
  connectionService.onMessage(MSG.REMOTE_ACCESS_SESSION_END, async (data) => {
    try {
      stateManager.update({ 'warning.active': false });
      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        try {
          await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
        } catch (_) { /* tab may not have content script */ }
      }
    } catch (e) {
      console.error('[Background] REMOTE_ACCESS_SESSION_END handler error:', e);
    }
  });

  // App closed from desktop
  connectionService.onMessage(MSG.REMOTE_ACCESS_APP_CLOSED, async (data) => {
    try {
      stateManager.update({ 'warning.active': false });
      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        try {
          await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
        } catch (_) { /* tab may not have content script */ }
      }
    } catch (e) {
      console.error('[Background] REMOTE_ACCESS_APP_CLOSED handler error:', e);
    }
  });

  // SetTrackedDomains from backend
  connectionService.onMessage(MSG.TRACKED_DOMAINS_SET, async (data) => {
    try {
      if (!data.domains || !Array.isArray(data.domains)) return;

      await chrome.storage.local.set({
        trackedDomains:        data.domains,
        alwaysSendFormSubmits: data.alwaysSendFormSubmits || false
      });
      console.debug('[Background] Stored %d tracked domains', data.domains.length);

      trackingService.rebuildFromBackend(data.domains);
      console.debug('[Background] In-memory trackedDomains rebuilt: %d domain(s)',
        trackingService.getMap().size);

      dangerStateService.persist({
        immediateDangerMode, isDeviceRemoteControlled,
        trackedDomains: trackingService.getMap(),
        sensitiveDomainCache: tabAlertService.getSensitiveDomainCache()
      });

      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        if (tab.url?.startsWith('http')) {
          try {
            await chrome.tabs.sendMessage(tab.id, {
              type:                  MSG.TRACKED_DOMAINS_UPDATED,
              domains:               data.domains,
              alwaysSendFormSubmits: data.alwaysSendFormSubmits || false
            });
          } catch (_) { /* no content script — ignore */ }
        }
      }
    } catch (e) {
      console.error('[Background] TRACKED_DOMAINS_SET handler error:', e);
    }
  });
}

// ============================================
// URL Result Handler
// ============================================

function handleUrlResult(data) {
  console.debug('[Background] URL result received: score=%s action=%s analyzing=%s',
    data.score, data.protectiveAction, data.analyzing);

  // Update sensitive-domain cache from result
  tabAlertService.updateSensitiveCache(data.url, data.isSensitiveWebsite);
  if (typeof data.isSensitiveWebsite === 'boolean' && data.url) {
    dangerStateService.persist({
      immediateDangerMode, isDeviceRemoteControlled,
      trackedDomains: trackingService.getMap(),
      sensitiveDomainCache: tabAlertService.getSensitiveDomainCache()
    });
  }

  if (data.analyzing === true) {
    console.debug('[Background] Analysis in progress, waiting for final result');
    return;
  }

  stopLoadingState();

  // Process EnableUrlTracking / SetTrackMode protective actions
  if (data.protectiveActions && Array.isArray(data.protectiveActions)) {
    for (const action of data.protectiveActions) {
      if (!action.message) continue;
      if (action.message.startsWith('EnableUrlTracking|')) {
        const parts = action.message.split('|');
        if (parts.length >= 3) {
          trackingService.enableUrlTracking(
            parts[1], parseInt(parts[2], 10) || 30, TrackMode.Surf
          );
        }
      } else if (action.message.startsWith('SetTrackMode|')) {
        const parts = action.message.split('|');
        if (parts.length >= 3) {
          trackingService.setTrackMode(
            parts[1],
            parseInt(parts[2], 10) || TrackMode.Surf,
            parts[3] || '',
            parseInt(parts[4], 10) || 30
          );
        }
      }
    }
  }

  const result = scanService.handleResult(data);

  if (result) {
    iconService.setColorByAction(result.protectiveAction, result.score);
    protectionService.executeAction(
      result.protectiveAction,
      result.riskType,
      result.score,
      result.tabId ?? null
    );
  }
}

// ============================================
// Notification Handler
// ============================================

function handleNotification(data) {
  chrome.notifications.create({
    type:     'basic',
    iconUrl:  'icons/icon128.png',
    title:    data.title   || 'AntiScam Alert',
    message:  data.message || 'New notification from server'
  });
}

// ============================================
// Message Bus Handlers (chrome.runtime.sendMessage)
// ============================================

function buildStatusResponse() {
  return {
    isConnectedToDesktop:  connectionService.isConnected(),
    isConnectedToBackend:  false,
    connectedPort:         stateManager.get('connection.port'),
    cacheSize:             cacheService.size(),
    version:               CONFIG.VERSION,
    currentPageScore:      stateManager.get('scan.score'),
    currentPageRiskType:   stateManager.get('scan.riskType'),
    currentPageAction:     stateManager.get('scan.protectiveAction'),
    warningActive:         stateManager.get('warning.active')       || false,
    warningToolName:       stateManager.get('warning.toolName')     || null,
    warningDirection:      stateManager.get('warning.direction')    || null,
    warningRemoteCountry:  stateManager.get('warning.remoteCountry') || null
  };
}

function setupMessageHandlers() {
  messageBus.init();

  // Status
  messageBus.on(MSG.STATUS_GET, () => buildStatusResponse());

  // Reconnect
  messageBus.on(MSG.CONNECTION_RECONNECT, async () => {
    const connected = await connectionService.reconnect();
    return { success: connected };
  });

  // Clear cache
  messageBus.on(MSG.CACHE_CLEAR, () => {
    cacheService.clear();
    return { success: true };
  });

  // Scan current page
  messageBus.on(MSG.SCAN_CURRENT, async () => {
    startLoadingState();
    const result = await scanService.scanCurrentTab();
    if (result) {
      stopLoadingState();
      iconService.setColorByAction(result.protectiveAction, result.score);
    }
    return { success: true };
  });

  // User signed in
  messageBus.on(MSG.AUTH_SIGN_IN, async (payload) => {
    console.debug('[Background] User signed in');
    stateManager.update({ 'user.loggedIn': true, 'user.email': payload.email });
    connectionService.send({ type: MSG.WS_USER_AUTH, email: payload.email });
    return { success: true };
  });

  // User signed out
  messageBus.on(MSG.AUTH_SIGN_OUT, async () => {
    console.debug('[Background] User signed out');
    stateManager.update({ 'user.loggedIn': false, 'user.email': null });
    connectionService.send({ type: MSG.WS_USER_SIGNOUT });
    return { success: true };
  });

  // Warning dismiss (from content script)
  messageBus.on(MSG.REMOTE_ACCESS_WARNING_DISMISS, async () => {
    try {
      console.debug('[Background] Warning dismiss requested');
      stateManager.update({ 'warning.active': false });
      connectionService.send({ type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        try {
          await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
        } catch (_) { /* no content script */ }
      }
      return { success: true };
    } catch (e) {
      console.error('[Background] Warning dismiss error:', e);
      return { success: false };
    }
  });

  // Close remote session
  messageBus.on(MSG.REMOTE_ACCESS_CLOSE_SESSION, async (data) => {
    const appName = (data?.app)
      ? data.app
      : (stateManager.get('warning.toolName') || '').toLowerCase();
    try {
      const result = await connectionService.sendAndWait(
        { type: MSG.REMOTE_ACCESS_CLOSE_SESSION, app: appName },
        MSG.REMOTE_ACCESS_CLOSE_SESSION_RESULT,
        7000
      );
      console.debug('[Background] Close session result: success=%s', result.success);
      return { success: result.success === true, reason: result.reason || 'unknown' };
    } catch (err) {
      console.warn('[Background] Close session error / timeout:', err.message);
      return { success: false, reason: 'timeout' };
    }
  });

  // User continued anyway
  messageBus.on(MSG.REMOTE_ACCESS_CONTINUED, async () => {
    try {
      console.debug('[Background] User continued anyway');
      stateManager.update({ 'warning.active': false });
      connectionService.send({ type: MSG.REMOTE_ACCESS_CONTINUED });
      const tabs = await chrome.tabs.query({});
      for (const tab of tabs) {
        try {
          await chrome.tabs.sendMessage(tab.id, { type: MSG.REMOTE_ACCESS_WARNING_DISMISS });
        } catch (_) { /* no content script */ }
      }
      return { success: true };
    } catch (e) {
      console.error('[Background] REMOTE_ACCESS_CONTINUED handler error:', e);
      return { success: false };
    }
  });

  // Form submitted (from content script)
  messageBus.on(MSG.FORM_SUBMITTED, async (payload) => {
    try {
      const ipAddress = connectionService.getDeviceIpAddress();
      const timezoneOffset = new Date().getTimezoneOffset() / -60;
      const timezone = `UTC${timezoneOffset >= 0 ? '+' : ''}${timezoneOffset}`;

      let tabId = '';
      try {
        const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
        if (tabs[0]) tabId = tabs[0].id.toString();
      } catch (e) {
        console.error('[Background] Error getting tab ID for form submission:', e);
      }

      const alert = {
        type:             MSG.WS_TRACK_URL_ALERT,
        Url:              payload.data.url,
        FromUrl:          payload.data.fromUrl,
        Duration:         0,
        ScamInProgressKey: payload.data.scamInProgressKey || '',
        IPAddress:        ipAddress,
        UserAgent:        navigator.userAgent,
        TabId:            tabId,
        Timezone:         timezone,
        FormAction:       payload.data.formAction,
        Method:           payload.data.method,
        HasPasswordField: payload.data.hasPasswordField,
        HasEmailField:    payload.data.hasEmailField,
        HasCreditCard:    payload.data.hasCreditCard,
        FieldCount:       payload.data.fieldCount
      };

      console.debug('[Background] Sending TrackUrlAlert for form submission');
      connectionService.send(alert);
      return { success: true };
    } catch (e) {
      console.error('[Background] FORM_SUBMITTED handler error:', e);
      return { success: false };
    }
  });
}

// ============================================
// Tab Event Handlers
// ============================================

function setupTabListeners() {
  const lastScannedUrl = new Map();

  async function triggerScan(tabId, url) {
    if (!url || !url.startsWith('http')) return;
    try {
      await chrome.tabs.get(tabId);
    } catch (_) {
      return; // Tab no longer exists
    }
    if (lastScannedUrl.get(tabId) === url) return;
    lastScannedUrl.set(tabId, url);
    console.debug('[Background] Auto-scan triggered');

    startLoadingState();
    chrome.storage.local.set({
      [`tab_${tabId}_score`]:    null,
      [`tab_${tabId}_riskType`]: [],
      [`tab_${tabId}_action`]:   0
    });

    let result;
    try {
      result = await scanService.scan(tabId, url);
    } catch (e) {
      console.error('[Background] Scan error:', e);
      stopLoadingState();
      iconService.setColor('gray');
      return;
    }

    if (result) {
      stopLoadingState();
      iconService.setColorByAction(result.protectiveAction, result.score);
      return;
    }

    // No immediate result: set a 30-second timeout
    setTimeout(() => {
      chrome.storage.local.get(['currentPageScanning'], (data) => {
        if (data.currentPageScanning) {
          stopLoadingState();
          iconService.setColor('gray');
          chrome.storage.local.set({
            currentPageScore:    null,
            currentPageScanning: false
          });
          console.debug('[Background] Scan timeout — showing neutral state');
        }
      });
    }, 30000);
  }

  // Tab updated
  chrome.tabs.onUpdated.addListener(async (tabId, changeInfo, tab) => {
    try {
      if (changeInfo.url) {
        // Navigation tracking
        const navResult = trackingService.processNavigation(
          tabId, changeInfo.url, () => connectionService.getDeviceIpAddress()
        );
        if (navResult) {
          connectionService.send(navResult.alert);
        }
      }
      if (changeInfo.status === 'complete' && tab.url) {
        await triggerScan(tabId, tab.url);
      } else if (changeInfo.url) {
        await triggerScan(tabId, changeInfo.url);
      }
    } catch (e) {
      console.error('[Background] tabs.onUpdated error:', e);
    }
  });

  // Tab created
  chrome.tabs.onCreated.addListener(async (tab) => {
    try {
      if (!tab?.id || !tab.url) return;
      const snap = await tabAlertService.refreshTabControl(tab.id, tab.url);
      if (isDeviceRemoteControlled && snap.isSensitiveWebsite === true) {
        tabAlertService.sendTabChangedAlert(tab.id, snap, connectionService);
      }
    } catch (e) {
      console.error('[Background] tabs.onCreated error:', e);
    }
  });

  // Tab removed
  chrome.tabs.onRemoved.addListener((tabId) => {
    try {
      lastScannedUrl.delete(tabId);
      trackingService.removeTab(tabId);

      const prev = tabAlertService.removeTab(tabId);
      if (!prev) {
        cleanup(tabId);
        return;
      }

      const shouldSend =
        immediateDangerMode ||
        (isDeviceRemoteControlled && prev.isSensitiveWebsite === true);
      if (shouldSend) {
        tabAlertService.sendTabClosedAlert(tabId, prev.url, connectionService);
      }

      cleanup(tabId);
    } catch (e) {
      console.error('[Background] tabs.onRemoved error:', e);
    }

    function cleanup(id) {
      chrome.storage.local.remove([
        `tab_${id}`,
        `tab_${id}_score`,
        `tab_${id}_riskType`,
        `tab_${id}_action`
      ]);
    }
  });

  // webNavigation completed (more reliable for some navigations)
  chrome.webNavigation.onCompleted.addListener(async (details) => {
    if (details.frameId !== 0) return;
    try {
      const navResult = trackingService.processNavigation(
        details.tabId, details.url, () => connectionService.getDeviceIpAddress()
      );
      if (navResult) connectionService.send(navResult.alert);
      await triggerScan(details.tabId, details.url);
    } catch (e) {
      console.error('[Background] webNavigation.onCompleted error:', e);
    }
  });

  // History state updated (SPA navigations)
  chrome.webNavigation.onHistoryStateUpdated.addListener(async (details) => {
    if (details.frameId !== 0) return;
    try {
      await triggerScan(details.tabId, details.url);
    } catch (e) {
      console.error('[Background] webNavigation.onHistoryStateUpdated error:', e);
    }
  });

  // Tab activated — load cached score, never trigger new scan
  chrome.tabs.onActivated.addListener(async (activeInfo) => {
    try {
      const tabId = activeInfo.tabId;
      const tab   = await chrome.tabs.get(tabId);

      const data = await chrome.storage.local.get([
        `tab_${tabId}_score`,
        `tab_${tabId}_riskType`,
        `tab_${tabId}_action`
      ]);

      if (data[`tab_${tabId}_score`] != null) {
        stopLoadingState();
        chrome.storage.local.set({
          currentPageScore:      data[`tab_${tabId}_score`],
          currentPageRiskType:   data[`tab_${tabId}_riskType`] || [],
          currentPageAction:     data[`tab_${tabId}_action`]   || 0,
          currentPageScanning:   false
        });
      } else if (tab.url?.startsWith('http')) {
        const cached = cacheService.get(tab.url);
        if (cached?.score != null) {
          stopLoadingState();
          chrome.storage.local.set({
            [`tab_${tabId}_score`]:    cached.score,
            [`tab_${tabId}_riskType`]: cached.riskType      || [],
            [`tab_${tabId}_action`]:   cached.protectiveAction || 0,
            currentPageScore:          cached.score,
            currentPageRiskType:       cached.riskType      || [],
            currentPageAction:         cached.protectiveAction || 0,
            currentPageScanning:       false
          });
        } else {
          chrome.storage.local.set({
            currentPageScore:    null,
            currentPageRiskType: [],
            currentPageAction:   0,
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
// Tab update listener for TabAlertService
// (sensitive-domain + loggedIn re-check)
// ============================================

chrome.tabs.onUpdated.addListener(async (tabId, changeInfo, tab) => {
  if (!tab?.url) return;
  const urlChanged = !!changeInfo.url;
  const completed  = changeInfo.status === 'complete';
  if (!urlChanged && !completed) return;

  try {
    const prev = tabAlertService.getTabControl(tabId);
    const snap = await tabAlertService.refreshTabControl(tabId, tab.url);

    if (!isDeviceRemoteControlled) return;

    const wasSensitive       = prev?.isSensitiveWebsite === true;
    const nowSensitive       = snap.isSensitiveWebsite === true;
    const loggedInTransition = nowSensitive && prev && prev.isLoggedIn !== snap.isLoggedIn;

    if ((urlChanged && (nowSensitive || wasSensitive)) || loggedInTransition) {
      tabAlertService.sendTabChangedAlert(tabId, snap, connectionService);
    }
  } catch (e) {
    console.warn('[Background] TabAlert onUpdated error:', e);
  }
});

// ============================================
// Cookie-Driven LoggedIn Re-evaluation
// ============================================

chrome.cookies.onChanged.addListener((info) => {
  tabAlertService.handleCookieChange(info, isDeviceRemoteControlled, connectionService);
});

// ============================================
// State Change Listeners
// ============================================

function setupStateListeners() {
  stateManager.subscribe('connection.desktop', (connected) => {
    if (connected) {
      console.debug('[Background] Connected to desktop app');
      chrome.storage.local.set({ notFullyProtected: false });
      updateConnectionBadge(ConnectionStatus.CONNECTED);
    } else {
      console.debug('[Background] Disconnected from desktop app');
      chrome.storage.local.set({
        notFullyProtected: true,
        warningMessage:    'Desktop app not running. Please start AntiScam Desktop for protection.'
      });
      updateConnectionBadge(ConnectionStatus.DISCONNECTED);
    }
  });

  stateManager.subscribe('connection.reconnecting', (reconnecting) => {
    if (reconnecting) {
      updateConnectionBadge(ConnectionStatus.RECONNECTING);
    }
  });
}

// ============================================
// Initialization
// ============================================

async function init() {
  console.debug('[Background] AntiScam Extension v%s starting', CONFIG.VERSION);

  await stateManager.init();
  await cacheService.init();
  await authService.init();

  // ASPS-622: Restore persisted danger/tracking state BEFORE any handler is
  // registered so that events queued by Chrome during SW restart see correct state.
  {
    const restored = await dangerStateService.restore();
    immediateDangerMode      = restored.immediateDangerMode;
    isDeviceRemoteControlled = restored.isDeviceRemoteControlled;
    trackingService.setMap(restored.trackedDomains);
    tabAlertService.restore(new Map(), restored.sensitiveDomainCache);
    console.debug(
      '[Background] Restored danger state: dangerMode=%s rc=%s tracked=%d sensitive=%d',
      immediateDangerMode, isDeviceRemoteControlled,
      restored.trackedDomains.size, restored.sensitiveDomainCache.size
    );
  }

  setupWebSocketHandlers();
  setupMessageHandlers();
  setupTabListeners();
  setupStateListeners();

  await messageQueueService.restore();
  await connectionService.connect();

  updateConnectionBadge(
    connectionService.isConnected() ? ConnectionStatus.CONNECTED : ConnectionStatus.DISCONNECTED
  );

  iconService.update();

  console.debug('[Background] Extension initialized');
}

// Start
init();

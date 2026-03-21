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
      const tabData = tabs
        .filter(tab => tab.url && !tab.url.startsWith('chrome://') && !tab.url.startsWith('chrome-extension://'))
        .map(tab => ({
          title:     tab.title     || '',
          url:       tab.url       || '',
          isActive:  tab.active    || false,
          userAgent: navigator.userAgent,
          timestamp: tab.lastAccessed ? new Date(tab.lastAccessed).toISOString() : new Date().toISOString()
        }));
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
}

function handleUrlResult(data) {
  console.log('[Background] URL result received:', { url: data.url, score: data.score, action: data.protectiveAction, analyzing: data.analyzing });

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

}

// ============================================
// URL Tracking Helper
// ============================================

// Track URL navigation history per tab
const tabNavigationHistory = new Map();
const tabActivationTimes = new Map();

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

// Start
init();

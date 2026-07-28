// ============================================
// AntiScam Extension - Scan Service
// Handles page scanning and risk assessment
// ============================================

import stateManager from '../state/StateManager.js';
import connectionService from './ConnectionService.js';
import cacheService from './CacheService.js';
import { MSG, PROTECTIVE_ACTION } from '../messaging/MessageTypes.js';
import '../generated/messaging/v1/message-envelope.js';

const messagingV1 = globalThis.AspsMessagingV1;

class ScanService {
  constructor() {
    this.pendingScans = new Map();
    this.recentlyScanned = new Map(); // Track recently scanned URLs to avoid duplicates
    this.recentScanTTL = 60000; // 60 seconds - don't rescan same URL within this time
    this.scanTimeout = 30000; // 30 seconds - increased for slower analysis
  }

  // Check if URL points to a local/loopback address — never send to backend
  isLocalUrl(url) {
    try {
      const hostname = new URL(url).hostname.toLowerCase();
      return (
        hostname === 'localhost' ||
        hostname === '127.0.0.1' ||
        hostname.startsWith('127.') ||
        hostname === '::1' ||
        hostname === '0.0.0.0'
      );
    } catch {
      return false;
    }
  }

  // Scan a URL
  async scan(tabId, url) {
    if (!url || !url.startsWith('http')) {
      console.log('[ScanService] Skipping non-http URL:', url);
      return null;
    }

    if (this.isLocalUrl(url)) {
      console.log('[ScanService] Skipping local URL:', url);
      return null;
    }

    const domain = this.extractDomain(url);
    console.log(`[ScanService] Scanning: ${domain}`);

    // Update state
    stateManager.update({
      'scan.currentUrl': url,
      'scan.loading': true,
      'scan.error': null
    });

    // Check cache first
    const cached = cacheService.get(url);
    if (cached) {
      console.log(`[ScanService] Cache hit for ${domain}`);
      this.handleResult(cached, true);
      return cached;
    }

    // Check if we recently scanned this URL (avoid duplicate scans during analyzing phase)
    const recentScan = this.recentlyScanned.get(url);
    if (recentScan && (Date.now() - recentScan) < this.recentScanTTL) {
      console.log(`[ScanService] Already scanned recently, skipping: ${domain}`);
      return null;
    }

    // Mark as recently scanned
    this.recentlyScanned.set(url, Date.now());
    // Cleanup old entries
    this.cleanupRecentlyScanned();

    // Set scanning state in storage for this tab
    chrome.storage.local.set({
      currentPageScanning: true,
      [`tab_${tabId}_scanning`]: true
    });

    // Get page info from content script
    const pageInfo = await this.getPageInfo(tabId);

    // Save tab data for popup
    await this.saveTabData(tabId, domain, pageInfo);

    // Send to desktop app
    const canonicalUrl = messagingV1.canonicalizeUrl(url);
    const message = messagingV1.createEnvelope(
      'url_scan.request',
      'extension',
      { deviceId: null, tabId: tabId.toString(), url: canonicalUrl },
      {
      trackers: pageInfo?.trackers || [],
      iframes: pageInfo?.iframes || [],
      ipAddress: connectionService.getDeviceIpAddress(),
      originalUrl: url
      });

    if (!connectionService.send(message)) {
      console.log('[ScanService] Not connected to desktop app');
      stateManager.update({
        'scan.loading': false,
        'scan.error': 'Not connected to desktop app'
      });
      return null;
    }

    // Wait for result (with timeout)
    return new Promise((resolve) => {
      const timeoutId = setTimeout(() => {
        this.pendingScans.delete(message.requestId);
        stateManager.update({
          'scan.loading': false,
          'scan.error': 'Scan timeout'
        });
        resolve(null);
      }, this.scanTimeout);

      this.pendingScans.set(message.requestId, {
        resolve, timeoutId, url: canonicalUrl, tabId: tabId.toString(),
        correlationId: message.correlationId
      });
    });
  }

  // Get page info from content script
  async getPageInfo(tabId) {
    try {
      return await chrome.tabs.sendMessage(tabId, { type: MSG.PAGE_INFO_REQUEST });
    } catch (e) {
      console.log('[ScanService] Content script not ready');
      return null;
    }
  }

  // Save tab data for popup
  async saveTabData(tabId, domain, pageInfo) {
    const tabData = {
      domain: domain,
      fbPixel: pageInfo?.trackers?.filter(t => t.Type === 'fbPixel').length || 0,
      iframeDomains: pageInfo?.iframes || [],
      fromCache: false
    };

    await chrome.storage.local.set({ [`tab_${tabId}`]: tabData });
  }

  // Handle scan result from desktop app
  // Uses server values directly - no local calculations
  handleResult(data, fromCache = false) {
    if (data?.schemaVersion) {
      try {
        messagingV1.validateEnvelope(data, true);
      } catch (error) {
        console.warn('[ScanService] Invalid v1 result:', error.code);
        return null;
      }

      const pending = this.pendingScans.get(data.requestId);
      if (!pending || pending.correlationId !== data.correlationId ||
          pending.url !== data.context.url || pending.tabId !== data.context.tabId) {
        console.warn('[ScanService] Stale or mismatched result:', data.requestId);
        return null;
      }

      if (data.messageType === 'url_scan.accepted') {
        return data;
      }

      data = data.messageType === 'url_scan.error'
        ? {
            error: true,
            message: data.outcome.error.message,
            url: data.context.url,
            requestId: data.requestId
          }
        : {
            ...data.outcome.result,
            url: data.context.url,
            requestId: data.requestId
          };
    }

    console.log('[ScanService] Result received (from server):', data);

    // Skip if still analyzing (no final result yet)
    if (data.analyzing === true) {
      console.log('[ScanService] Still analyzing, waiting for final result');
      return null;
    }

    // Check for errors
    if (data.error) {
      stateManager.update({
        'scan.loading': false,
        'scan.error': data.message
      });
      return null;
    }

    // Skip if no score (invalid result)
    if (data.score === undefined || data.score === null) {
      console.log('[ScanService] No score in result, skipping');
      return null;
    }

    // Use values directly from server - no modifications
    const score = data.score;
    const riskType = data.riskType || [];
    const protectiveAction = data.protectiveAction ?? PROTECTIVE_ACTION.NONE;
    const ttl = data.ttl || 3600;

    // Clear from recently scanned - we have final result, allow future rescans
    if (data.url) {
      this.clearRecentlyScan(data.url);
    }

    // Update state
    stateManager.update({
      'scan.score': score,
      'scan.riskType': riskType,
      'scan.protectiveAction': protectiveAction,
      'scan.loading': false,
      'scan.error': null
    });

    // Save to storage for popup - per-tab and global
    // Get current active tab to save per-tab data
    chrome.tabs.query({ active: true, currentWindow: true }).then(tabs => {
      if (tabs[0]) {
        const tabId = tabs[0].id;
        chrome.storage.local.set({
          [`tab_${tabId}_score`]: score,
          [`tab_${tabId}_riskType`]: riskType,
          [`tab_${tabId}_action`]: protectiveAction,
          [`tab_${tabId}_scanning`]: false,
          // Also update global for current tab
          currentPageScore: score,
          currentPageRiskType: riskType,
          currentPageAction: protectiveAction,
          currentPageScanning: false
        });
      } else {
        // No active tab, just save global
        chrome.storage.local.set({
          currentPageScore: score,
          currentPageRiskType: riskType,
          currentPageAction: protectiveAction,
          currentPageScanning: false
        });
      }
    });

    // Cache the result if not already cached
    if (!fromCache && data.url) {
      cacheService.set(data.url, { score, riskType, protectiveAction, ttl });
    }

    // Resolve pending scan if exists
    const pendingKey = data.requestId || [...this.pendingScans.entries()]
      .find(([, pending]) => pending.url === data.url)?.[0];
    if (pendingKey && this.pendingScans.has(pendingKey)) {
      const { resolve, timeoutId } = this.pendingScans.get(pendingKey);
      clearTimeout(timeoutId);
      this.pendingScans.delete(pendingKey);
      resolve({ score, riskType, protectiveAction, fromCache });
    }

    return { score, riskType, protectiveAction, fromCache };
  }

  // Scan current active tab
  async scanCurrentTab() {
    try {
      const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
      if (tabs[0]) {
        // Reset current score
        stateManager.set('scan.score', null);
        await chrome.storage.local.remove(['currentPageScore']);

        return await this.scan(tabs[0].id, tabs[0].url);
      }
    } catch (e) {
      console.error('[ScanService] Error scanning current tab:', e);
    }
    return null;
  }

  // Extract domain from URL
  extractDomain(url) {
    try {
      return new URL(url).hostname;
    } catch {
      return url;
    }
  }

  // Get current scan state
  getState() {
    return {
      url: stateManager.get('scan.currentUrl'),
      score: stateManager.get('scan.score'),
      riskType: stateManager.get('scan.riskType'),
      loading: stateManager.get('scan.loading'),
      error: stateManager.get('scan.error')
    };
  }

  // Clear pending scans
  clearPending() {
    for (const [, { timeoutId }] of this.pendingScans) {
      clearTimeout(timeoutId);
    }
    this.pendingScans.clear();
  }

  // Cleanup old entries from recentlyScanned
  cleanupRecentlyScanned() {
    const now = Date.now();
    for (const [url, timestamp] of this.recentlyScanned) {
      if (now - timestamp > this.recentScanTTL) {
        this.recentlyScanned.delete(url);
      }
    }
  }

  // Clear recently scanned URL (call when we get final result)
  clearRecentlyScan(url) {
    this.recentlyScanned.delete(url);
  }
}

// Singleton instance
export const scanService = new ScanService();
export default scanService;

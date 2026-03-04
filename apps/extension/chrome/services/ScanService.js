// ============================================
// AntiScam Extension - Scan Service
// Handles page scanning and risk assessment
// ============================================

import stateManager from '../state/StateManager.js';
import connectionService from './ConnectionService.js';
import cacheService from './CacheService.js';
import { MSG, PROTECTIVE_ACTION } from '../messaging/MessageTypes.js';

class ScanService {
  constructor() {
    this.pendingScans = new Map();
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
    const message = {
      type: MSG.WS_URL_CHECK,
      url: url,
      trackers: pageInfo?.trackers || [],
      iframes: pageInfo?.iframes || [],
      ipAddress: connectionService.getDeviceIpAddress()
    };

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
        this.pendingScans.delete(domain);
        stateManager.update({
          'scan.loading': false,
          'scan.error': 'Scan timeout'
        });
        resolve(null);
      }, this.scanTimeout);

      this.pendingScans.set(domain, { resolve, timeoutId, url });
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
    const domain = this.extractDomain(data.url);
    if (this.pendingScans.has(domain)) {
      const { resolve, timeoutId } = this.pendingScans.get(domain);
      clearTimeout(timeoutId);
      this.pendingScans.delete(domain);
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
}

// Singleton instance
export const scanService = new ScanService();
export default scanService;

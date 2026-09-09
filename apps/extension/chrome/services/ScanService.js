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

// ============================================
// ASPS-759: loopback canonicalization helpers
// ============================================
// isLocalUrl() below is a security guard — it decides whether a URL is
// EVER allowed to leave the machine (see scan() which skips local URLs).
// String-literal matching (e.g. hostname.startsWith('127.')) is bypassable
// by alternate encodings of the same loopback address (decimal/hex/octal
// IPv4, IPv4-mapped IPv6, fully-expanded IPv6, trailing-dot hostnames).
// These helpers canonicalize the host to a numeric form and test it
// against the actual loopback ranges instead of trusting string shape.
//
// Boundary drawn (intentional, do not silently widen):
//   - IPv4 127.0.0.0/8 (any decimal/hex/octal/dotted encoding) -> local.
//   - IPv4 0.0.0.0 (any encoding) -> local (existing behavior, kept).
//   - IPv6 ::1 (any equivalent expansion) -> local.
//   - IPv6 IPv4-mapped ::ffff:0:0/96 whose embedded IPv4 is in 127.0.0.0/8
//     -> local. (::ffff:0.0.0.0 itself is NOT special-cased to local; only
//     the 127.0.0.0/8 embedded range is treated as loopback there, matching
//     the IPv4 rule above.)
//   - 'localhost' and 'localhost.' (single trailing dot) -> local. Other
//     *.localhost subdomains are deliberately NOT covered — Chrome does not
//     route those to loopback the way it does the bare name, and treating
//     arbitrary attacker-chosen subdomains as "local" would itself be a
//     footgun. Flagged here rather than silently added.
//   - Any host that cannot be parsed as a valid IPv4/IPv6 literal is NOT
//     treated as local (falls through to false). This guard exists to stop
//     internal URLs leaking to the backend, but the failure mode for an
//     ambiguous host must not be "treat it as local" (that would silently
//     stop real external sites from ever being scanned) — it must be
//     "treat it as external" so isLocalUrl() stays conservative in the
//     direction of "when unsure, still scan it, don't skip the guard".
//   - IPv6 '::' (all-zero/unspecified) is intentionally NOT treated as
//     local — it was not in the enumerated bypass list and is not a
//     loopback address per RFC 4291 (it is the unspecified address).

// Parse one dot-separated IPv4 component per the WHATWG URL "IPv4 number
// parser": a leading "0x"/"0X" means hex, a leading "0" (with more digits
// following) means octal, otherwise decimal. Returns null when the part is
// not a valid number in its radix.
function parseIPv4Part(part) {
  if (part === '') {
    return null;
  }
  let radix = 10;
  let digits = part;
  if (digits.length >= 2 && digits[0] === '0' && (digits[1] === 'x' || digits[1] === 'X')) {
    radix = 16;
    digits = digits.slice(2);
  } else if (digits.length >= 2 && digits[0] === '0') {
    radix = 8;
    digits = digits.slice(1);
  }
  if (digits === '') {
    return 0;
  }
  const validPattern = radix === 16 ? /^[0-9a-f]+$/i : radix === 8 ? /^[0-7]+$/ : /^[0-9]+$/;
  if (!validPattern.test(digits)) {
    return null;
  }
  const value = parseInt(digits, radix);
  return Number.isNaN(value) ? null : value;
}

// Canonicalize an IPv4 host (1-4 dot-separated parts, each decimal/hex/octal,
// per the WHATWG URL IPv4 parser) to its 32-bit unsigned integer value.
// Returns null when the host is not a valid IPv4 literal (e.g. a domain
// name) — that is not an error, just "not an IPv4 address".
function parseIPv4ToInt(hostname) {
  if (typeof hostname !== 'string' || hostname === '') {
    return null;
  }
  let parts = hostname.split('.');
  // A single trailing dot is allowed (e.g. "127.0.0.1.").
  if (parts.length > 1 && parts[parts.length - 1] === '') {
    parts = parts.slice(0, -1);
  }
  if (parts.length === 0 || parts.length > 4) {
    return null;
  }

  const numbers = [];
  for (const part of parts) {
    const n = parseIPv4Part(part);
    if (n === null) {
      return null;
    }
    numbers.push(n);
  }

  for (let i = 0; i < numbers.length - 1; i++) {
    if (numbers[i] > 255) {
      return null;
    }
  }
  const last = numbers[numbers.length - 1];
  const maxLast = 256 ** (5 - numbers.length);
  if (last >= maxLast) {
    return null;
  }

  let ipv4 = last;
  for (let i = 0; i < numbers.length - 1; i++) {
    ipv4 += numbers[i] * 256 ** (3 - i);
  }
  return ipv4 >>> 0;
}

// True for 127.0.0.0/8 (any decimal/hex/octal/dotted encoding) or 0.0.0.0
// (any encoding).
function isLoopbackIPv4(hostname) {
  const ipv4 = parseIPv4ToInt(hostname);
  if (ipv4 === null) {
    return false;
  }
  const highByte = (ipv4 >>> 24) & 0xff;
  return highByte === 127 || ipv4 === 0;
}

// Expand a (bracket-stripped) IPv6 literal to its 8 16-bit groups, handling
// "::" compression and an optional embedded IPv4 dotted-quad in the final
// group (e.g. "::ffff:127.0.0.1"). Returns null when the literal is not a
// valid IPv6 address.
function parseIPv6Groups(hostname) {
  let host = hostname;

  // An embedded IPv4 dotted-quad can only appear as the last group.
  const lastColon = host.lastIndexOf(':');
  const tail = lastColon === -1 ? host : host.slice(lastColon + 1);
  if (tail.includes('.')) {
    const v4 = parseIPv4ToInt(tail);
    if (v4 === null) {
      return null;
    }
    const highHex = ((v4 >>> 16) & 0xffff).toString(16);
    const lowHex = (v4 & 0xffff).toString(16);
    host = host.slice(0, lastColon + 1) + highHex + ':' + lowHex;
  }

  const doubleColonIndex = host.indexOf('::');
  let headPart;
  let tailPart;
  if (doubleColonIndex !== -1) {
    if (host.indexOf('::', doubleColonIndex + 1) !== -1) {
      return null; // more than one "::" is invalid
    }
    headPart = host.slice(0, doubleColonIndex);
    tailPart = host.slice(doubleColonIndex + 2);
  } else {
    headPart = host;
    tailPart = '';
  }

  const headGroups = headPart === '' ? [] : headPart.split(':');
  const tailGroups = tailPart === '' ? [] : tailPart.split(':');

  if (doubleColonIndex === -1 && headGroups.length !== 8) {
    return null;
  }
  if (doubleColonIndex !== -1 && headGroups.length + tailGroups.length >= 8) {
    return null;
  }

  const missing = 8 - headGroups.length - tailGroups.length;
  const fillZeros = doubleColonIndex !== -1 ? new Array(missing).fill('0') : [];
  const allGroups = [...headGroups, ...fillZeros, ...tailGroups];

  if (allGroups.length !== 8) {
    return null;
  }

  const numbers = [];
  for (const g of allGroups) {
    if (!/^[0-9a-f]{1,4}$/i.test(g)) {
      return null;
    }
    numbers.push(parseInt(g, 16));
  }
  return numbers;
}

// True for ::1 (any valid expansion) and for the IPv4-mapped range
// ::ffff:0:0/96 whose embedded IPv4 falls in 127.0.0.0/8.
function isLoopbackIPv6(hostname) {
  const groups = parseIPv6Groups(hostname);
  if (!groups) {
    return false;
  }

  if (groups.slice(0, 7).every((g) => g === 0) && groups[7] === 1) {
    return true; // ::1
  }

  if (groups.slice(0, 5).every((g) => g === 0) && groups[5] === 0xffff) {
    const highByte = (groups[6] >> 8) & 0xff;
    return highByte === 127;
  }

  return false;
}

class ScanService {
  constructor() {
    this.pendingScans = new Map();
    this.recentlyScanned = new Map(); // Track recently scanned URLs to avoid duplicates
    this.recentScanTTL = 60000; // 60 seconds - don't rescan same URL within this time
    this.scanTimeout = 30000; // 30 seconds - increased for slower analysis
  }

  // Check if URL points to a local/loopback address — never send to backend.
  // See the ASPS-759 helpers above the class for the canonicalization
  // approach and the exact boundary drawn (what counts as "local" and what
  // deliberately does not).
  isLocalUrl(url) {
    try {
      // URL.hostname returns IPv6 literals in bracketed form (e.g. "[::1]"),
      // so strip the brackets before comparing against the loopback address.
      const hostname = new URL(url).hostname.toLowerCase().replace(/^\[|\]$/g, '');

      if (hostname === 'localhost' || hostname === 'localhost.') {
        return true;
      }

      if (hostname.includes(':')) {
        return isLoopbackIPv6(hostname);
      }

      return isLoopbackIPv4(hostname);
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
    // originatingPending is resolved here so it's available to the whole
    // method regardless of which code path we follow (envelope vs legacy).
    let originatingPending = null;

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

      // Record the originating pending entry before we may overwrite `data`.
      originatingPending = pending;

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

    // Resolve the originating pending entry for the legacy (non-envelope) path.
    // The desktop agent now includes `tabId` and `requestId` in legacy url_result
    // messages; fall back to URL-keyed lookup when requestId is absent.
    if (!originatingPending) {
      const pendingKey = data.requestId
        ? data.requestId
        : [...this.pendingScans.keys()].find(k => this.pendingScans.get(k).url === data.url);
      originatingPending = pendingKey ? this.pendingScans.get(pendingKey) : null;
    }

    // Save to storage for popup - per-tab and global.
    // Use the originating tab's ID so that a tab switch during analysis does
    // not misroute the result to whichever tab happens to be active on arrival.
    // The tabId may come from the pending entry (envelope path) or from the
    // desktop-agent-injected `tabId` field on the legacy message.
    const rawTabId = (originatingPending && originatingPending.tabId)
      || (data.tabId ? String(data.tabId) : null);
    const originatingTabId = rawTabId ? parseInt(rawTabId, 10) : null;

    if (originatingTabId && !Number.isNaN(originatingTabId)) {
      chrome.storage.local.set({
        [`tab_${originatingTabId}_score`]: score,
        [`tab_${originatingTabId}_riskType`]: riskType,
        [`tab_${originatingTabId}_action`]: protectiveAction,
        [`tab_${originatingTabId}_scanning`]: false,
        // Also update global so any UI reading currentPage* stays consistent
        currentPageScore: score,
        currentPageRiskType: riskType,
        currentPageAction: protectiveAction,
        currentPageScanning: false
      });
    } else {
      // No originating tab known — fall back to global-only write
      chrome.storage.local.set({
        currentPageScore: score,
        currentPageRiskType: riskType,
        currentPageAction: protectiveAction,
        currentPageScanning: false
      });
    }

    // Cache the result if not already cached
    if (!fromCache && data.url) {
      cacheService.set(data.url, { score, riskType, protectiveAction, ttl });
    }

    // Resolve the Promise from scan() and remove from pendingScans map.
    const resolveKey = (originatingPending && [...this.pendingScans.entries()]
      .find(([, v]) => v === originatingPending)?.[0])
      || data.requestId
      || [...this.pendingScans.entries()].find(([, v]) => v.url === data.url)?.[0];
    if (resolveKey && this.pendingScans.has(resolveKey)) {
      const { resolve, timeoutId } = this.pendingScans.get(resolveKey);
      clearTimeout(timeoutId);
      this.pendingScans.delete(resolveKey);
      resolve({ score, riskType, protectiveAction, fromCache, tabId: originatingTabId });
    }

    return { score, riskType, protectiveAction, fromCache, tabId: originatingTabId };
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

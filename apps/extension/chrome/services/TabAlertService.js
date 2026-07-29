// ============================================
// AntiScam Extension - TabAlertService
// Manages:
//   - Per-tab control state (url, root, isSensitiveWebsite, isLoggedIn)
//   - In-memory sensitive-domain cache (root → bool)
//   - TabChangedAlert and TabClosedAlert emission
//   - Cookie-driven loggedIn re-evaluation (debounced)
//
// Depends on:
//   - connectionService  (send)
//   - loggedInDetectorService (detect / getAuthCookiePattern)
//   - trackingService (getRootDomain / getRootDomainFromUrl)
//   - MSG constants
//
// Extracted from background.js (ASPS-627).
// ============================================

import { MSG } from '../messaging/MessageTypes.js';
import { loggedInDetectorService } from './LoggedInDetectorService.js';
import { trackingService } from './TrackingService.js';

// Debounce window for cookie-driven re-evaluation (ms)
const COOKIE_DEBOUNCE_MS = 400;

class TabAlertService {
  constructor() {
    // tabId → { url, root, isSensitiveWebsite: bool|null, isLoggedIn: bool|null }
    this.tabControls = new Map();

    // rootDomain → bool: cached from WS_URL_RESULT isSensitiveWebsite flag
    this.sensitiveDomainCache = new Map();

    // rootDomain → timeoutId: debounce state for cookie events
    this._cookieCoalesceTimers = new Map();
  }

  // ── Cache management ─────────────────────────────────────────────────

  /**
   * Replace the in-memory Maps from restored state.
   * @param {Map} tabControlsMap
   * @param {Map} sensitiveDomainCacheMap
   */
  restore(tabControlsMap, sensitiveDomainCacheMap) {
    this.tabControls        = tabControlsMap;
    this.sensitiveDomainCache = sensitiveDomainCacheMap;
  }

  /**
   * Return the sensitive-domain-cache Map (for persistence).
   * @returns {Map}
   */
  getSensitiveDomainCache() {
    return this.sensitiveDomainCache;
  }

  /**
   * Update the sensitive-domain cache from a URL scan result.
   * Back-fills all open tabs on the same root domain.
   * @param {string} url
   * @param {boolean|undefined} isSensitiveWebsite
   */
  updateSensitiveCache(url, isSensitiveWebsite) {
    if (typeof isSensitiveWebsite !== 'boolean') return;
    const root = trackingService.getRootDomainFromUrl(url);
    if (!root) return;
    this.sensitiveDomainCache.set(root, isSensitiveWebsite);
    // Back-fill open tabs on the same root
    for (const [, ctrl] of this.tabControls) {
      if (ctrl.root === root) {
        ctrl.isSensitiveWebsite = isSensitiveWebsite;
      }
    }
  }

  // ── Per-tab control state ────────────────────────────────────────────

  /**
   * Build or refresh the per-tab snapshot.  Checks cookies+DOM only when
   * the domain is already known-sensitive (avoids the cost for every tab).
   * @param {number} tabId
   * @param {string} url
   * @returns {Promise<{url, root, isSensitiveWebsite, isLoggedIn}>}
   */
  async refreshTabControl(tabId, url) {
    const root      = trackingService.getRootDomainFromUrl(url);
    const isSensitive = this.sensitiveDomainCache.get(root);
    let isLoggedIn  = null;
    if (isSensitive === true) {
      try {
        const result = await loggedInDetectorService.detect(tabId, url);
        isLoggedIn = result.loggedIn;
      } catch (_) { /* tab may have closed */ }
    }
    const snap = { url, root, isSensitiveWebsite: isSensitive ?? null, isLoggedIn };
    this.tabControls.set(tabId, snap);
    return snap;
  }

  /**
   * Remove a tab from control state (call on chrome.tabs.onRemoved).
   * @param {number} tabId
   * @returns {{url: string}|null} the removed entry, or null
   */
  removeTab(tabId) {
    const prev = this.tabControls.get(tabId);
    this.tabControls.delete(tabId);
    return prev || null;
  }

  /**
   * Return the stored snapshot for a tab.
   * @param {number} tabId
   * @returns {object|undefined}
   */
  getTabControl(tabId) {
    return this.tabControls.get(tabId);
  }

  // ── Alert emission ────────────────────────────────────────────────────

  /**
   * Send WS_TAB_CHANGED_ALERT if the connection is up.
   * @param {number} tabId
   * @param {object} snap
   * @param {object} connectionService
   */
  sendTabChangedAlert(tabId, snap, connectionService) {
    if (!connectionService?.isConnected?.()) return;
    try {
      connectionService.send({
        type:               MSG.WS_TAB_CHANGED_ALERT,
        tabId:              String(tabId),
        url:                snap.url || '',
        isSensitiveWebsite: snap.isSensitiveWebsite,
        isLoggedIn:         snap.isLoggedIn,
        timestamp:          new Date().toISOString(),
      });
      console.debug('[TabAlertService] TabChangedAlert sent: tab=%s sensitive=%s loggedIn=%s',
        tabId, snap.isSensitiveWebsite, snap.isLoggedIn);
    } catch (e) {
      console.warn('[TabAlertService] TabChangedAlert send failed:', e);
    }
  }

  /**
   * Send WS_TAB_CLOSED_ALERT if the connection is up.
   * @param {number} tabId
   * @param {string} url
   * @param {object} connectionService
   */
  sendTabClosedAlert(tabId, url, connectionService) {
    if (!connectionService?.isConnected?.()) return;
    try {
      connectionService.send({
        type:      MSG.WS_TAB_CLOSED_ALERT,
        tabId:     String(tabId),
        url:       url || '',
        timestamp: new Date().toISOString(),
      });
      console.debug('[TabAlertService] TabClosedAlert sent: tab=%s', tabId);
    } catch (e) {
      console.warn('[TabAlertService] TabClosedAlert send failed:', e);
    }
  }

  // ── Cookie-driven re-evaluation ───────────────────────────────────────

  /**
   * Handle a chrome.cookies.onChanged event.  Debounces re-evaluation per
   * root domain so bursts (e.g., logout) produce at most one scan.
   * @param {object} info
   * @param {boolean} isDeviceRemoteControlled
   * @param {object} connectionService
   */
  handleCookieChange(info, isDeviceRemoteControlled, connectionService) {
    try {
      const cookie = info?.cookie;
      if (!cookie) return;
      if (!loggedInDetectorService.getAuthCookiePattern().test(cookie.name)) return;
      const root = this._cookieDomainToRoot(cookie.domain);
      if (!root) return;
      if (this.sensitiveDomainCache.get(root) !== true) return;

      const existing = this._cookieCoalesceTimers.get(root);
      if (existing) clearTimeout(existing);
      const tid = setTimeout(() => {
        this._cookieCoalesceTimers.delete(root);
        this._reevaluateLoggedInForRoot(root, isDeviceRemoteControlled, connectionService)
          .catch(e => console.warn('[TabAlertService] cookie re-eval failed:', e));
      }, COOKIE_DEBOUNCE_MS);
      this._cookieCoalesceTimers.set(root, tid);
    } catch (e) {
      console.warn('[TabAlertService] cookies.onChanged handler error:', e);
    }
  }

  // ── Internal ──────────────────────────────────────────────────────────

  _cookieDomainToRoot(rawDomain) {
    if (!rawDomain) return '';
    let h = rawDomain.toLowerCase();
    if (h.startsWith('.')) h = h.slice(1);
    return trackingService.getRootDomain(h);
  }

  async _reevaluateLoggedInForRoot(root, isDeviceRemoteControlled, connectionService) {
    for (const [tabId, prev] of this.tabControls) {
      if (prev.root !== root) continue;
      let tab;
      try { tab = await chrome.tabs.get(tabId); } catch { continue; }
      if (!tab?.url) continue;

      const snap = await this.refreshTabControl(tabId, tab.url);
      const loggedInChanged = prev.isLoggedIn !== snap.isLoggedIn;
      if (!loggedInChanged) continue;
      if (!isDeviceRemoteControlled) continue;
      if (snap.isSensitiveWebsite !== true) continue;
      this.sendTabChangedAlert(tabId, snap, connectionService);
    }
  }
}

export const tabAlertService = new TabAlertService();
export default tabAlertService;

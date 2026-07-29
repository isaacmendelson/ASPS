// ============================================
// AntiScam Extension - Tracking Service
// Manages URL / domain tracking state:
//   - TrackMode per root domain (Surf / Click)
//   - enable / set / expire tracking entries
//   - navigation duration calculation
//
// Extracted from background.js (ASPS-627).
// ============================================

// TrackMode enum — matches backend Common.Enums.TrackMode
export const TrackMode = {
  None: 0,   // No tracking
  Surf: 1,   // Default: send TrackUrlAlert once per domain with silence interval
  Click: 2   // Send TrackUrlAlert on every click / navigation
};

// Known two-part TLDs for root-domain extraction
const KNOWN_TWO_PART_TLDS = [
  'co.uk', 'com.au', 'co.nz', 'co.jp', 'com.br', 'co.il', 'org.uk', 'net.au'
];

class TrackingService {
  constructor() {
    // Map of rootDomain → { expiresAt, trackMode, scamInProgressKey }
    this.trackedDomains = new Map();

    // Map of tabId → { url, timestamp }
    this.tabNavigationHistory = new Map();
  }

  // ── Domain helpers ────────────────────────────────────────────────────

  /**
   * Extract root domain from a hostname.
   * Examples: www.news.example.com → example.com, bank.co.uk → bank.co.uk
   * @param {string} hostname
   * @returns {string}
   */
  getRootDomain(hostname) {
    let h = String(hostname).toLowerCase();
    if (h.startsWith('www.')) h = h.slice(4);
    const parts = h.split('.');
    if (parts.length <= 2) return h;
    const lastTwo = `${parts[parts.length - 2]}.${parts[parts.length - 1]}`;
    if (KNOWN_TWO_PART_TLDS.includes(lastTwo)) {
      return parts.length >= 3 ? `${parts[parts.length - 3]}.${lastTwo}` : h;
    }
    return lastTwo;
  }

  /**
   * Extract root domain from a full URL.  Returns '' on parse failure.
   * @param {string} url
   * @returns {string}
   */
  getRootDomainFromUrl(url) {
    try {
      return this.getRootDomain(new URL(url).hostname);
    } catch (_) {
      return '';
    }
  }

  // ── Tracking state management ─────────────────────────────────────────

  /**
   * Enable URL tracking for a domain.
   * @param {string} domain        - Root domain to track (e.g. 'example.com')
   * @param {number} durationMinutes
   * @param {number} [trackMode]   - TrackMode enum value (default: Surf)
   * @param {string} [scamInProgressKey]
   */
  enableUrlTracking(domain, durationMinutes, trackMode = TrackMode.Surf, scamInProgressKey = '') {
    const expiresAt = Date.now() + durationMinutes * 60 * 1000;
    this.trackedDomains.set(domain, { expiresAt, trackMode, scamInProgressKey });
    const modeName = trackMode === TrackMode.Click ? 'Click' : 'Surf';
    console.debug(`[TrackingService] Tracking enabled: <domain> (${durationMinutes} min, ${modeName})`);
  }

  /**
   * Set track mode for a domain.  Delegates to enableUrlTracking.
   * @param {string} domain
   * @param {number} trackMode
   * @param {string} [scamInProgressKey]
   * @param {number} [durationMinutes]
   */
  setTrackMode(domain, trackMode, scamInProgressKey = '', durationMinutes = 30) {
    this.enableUrlTracking(domain, durationMinutes, trackMode, scamInProgressKey);
  }

  /**
   * Rebuild the in-memory trackedDomains Map from a backend SetTrackedDomains
   * payload array.  Each item should contain Domain, TrackMode, ScamInProgressKey.
   * Uses a long TTL so the map self-heals if the backend stops syncing.
   * @param {Array<{Domain?:string, domain?:string, TrackMode?:number, trackMode?:number, ScamInProgressKey?:string, scamInProgressKey?:string}>} domains
   */
  rebuildFromBackend(domains) {
    const CENTRAL_TRACK_TTL_MS = 24 * 60 * 60 * 1000; // 24 h
    this.trackedDomains.clear();
    for (const item of domains) {
      const rawDomain = item.Domain || item.domain;
      if (!rawDomain) continue;
      let rootDomain;
      try {
        rootDomain = this.getRootDomain(
          rawDomain.replace(/^https?:\/\//, '').split('/')[0]
        );
      } catch (_) {
        rootDomain = String(rawDomain).toLowerCase();
      }
      this.trackedDomains.set(rootDomain, {
        expiresAt: Date.now() + CENTRAL_TRACK_TTL_MS,
        trackMode: typeof item.TrackMode === 'number'
          ? item.TrackMode
          : (item.trackMode ?? TrackMode.Surf),
        scamInProgressKey: item.ScamInProgressKey || item.scamInProgressKey || '',
      });
    }
    console.debug(`[TrackingService] Rebuilt from backend: ${this.trackedDomains.size} domain(s)`);
  }

  /**
   * Replace the in-memory Map wholesale (used during state restore).
   * @param {Map} map
   */
  setMap(map) {
    this.trackedDomains = map;
  }

  /**
   * Return the underlying Map (for persistence).
   * @returns {Map}
   */
  getMap() {
    return this.trackedDomains;
  }

  // ── Query ─────────────────────────────────────────────────────────────

  /**
   * Return tracking info for a URL.
   * Expires and removes entries whose TTL has passed.
   * @param {string} url
   * @returns {{shouldTrack: boolean, trackMode: number, scamInProgressKey: string}}
   */
  getTrackingInfo(url) {
    const defaultResult = { shouldTrack: false, trackMode: TrackMode.None, scamInProgressKey: '' };
    try {
      const rootDomain = this.getRootDomainFromUrl(url);
      if (!rootDomain) return defaultResult;
      const trackInfo = this.trackedDomains.get(rootDomain);
      if (!trackInfo) return defaultResult;
      if (Date.now() > trackInfo.expiresAt) {
        this.trackedDomains.delete(rootDomain);
        console.debug('[TrackingService] Tracking expired for <root domain>');
        return defaultResult;
      }
      return {
        shouldTrack: true,
        trackMode: trackInfo.trackMode || TrackMode.Surf,
        scamInProgressKey: trackInfo.scamInProgressKey || ''
      };
    } catch (_) {
      return defaultResult;
    }
  }

  // ── Navigation tracking ───────────────────────────────────────────────

  /**
   * Record a tab navigation and return a TrackUrlAlert payload if one should
   * be sent, or null otherwise.  The caller is responsible for sending.
   *
   * @param {number} tabId
   * @param {string} url
   * @param {Function} getDeviceIpAddress  - () => string (from ConnectionService)
   * @returns {{alert: Object}|null}
   */
  processNavigation(tabId, url, getDeviceIpAddress) {
    if (!url || !url.startsWith('http')) return null;

    const now = Date.now();
    const currentTrackInfo = this.getTrackingInfo(url);
    const previousNav = this.tabNavigationHistory.get(tabId);

    let shouldSendAlert = false;
    let scamKey = '';

    if (previousNav) {
      const duration = Math.floor((now - previousNav.timestamp) / 1000);
      const prevTrackInfo = this.getTrackingInfo(previousNav.url);

      if (currentTrackInfo.shouldTrack) {
        if (currentTrackInfo.trackMode === TrackMode.Click) {
          shouldSendAlert = true;
          scamKey = currentTrackInfo.scamInProgressKey;
        } else {
          shouldSendAlert = previousNav.url !== url;
          scamKey = currentTrackInfo.scamInProgressKey;
        }
      } else if (prevTrackInfo.shouldTrack && prevTrackInfo.trackMode === TrackMode.Click) {
        shouldSendAlert = true;
        scamKey = prevTrackInfo.scamInProgressKey;
      }

      this.tabNavigationHistory.set(tabId, { url, timestamp: now });

      if (shouldSendAlert) {
        return {
          alert: this._buildTrackUrlAlert(tabId, url, previousNav.url, duration, scamKey, getDeviceIpAddress)
        };
      }
      return null;
    }

    // First navigation for this tab
    this.tabNavigationHistory.set(tabId, { url, timestamp: now });
    if (currentTrackInfo.shouldTrack) {
      return {
        alert: this._buildTrackUrlAlert(tabId, url, '', 0, currentTrackInfo.scamInProgressKey, getDeviceIpAddress)
      };
    }
    return null;
  }

  /**
   * Remove a tab from navigation history (on tab close).
   * @param {number} tabId
   */
  removeTab(tabId) {
    this.tabNavigationHistory.delete(tabId);
  }

  // ── Internal ──────────────────────────────────────────────────────────

  /**
   * Build a WS_TRACK_URL_ALERT message payload.
   * URLs are NOT logged — caller logs at debug level if needed.
   */
  _buildTrackUrlAlert(tabId, currentUrl, fromUrl, duration, scamInProgressKey, getDeviceIpAddress) {
    const ipAddress = typeof getDeviceIpAddress === 'function' ? getDeviceIpAddress() : '';
    const timezoneOffset = new Date().getTimezoneOffset() / -60;
    const timezone = `UTC${timezoneOffset >= 0 ? '+' : ''}${timezoneOffset}`;
    return {
      type: 'track_url_alert',
      Url: currentUrl,
      FromUrl: fromUrl,
      Duration: duration,
      ScamInProgressKey: scamInProgressKey,
      IPAddress: ipAddress,
      UserAgent: navigator.userAgent,
      TabId: String(tabId),
      Timezone: timezone
    };
  }
}

export const trackingService = new TrackingService();
export default trackingService;

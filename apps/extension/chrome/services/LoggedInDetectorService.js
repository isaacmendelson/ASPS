// ============================================
// AntiScam Extension - LoggedInDetectorService
// Per-tab logged-in signal aggregation using:
//   1. Auth-shaped cookies (cookies permission)
//   2. DOM scan via content script
//
// Extracted from background.js (ASPS-627).
// ============================================

import { MSG } from '../messaging/MessageTypes.js';

// Strict auth-cookie name patterns.
// Only matches names that are conventional for *authenticated* sessions;
// deliberately excludes csrf, _ga, AWSALB, load-balancer affinity cookies etc.
// that are routinely set before login.
const AUTH_COOKIE_NAME_PATTERN = new RegExp(
  '^(' + [
    // Framework session cookies (set on login)
    'phpsessid', 'jsessionid', 'connect\\.sid', 'asp\\.net.?sessionid',
    '_session_id', 'rack\\.session', 'laravel_session', 'symfony',
    'ci_session', 'codeigniter_session',
    // Explicit auth/token cookies
    '(remember|auth|access|refresh|bearer|id)[_-]?token',
    '(remember|auth|user)[_-]?session',
    'jwt',
    'authorization',
    'sessionid',
    // Modern secure-prefix cookies
    '__secure-.+',
    '__host-.+',
  ].join('|') + ')$',
  'i'
);

// Minimum cookie value length to avoid boolean flags like "session=1"
const MIN_SESSION_VALUE_LENGTH = 16;

// Timeout for DOM-scan response from content script (ms)
const DOM_SCAN_TIMEOUT_MS = 1500;

class LoggedInDetectorService {

  /**
   * Cookie-based logged-in check.
   * @param {string} url
   * @returns {Promise<{loggedIn: boolean|null, hasCookies: boolean}>}
   */
  async checkByCookies(url) {
    if (!url || !/^https?:/i.test(url)) {
      return { loggedIn: null, hasCookies: false };
    }
    try {
      const cookies = await chrome.cookies.getAll({ url });
      if (!cookies || cookies.length === 0) {
        return { loggedIn: false, hasCookies: false };
      }
      const sessionLike = cookies.some(
        c => AUTH_COOKIE_NAME_PATTERN.test(c.name) && (c.value || '').length >= MIN_SESSION_VALUE_LENGTH
      );
      return { loggedIn: sessionLike, hasCookies: true };
    } catch (e) {
      console.warn('[LoggedInDetector] Cookie check failed:', e?.message || e);
      return { loggedIn: null, hasCookies: false };
    }
  }

  /**
   * DOM-scan logged-in check via content script.
   * @param {number} tabId
   * @returns {Promise<boolean|null>}  true | false | null (null = unknown/no script)
   */
  async checkByDom(tabId) {
    if (typeof tabId !== 'number') return null;
    try {
      const resp = await Promise.race([
        chrome.tabs.sendMessage(tabId, { type: MSG.CHECK_LOGGED_IN_REQUEST }),
        new Promise(resolve => setTimeout(() => resolve(null), DOM_SCAN_TIMEOUT_MS)),
      ]);
      if (resp && typeof resp.loggedIn !== 'undefined') {
        return resp.loggedIn;
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  /**
   * Combined logged-in determination from cookie + DOM signals.
   * Errs toward "logged in" when signals conflict (false negative = missed alert).
   * @param {{loggedIn: boolean|null, hasCookies: boolean}} cookieResult
   * @param {boolean|null} domResult
   * @returns {{loggedIn: boolean|null, confidence: string|null, signals: string[]}}
   */
  combineSignals(cookieResult, domResult) {
    const cookieSays = cookieResult?.loggedIn;
    const domSays    = domResult;
    const signals = [];
    if (cookieSays !== null) signals.push('cookie');
    if (domSays    !== null) signals.push('dom');

    if (cookieSays === null && domSays === null) {
      return { loggedIn: null, confidence: null, signals: [] };
    }
    if (cookieSays === true  && domSays === true)  return { loggedIn: true,  confidence: 'high',   signals };
    if (cookieSays === false && domSays === false) return { loggedIn: false, confidence: 'medium', signals };
    if (cookieSays === true  || domSays === true)  return { loggedIn: true,  confidence: 'medium', signals };
    return { loggedIn: false, confidence: 'low', signals };
  }

  /**
   * Run cookie + DOM checks in parallel and combine results.
   * @param {number} tabId
   * @param {string} url
   * @returns {Promise<{loggedIn: boolean|null, confidence: string|null, signals: string[]}>}
   */
  async detect(tabId, url) {
    try {
      if (!url || !/^https?:/i.test(url)) return { loggedIn: null, confidence: null, signals: [] };
      const [cookieResult, domResult] = await Promise.all([
        this.checkByCookies(url),
        this.checkByDom(tabId),
      ]);
      return this.combineSignals(cookieResult, domResult);
    } catch (_) {
      return { loggedIn: null, confidence: null, signals: [] };
    }
  }

  /**
   * Expose the auth-cookie pattern so callers can filter cookie events
   * without duplicating the regex.
   * @returns {RegExp}
   */
  getAuthCookiePattern() {
    return AUTH_COOKIE_NAME_PATTERN;
  }
}

export const loggedInDetectorService = new LoggedInDetectorService();
export default loggedInDetectorService;

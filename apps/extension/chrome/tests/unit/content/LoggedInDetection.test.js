import { describe, test, expect, beforeEach } from '@jest/globals';

// ── Extracted from content.js for unit testing ──────────────────────────────
// LoggedInDetector is defined inside the content.js IIFE and is not exported.
// The implementation below is a verbatim copy from content.js with one
// adaptation: _isVisible omits the offsetParent check because jsdom has no
// layout engine (offsetParent is always null in jsdom). The visibility check
// is exercised via aria-hidden / hidden / computed-style instead, which covers
// the real filtering scenarios.
// (FR-041)

const LoggedInDetector = {
  _textPattern: new RegExp(
    [
      // English
      '\\blog\\s*out\\b', '\\blogout\\b', '\\blog\\s*off\\b', '\\blogoff\\b',
      '\\bsign\\s*-?\\s*out\\b', '\\bsignout\\b',
      // Hebrew
      'התנתק', 'התנתקות', 'יציאה',
      // French
      'd[ée]connexion', 'se\\s+d[ée]connecter', 'fermer\\s+la\\s+session', 'quitter',
      // Russian
      'выйти', 'выход', 'разлогин',
    ].join('|'),
    'i'
  ),

  _hrefPattern: /\/(log\s*out|logout|log\s*off|logoff|sign\s*-?\s*out|signout|deconnexion|deconnect|exit_session|leave_session|user\/?logout)\b/i,

  // Simplified for jsdom: offsetParent check removed (no layout engine).
  // aria-hidden, hidden attribute, and computed style are still tested.
  _isVisible(el) {
    if (!el) return false;
    if (el.hidden) return false;
    if (el.getAttribute('aria-hidden') === 'true') return false;
    const cs = window.getComputedStyle(el);
    if (cs.visibility === 'hidden' || cs.display === 'none' || cs.opacity === '0') {
      return false;
    }
    return true;
  },

  isLoggedIn() {
    try {
      if (document.readyState === 'loading') return null;

      const candidates = document.querySelectorAll(
        'a, button, [role="button"], [role="menuitem"], input[type="submit"]'
      );

      for (const el of candidates) {
        const text  = (el.textContent || '').trim();
        const aria  = (el.getAttribute('aria-label') || '').trim();
        const title = (el.getAttribute('title')      || '').trim();
        const value = (el.value                      || '').toString().trim();

        // Cap textContent length to avoid matching inside mega-navbars
        const probeText = (text.length > 200 ? '' : text) + ' ' + aria + ' ' + title + ' ' + value;
        const href       = el.getAttribute('href')        || '';
        const onclick    = el.getAttribute('onclick')      || '';
        const dataAction = el.getAttribute('data-action')  || el.getAttribute('data-test') || '';

        const matchedText = this._textPattern.test(probeText);
        const matchedHref = this._hrefPattern.test(href) ||
                            this._hrefPattern.test(onclick) ||
                            this._hrefPattern.test(dataAction);

        if (!matchedText && !matchedHref) continue;
        if (this._isVisible(el)) return true;
      }

      return false;
    } catch (e) {
      return null;
    }
  }
};

// ── combineLoggedInSignals extracted from background.js ──────────────────────
// Tested here because it is the confidence pipeline that turns the DOM signal
// (from LoggedInDetector) and the cookie signal (from checkLoggedInByCookies)
// into a single { loggedIn, confidence, signals } verdict.

function combineLoggedInSignals(cookieResult, domResult) {
  const cookieSays = cookieResult?.loggedIn;  // true | false | null
  const domSays    = domResult;               // true | false | null
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

// ─────────────────────────────────────────────────────────────────────────────

// Helper: append a visible button (or anchor) with the given text to document.body.
function addButton(text, { tag = 'button', href, ariaLabel, hidden = false } = {}) {
  const el = document.createElement(tag);
  el.textContent = text;
  if (href)      el.setAttribute('href', href);
  if (ariaLabel) el.setAttribute('aria-label', ariaLabel);
  if (hidden)    el.hidden = true;
  document.body.appendChild(el);
  return el;
}

// ─────────────────────────────────────────────────────────────────────────────

describe('LoggedInDetector', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    // Ensure readyState is 'complete' for all tests unless overridden
    Object.defineProperty(document, 'readyState', { value: 'complete', configurable: true });
  });

  // ── DOM text matching ──────────────────────────────────────────────────────

  describe('DOM text matching — logout keywords', () => {
    test('Hebrew "התנתק" → isLoggedIn() returns true', () => {
      // Most common Hebrew logout text on Israeli banking sites
      addButton('התנתק');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('Hebrew "התנתקות" → isLoggedIn() returns true', () => {
      // Noun form used by some Israeli portals
      addButton('התנתקות');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('English "Sign out" (with space) → isLoggedIn() returns true', () => {
      // Standard Google/Microsoft logout phrase
      addButton('Sign out');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('English "Logout" → isLoggedIn() returns true', () => {
      // Common single-word variant
      addButton('Logout');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('English "Log off" → isLoggedIn() returns true', () => {
      // Windows-style phrasing
      addButton('Log off');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('French "Déconnexion" → isLoggedIn() returns true', () => {
      // Standard French logout label on EU sites
      addButton('Déconnexion');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('Russian "выйти" → isLoggedIn() returns true', () => {
      // Common Russian verb for logout
      addButton('выйти');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('aria-label matching "Sign out" → isLoggedIn() returns true', () => {
      // Icon-only buttons rely on aria-label for the text
      const btn = document.createElement('button');
      btn.setAttribute('aria-label', 'Sign out');
      document.body.appendChild(btn);
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('no logout elements in DOM → isLoggedIn() returns false', () => {
      // Baseline: unrelated buttons must not produce a false positive
      addButton('Home');
      addButton('Contact Us');
      addButton('My Account');
      expect(LoggedInDetector.isLoggedIn()).toBe(false);
    });

    test('empty DOM → isLoggedIn() returns false', () => {
      // Page with no interactive elements: definitely not detected as logged in
      expect(LoggedInDetector.isLoggedIn()).toBe(false);
    });
  });

  // ── href-based matching ────────────────────────────────────────────────────

  describe('href-based matching', () => {
    test('link with href "/logout" → isLoggedIn() returns true', () => {
      // href pattern is the most reliable signal on many frameworks
      addButton('Exit', { tag: 'a', href: '/logout' });
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('link with href "/sign-out" → isLoggedIn() returns true', () => {
      addButton('Goodbye', { tag: 'a', href: '/sign-out' });
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('link with href "/user/logout" → isLoggedIn() returns true', () => {
      // Laravel / many PHP frameworks use this route
      addButton('Leave', { tag: 'a', href: '/user/logout' });
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('link with href "/exit_session" → isLoggedIn() returns true', () => {
      addButton('Close', { tag: 'a', href: '/exit_session' });
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });

    test('link with generic href "/home" (no logout keyword) → returns false', () => {
      // Ensure the href pattern does not over-match
      addButton('Home', { tag: 'a', href: '/home' });
      expect(LoggedInDetector.isLoggedIn()).toBe(false);
    });
  });

  // ── Visibility filtering ───────────────────────────────────────────────────

  describe('visibility filtering', () => {
    test('element with aria-hidden="true" containing "Logout" → returns false', () => {
      // A11y-hidden elements must not be counted (pre-login template trick)
      const btn = addButton('Logout');
      btn.setAttribute('aria-hidden', 'true');
      expect(LoggedInDetector.isLoggedIn()).toBe(false);
    });

    test('element with hidden attribute containing "Sign out" → returns false', () => {
      // The HTML hidden attribute hides the element from both display and AT
      addButton('Sign out', { hidden: true });
      expect(LoggedInDetector.isLoggedIn()).toBe(false);
    });

    test('visible logout element alongside a hidden one → returns true', () => {
      // Only the visible element must trigger the detector
      addButton('Logout', { hidden: true });
      addButton('Sign out');
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });
  });

  // ── Page loading state ─────────────────────────────────────────────────────

  describe('page loading state', () => {
    test('returns null when document.readyState is "loading"', () => {
      // DOM is not fully parsed yet — result would be unreliable
      Object.defineProperty(document, 'readyState', { value: 'loading', configurable: true });
      expect(LoggedInDetector.isLoggedIn()).toBeNull();
    });

    test('returns a boolean (not null) when readyState is "complete"', () => {
      // Once loading is done the detector must give a definitive answer
      Object.defineProperty(document, 'readyState', { value: 'complete', configurable: true });
      const result = LoggedInDetector.isLoggedIn();
      expect(result === true || result === false).toBe(true);
    });
  });

  // ── textContent length cap ─────────────────────────────────────────────────

  describe('textContent length cap', () => {
    test('element with textContent > 200 chars is skipped for text matching', () => {
      // Prevents mega-navbars that happen to contain a logout link from
      // polluting the probe text with thousands of unrelated characters
      const btn = document.createElement('button');
      btn.textContent = 'Logout ' + 'a'.repeat(300);
      document.body.appendChild(btn);
      // textContent.length > 200 → probeText starts with '' → text match fails.
      // The button also has no href/aria that matches → returns false.
      expect(LoggedInDetector.isLoggedIn()).toBe(false);
    });

    test('element with textContent <= 200 chars IS matched', () => {
      // Normal logout button length must still be detected
      const btn = document.createElement('button');
      btn.textContent = 'Sign out';
      document.body.appendChild(btn);
      expect(LoggedInDetector.isLoggedIn()).toBe(true);
    });
  });
});

// ─────────────────────────────────────────────────────────────────────────────

describe('combineLoggedInSignals', () => {
  // ── Both signals agree ────────────────────────────────────────────────────

  describe('agreement', () => {
    test('cookie=true + dom=true → loggedIn=true, confidence=high', () => {
      // Both detector paths confirm login: maximum confidence
      const r = combineLoggedInSignals({ loggedIn: true }, true);
      expect(r.loggedIn).toBe(true);
      expect(r.confidence).toBe('high');
    });

    test('cookie=false + dom=false → loggedIn=false, confidence=medium', () => {
      // Both say not logged in: medium (not high) because absence of evidence
      const r = combineLoggedInSignals({ loggedIn: false }, false);
      expect(r.loggedIn).toBe(false);
      expect(r.confidence).toBe('medium');
    });
  });

  // ── One signal, one unknown ────────────────────────────────────────────────

  describe('partial information', () => {
    test('cookie session_id present (loggedIn=true) + dom=null → loggedIn=true, confidence=medium', () => {
      // A long session_id cookie is a strong auth signal even without DOM
      const r = combineLoggedInSignals({ loggedIn: true }, null);
      expect(r.loggedIn).toBe(true);
      expect(r.confidence).toBe('medium');
    });

    test('cookie auth_token present (loggedIn=true) + dom=null → loggedIn=true, confidence=medium', () => {
      // auth_token matches AUTH_COOKIE_NAME_PATTERN in background.js
      const r = combineLoggedInSignals({ loggedIn: true }, null);
      expect(r.loggedIn).toBe(true);
      expect(r.confidence).toBe('medium');
    });

    test('cookie=null + dom=true → loggedIn=true, confidence=medium', () => {
      // DOM found a logout button; cookie check was unavailable
      const r = combineLoggedInSignals({ loggedIn: null }, true);
      expect(r.loggedIn).toBe(true);
      expect(r.confidence).toBe('medium');
    });

    test('cookie=false + dom=null → loggedIn=false, confidence=low', () => {
      // No auth cookies, DOM not available: weak negative signal
      const r = combineLoggedInSignals({ loggedIn: false }, null);
      expect(r.loggedIn).toBe(false);
      expect(r.confidence).toBe('low');
    });

    test('cookie=null + dom=false → loggedIn=false, confidence=low', () => {
      // DOM says not logged in but cookies unavailable: weak negative
      const r = combineLoggedInSignals({ loggedIn: null }, false);
      expect(r.loggedIn).toBe(false);
      expect(r.confidence).toBe('low');
    });
  });

  // ── Both unknown ──────────────────────────────────────────────────────────

  describe('both unknown', () => {
    test('no cookies + no logout elements → loggedIn=null, confidence=null', () => {
      // Cannot determine state — backend should not trigger an alert
      const r = combineLoggedInSignals({ loggedIn: null }, null);
      expect(r.loggedIn).toBeNull();
      expect(r.confidence).toBeNull();
      expect(r.signals).toEqual([]);
    });
  });

  // ── signals array ─────────────────────────────────────────────────────────

  describe('signals array', () => {
    test('signals contains "cookie" when cookie result is non-null', () => {
      // Backend can inspect which detectors contributed to the verdict
      const r = combineLoggedInSignals({ loggedIn: true }, null);
      expect(r.signals).toContain('cookie');
      expect(r.signals).not.toContain('dom');
    });

    test('signals contains "dom" when dom result is non-null', () => {
      const r = combineLoggedInSignals({ loggedIn: null }, false);
      expect(r.signals).toContain('dom');
      expect(r.signals).not.toContain('cookie');
    });

    test('signals contains both "cookie" and "dom" when both are non-null', () => {
      const r = combineLoggedInSignals({ loggedIn: true }, true);
      expect(r.signals).toContain('cookie');
      expect(r.signals).toContain('dom');
    });
  });
});

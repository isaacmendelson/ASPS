// ============================================
// AntiScam Extension - Content Script
// Refactored with better organization
// ============================================

(function() {
  'use strict';

  // ============================================
  // Configuration & Constants
  // ============================================

  const RISK_LABELS = {
    0: 'Safe',
    1: 'Phishing',
    2: 'Cloaking',
    3: 'Impersonation',
    4: 'Fake Domain',
    5: 'Unknown Risk'
  };

  const MSG = {
    PAGE_INFO_REQUEST: 'page:info:request',
    SHOW_WARNING: 'showWarning',
    BLOCK_PAGE: 'blockPage',
    REMOVE_WARNING: 'removeWarning'
  };

  // Remote access warning message types
  const REMOTE_MSG = {
    REMOTE_ACCESS_WARNING_SHOW: 'warning:remote_access',
    REMOTE_ACCESS_WARNING_DISMISS: 'warning:remote_dismiss',
    REMOTE_ACCESS_CLOSE_SESSION: 'remote:close_session',
    REMOTE_ACCESS_CONTINUED: 'remote:continued_anyway'
  };

  // ============================================
  // Tracker Detection Service
  // ============================================

  const TrackerService = {
    // Find Facebook Pixels
    findFacebookPixels() {
      const pixels = [];

      // Check scripts
      document.querySelectorAll('script').forEach(script => {
        const content = script.textContent || '';

        // Match fbq('init', 'PIXEL_ID')
        const matches = content.matchAll(/fbq\s*\(\s*['"]init['"]\s*,\s*['"](\d+)['"]/g);
        for (const match of matches) {
          pixels.push({
            Type: 'fbPixel',
            Value: match[1],
            Source: 'script'
          });
        }
      });

      // Check noscript tags
      document.querySelectorAll('noscript').forEach(ns => {
        const content = ns.textContent || '';
        const matches = content.matchAll(/facebook\.com\/tr\?id=(\d+)/g);
        for (const match of matches) {
          pixels.push({
            Type: 'fbPixel',
            Value: match[1],
            Source: 'noscript'
          });
        }
      });

      return this.deduplicate(pixels);
    },

    // Find Google Analytics
    findGoogleAnalytics() {
      const trackers = [];

      document.querySelectorAll('script').forEach(script => {
        const content = script.textContent || '';
        const src = script.src || '';

        // GA4 (G-XXXXXXXX)
        const ga4Matches = content.matchAll(/['"]G-([A-Z0-9]+)['"]/g);
        for (const match of ga4Matches) {
          trackers.push({
            Type: 'ga4',
            Value: 'G-' + match[1],
            Source: 'script'
          });
        }

        // Check src for gtag
        if (src.includes('gtag/js?id=G-')) {
          const idMatch = src.match(/id=(G-[A-Z0-9]+)/);
          if (idMatch) {
            trackers.push({
              Type: 'ga4',
              Value: idMatch[1],
              Source: 'src'
            });
          }
        }

        // Universal Analytics (UA-XXXXX-X)
        const uaMatches = content.matchAll(/['"]UA-(\d+-\d+)['"]/g);
        for (const match of uaMatches) {
          trackers.push({
            Type: 'ua',
            Value: 'UA-' + match[1],
            Source: 'script'
          });
        }
      });

      return this.deduplicate(trackers);
    },

    // Find external iFrames
    findExternalIframes() {
      const domains = [];
      const currentDomain = window.location.hostname;

      document.querySelectorAll('iframe').forEach(iframe => {
        const src = iframe.src;
        if (src) {
          try {
            const url = new URL(src);
            const domain = url.hostname;

            if (domain && domain !== currentDomain && !domains.includes(domain)) {
              domains.push(domain);
            }
          } catch (e) {}
        }
      });

      return domains;
    },

    // Get all page info
    getPageInfo() {
      const fbPixels = this.findFacebookPixels();
      const gaTrackers = this.findGoogleAnalytics();
      const iframes = this.findExternalIframes();

      return {
        trackers: [...fbPixels, ...gaTrackers],
        iframes: iframes,
        url: window.location.href,
        title: document.title,
        domain: window.location.hostname
      };
    },

    // Deduplicate trackers
    deduplicate(trackers) {
      const seen = new Set();
      return trackers.filter(tracker => {
        const key = `${tracker.Type}:${tracker.Value}`;
        if (seen.has(key)) return false;
        seen.add(key);
        return true;
      });
    }
  };

  // ============================================
  // Warning UI Service
  // ============================================

  const WarningService = {
    // Show warning banner at top of page
    showBanner(riskType, score) {
      this.removeAll();

      const riskLabels = riskType.map(r => RISK_LABELS[r] || 'Unknown').join(', ');

      const banner = document.createElement('div');
      banner.id = 'antiscam-warning-banner';
      banner.innerHTML = `
        <div class="antiscam-banner-content">
          <span class="antiscam-icon">⚠️</span>
          <span class="antiscam-text">
            <strong>Warning:</strong> This site may be dangerous.
            Risk: ${riskLabels} (Score: ${score}/100)
          </span>
          <button class="antiscam-close" onclick="this.parentElement.parentElement.remove()">✕</button>
        </div>
      `;

      document.body.insertBefore(banner, document.body.firstChild);
    },

    // Show modal warning popup
    showModal(riskType, score) {
      this.removeAll();

      const riskLabels = riskType.map(r => RISK_LABELS[r] || 'Unknown').join(', ');

      const overlay = document.createElement('div');
      overlay.id = 'antiscam-warning-modal';
      overlay.innerHTML = `
        <div class="antiscam-modal-content">
          <div class="antiscam-modal-header">
            <span class="antiscam-modal-icon">🛡️</span>
            <h2>Security Warning</h2>
          </div>
          <div class="antiscam-modal-body">
            <p><strong>This website may be dangerous!</strong></p>
            <p>Risk Type: <span class="antiscam-risk-type">${riskLabels}</span></p>
            <p>Risk Score: <span class="antiscam-score">${score}/100</span></p>
            <p>We recommend leaving this site immediately.</p>
          </div>
          <div class="antiscam-modal-footer">
            <button class="antiscam-btn-leave" onclick="window.history.back()">← Leave Site</button>
            <button class="antiscam-btn-continue" onclick="document.getElementById('antiscam-warning-modal').remove()">Continue Anyway</button>
          </div>
        </div>
      `;

      document.body.appendChild(overlay);
    },

    // Block page completely
    blockPage(riskType, score) {
      this.removeAll();

      const riskLabels = riskType.map(r => RISK_LABELS[r] || 'Unknown').join(', ');
      const domain = window.location.hostname;

      // Create blur wrapper
      const blurWrapper = document.createElement('div');
      blurWrapper.id = 'antiscam-blur-wrapper';
      blurWrapper.className = 'antiscam-blurred';

      // Move all body content into blur wrapper
      while (document.body.firstChild) {
        blurWrapper.appendChild(document.body.firstChild);
      }
      document.body.appendChild(blurWrapper);

      // Create block overlay
      const blocker = document.createElement('div');
      blocker.id = 'antiscam-block-page';
      blocker.innerHTML = `
        <div class="antiscam-block-content">
          <div class="antiscam-block-icon">🚫</div>
          <h1>אתר חסום</h1>
          <h2 class="antiscam-block-subtitle">Site Blocked for Your Protection</h2>
          <p class="antiscam-block-url">🌐 ${domain}</p>
          <div class="antiscam-block-details">
            <div class="antiscam-detail-item">
              <span class="antiscam-detail-label">סוג סיכון / Risk Type:</span>
              <span class="antiscam-detail-value">${riskLabels}</span>
            </div>
            <div class="antiscam-detail-item">
              <span class="antiscam-detail-label">ציון סיכון / Risk Score:</span>
              <span class="antiscam-detail-value">${score}/100</span>
            </div>
          </div>
          <p class="antiscam-block-warning">⚠️ אתר זה עלול להיות מסוכן ולגנוב את המידע שלך</p>
          <p class="antiscam-block-warning-en">This site may be dangerous and could steal your information</p>
          <div class="antiscam-block-buttons">
            <button class="antiscam-btn-back" onclick="window.history.back()">
              ← חזור למקום בטוח<br>
              <span style="font-size: 14px; font-weight: normal;">Go Back to Safety</span>
            </button>
            <button class="antiscam-btn-continue-anyway" onclick="document.getElementById('antiscam-block-page').remove(); document.getElementById('antiscam-blur-wrapper')?.classList.remove('antiscam-blurred'); document.body.style.overflow='';">
              המשך בכל זאת (לא מומלץ)<br>
              <span style="font-size: 12px; font-weight: normal;">Continue Anyway (Not Recommended)</span>
            </button>
          </div>
        </div>
      `;

      document.body.style.overflow = 'hidden';
      document.body.appendChild(blocker);
    },

    // Remove all warnings
    removeAll() {
      const elements = [
        'antiscam-warning-banner',
        'antiscam-warning-modal',
        'antiscam-block-page'
      ];

      elements.forEach(id => {
        const el = document.getElementById(id);
        if (el) el.remove();
      });

      // Restore blur wrapper content
      const blurWrapper = document.getElementById('antiscam-blur-wrapper');
      if (blurWrapper) {
        blurWrapper.classList.remove('antiscam-blurred');
        while (blurWrapper.firstChild) {
          document.body.appendChild(blurWrapper.firstChild);
        }
        blurWrapper.remove();
      }

      document.body.style.overflow = '';
    }
  };

  // ============================================
  // Remote Access Warning Service
  // ============================================

  const RemoteAccessWarningService = {
    _warningModule: null,

    /**
     * Shows the remote access warning overlay.
     * @param {string} toolName Name of detected remote access tool
     * @param {number} toolId ID of detected tool
     * @param {function} [onCloseSession] Callback for close session action
     * @param {function} [onContinue] Callback for continue anyway action
     */
    show(toolName, toolId, onCloseSession, onContinue) {
      // Dynamic import to avoid issues if module not loaded
      import('./warning/RemoteAccessWarning.js').then(({ remoteAccessWarning }) => {
        remoteAccessWarning.show({
          toolName: toolName || 'Remote access software',
          toolId: toolId,
          onCloseSession: () => {
            // Notify background to close session
            chrome.runtime.sendMessage({ type: REMOTE_MSG.REMOTE_ACCESS_CLOSE_SESSION });
            if (onCloseSession) onCloseSession();
          },
          onContinue: () => {
            // Notify background user continued anyway
            chrome.runtime.sendMessage({ type: REMOTE_MSG.REMOTE_ACCESS_CONTINUED });
            // Also dismiss on other tabs
            chrome.runtime.sendMessage({ type: REMOTE_MSG.REMOTE_ACCESS_WARNING_DISMISS });
            if (onContinue) onContinue();
          },
          onDismiss: () => {
            console.log('[Content] Remote access warning dismissed');
          }
        });
      }).catch(err => {
        console.error('[Content] Error loading RemoteAccessWarning:', err);
      });
    },

    /**
     * Hides the remote access warning overlay.
     */
    hide() {
      import('./warning/RemoteAccessWarning.js').then(({ remoteAccessWarning }) => {
        remoteAccessWarning.hide();
      }).catch(() => {
        // Ignore errors - module may not be loaded
      });
    }
  };

  // ============================================
  // Message Handler
  // ============================================

  const MessageHandler = {
    init() {
      chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
        this.handleMessage(message, sender, sendResponse);
        return true; // Async response support for dynamic imports
      });
    },

    handleMessage(message, sender, sendResponse) {
      const type = message.type;

      switch (type) {
        case MSG.PAGE_INFO_REQUEST:
        case 'getPageInfo':
          sendResponse(TrackerService.getPageInfo());
          break;

        case MSG.SHOW_WARNING:
        case 'showWarning':
          if (message.style === 'banner') {
            WarningService.showBanner(message.riskType, message.score);
          } else if (message.style === 'modal') {
            WarningService.showModal(message.riskType, message.score);
          }
          sendResponse({ success: true });
          break;

        case MSG.BLOCK_PAGE:
        case 'blockPage':
          WarningService.blockPage(message.riskType, message.score);
          sendResponse({ success: true });
          break;

        case MSG.REMOVE_WARNING:
        case 'removeWarning':
          WarningService.removeAll();
          sendResponse({ success: true });
          break;

        // Remote access warning messages
        case REMOTE_MSG.REMOTE_ACCESS_WARNING_SHOW:
        case 'warning:remote_access':
          RemoteAccessWarningService.show(
            message.toolName,
            message.toolId
          );
          sendResponse({ success: true });
          break;

        case REMOTE_MSG.REMOTE_ACCESS_WARNING_DISMISS:
        case 'warning:remote_dismiss':
          RemoteAccessWarningService.hide();
          sendResponse({ success: true });
          break;

        default:
          console.log('[Content] Unknown message type:', type);
          sendResponse({ error: 'Unknown message type' });
      }
    }
  };

  // ============================================
  // Initialize
  // ============================================

  function init() {
    MessageHandler.init();
    console.log('[AntiScam] Content script loaded');
  }

  // Start when DOM is ready
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

})();

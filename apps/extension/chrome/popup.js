// ============================================
// AntiScam Extension - Popup Script
// Refactored with better organization
// ============================================

(function() {
  'use strict';

  // ============================================
  // Configuration
  // ============================================

  const CONFIG = {
    SCAN_POLL_INTERVAL: 500,
    SCAN_MAX_ATTEMPTS: 60,  // 30 seconds at 500ms intervals
    RECONNECT_DELAY: 2000
  };

  // Connection status constants (match background.js)
  const ConnectionStatus = {
    CONNECTED: 'connected',
    DISCONNECTED: 'disconnected',
    RECONNECTING: 'reconnecting'
  };

  // ============================================
  // DOM Elements
  // ============================================

  const Elements = {
    // Sections
    mainSection: null,

    // Status
    statusIndicator: null,
    connectionStatus: null,
    desktopStatus: null,
    serverStatus: null,
    versionNumber: null,

    // Risk display
    riskCircle: null,
    riskScore: null,
    riskLabel: null,

    // Page info
    domainValue: null,
    sourceValue: null,

    // User
    userEmail: null,

    // Buttons
    btnScan: null,
    btnReconnect: null,

    // Warning
    warningBanner: null,
    warningText: null,

    // Feedback
    feedbackSection: null,
    btnCorrect: null,
    btnIncorrect: null,
    feedbackSent: null,

    // Initialize references
    init() {
      this.mainSection = document.getElementById('mainSection');
      this.statusIndicator = document.getElementById('statusIndicator');
      this.connectionStatus = document.getElementById('connectionStatus');
      this.desktopStatus = document.getElementById('desktopStatus');
      this.serverStatus = document.getElementById('serverStatus');
      this.versionNumber = document.getElementById('versionNumber');
      this.riskCircle = document.getElementById('riskCircle');
      this.riskScore = document.getElementById('riskScore');
      this.riskLabel = document.getElementById('riskLabel');
      this.domainValue = document.getElementById('domainValue');
      this.sourceValue = document.getElementById('sourceValue');
      this.userEmail = document.getElementById('userEmail');
      this.btnScan = document.getElementById('btnScan');
      this.btnReconnect = document.getElementById('btnReconnect');
      this.warningBanner = document.getElementById('warningBanner');
      this.warningText = document.getElementById('warningText');
      this.feedbackSection = document.getElementById('feedbackSection');
      this.btnCorrect = document.getElementById('btnCorrect');
      this.btnIncorrect = document.getElementById('btnIncorrect');
      this.feedbackSent = document.getElementById('feedbackSent');
    }
  };

  // ============================================
  // Auth Service
  // ============================================

  const AuthService = {
    async init() {
      // Always show main section - email comes from desktop agent via pong
      Elements.mainSection.style.display = 'block';

      // Display email if already stored (agent sends it on every ping/pong)
      const data = await chrome.storage.local.get(['userEmail']);
      if (data.userEmail) {
        Elements.userEmail.textContent = data.userEmail;
      } else {
        Elements.userEmail.textContent = 'Connecting...';
      }
    }
  };

  // ============================================
  // Status Service
  // ============================================

  const StatusService = {
    async update() {
      // Version (from manifest) - always update, even if status fails
      try {
        const manifest = chrome.runtime.getManifest();
        if (Elements.versionNumber) {
          Elements.versionNumber.textContent = manifest.version;
        }
      } catch (e) {
        console.warn('[Popup] Failed to get version:', e);
      }

      try {
        const response = await chrome.runtime.sendMessage({ type: 'getStatus' });
        console.log('[Popup] Status:', response);

        // Get reconnecting state from state manager (via storage sync)
        const data = await chrome.storage.local.get(['connection.reconnecting']);
        const isReconnecting = data['connection.reconnecting'] || false;

        // Connection status with reconnecting support
        if (response.isConnectedToDesktop) {
          this.setStatus(ConnectionStatus.CONNECTED);
        } else if (isReconnecting) {
          this.setStatus(ConnectionStatus.RECONNECTING);
        } else {
          this.setStatus(ConnectionStatus.DISCONNECTED);
        }

        // Risk display - use server values
        if (response.currentPageScore !== null && response.currentPageScore !== undefined) {
          RiskDisplay.update(
            response.currentPageScore,
            response.currentPageRiskType || [],
            response.currentPageAction || 0
          );
        }

        // Warning display - show if remote access active
        WarningDisplay.update({
          active: response.warningActive,
          toolName: response.warningToolName
        });

      } catch (e) {
        console.error('[Popup] Error getting status:', e);
        this.setError();
      }
    },

    setStatus(status) {
      // Update dot/indicator
      Elements.statusIndicator.className = 'status-indicator ' + this.getStatusClass(status);

      // Update status text
      const statusText = {
        [ConnectionStatus.CONNECTED]: 'Protected',
        [ConnectionStatus.DISCONNECTED]: 'Not Protected',
        [ConnectionStatus.RECONNECTING]: 'Reconnecting...'
      };
      Elements.connectionStatus.textContent = statusText[status] || 'Unknown';

      // Update desktop/server status
      if (status === ConnectionStatus.CONNECTED) {
        Elements.desktopStatus.textContent = 'Connected';
        Elements.desktopStatus.className = 'detail-value online';
        Elements.serverStatus.textContent = 'Via Desktop';
        Elements.serverStatus.className = 'detail-value online';
      } else if (status === ConnectionStatus.RECONNECTING) {
        Elements.desktopStatus.textContent = 'Reconnecting...';
        Elements.desktopStatus.className = 'detail-value offline';
        Elements.serverStatus.textContent = 'Reconnecting...';
        Elements.serverStatus.className = 'detail-value offline';
      } else {
        Elements.desktopStatus.textContent = 'Disconnected';
        Elements.desktopStatus.className = 'detail-value offline';
        Elements.serverStatus.textContent = 'Disconnected';
        Elements.serverStatus.className = 'detail-value offline';
      }

      // Show/hide reconnect button (user decision: only visible when disconnected)
      if (status === ConnectionStatus.DISCONNECTED) {
        Elements.btnReconnect.classList.remove('btn-reconnect-hidden');
      } else {
        Elements.btnReconnect.classList.add('btn-reconnect-hidden');
      }
    },

    getStatusClass(status) {
      const classes = {
        [ConnectionStatus.CONNECTED]: 'green',
        [ConnectionStatus.DISCONNECTED]: 'red',
        [ConnectionStatus.RECONNECTING]: 'yellow'
      };
      return classes[status] || 'gray';
    },

    // Legacy methods for backward compatibility
    setConnected() {
      this.setStatus(ConnectionStatus.CONNECTED);
    },

    setDisconnected() {
      this.setStatus(ConnectionStatus.DISCONNECTED);
    },

    setError() {
      Elements.statusIndicator.className = 'status-indicator gray';
      Elements.connectionStatus.textContent = 'Error';
    }
  };

  // ============================================
  // Page Info Service
  // ============================================

  const PageInfoService = {
    async update() {
      try {
        const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
        if (!tabs[0]) return;

        const tabId = tabs[0].id;
        const url = tabs[0].url;

        // Domain
        this.updateDomain(url);

        // Tab data - prioritize per-tab score over global
        const data = await chrome.storage.local.get([
          `tab_${tabId}`,
          `tab_${tabId}_score`,
          `tab_${tabId}_riskType`,
          `tab_${tabId}_action`,
          `tab_${tabId}_scanning`,
          'currentPageScore',
          'currentPageRiskType',
          'currentPageAction',
          'currentPageScanning'
        ]);
        const tabData = data[`tab_${tabId}`];

        if (tabData) {
          Elements.sourceValue.textContent = tabData.fromCache ? 'Cache' : 'Server';
        } else {
          Elements.sourceValue.textContent = '--';
        }

        // Risk display - prefer per-tab score, fallback to global
        const score = data[`tab_${tabId}_score`] ?? data.currentPageScore;
        const riskType = data[`tab_${tabId}_riskType`] ?? data.currentPageRiskType ?? [];
        const action = data[`tab_${tabId}_action`] ?? data.currentPageAction ?? 0;
        const isScanning = data[`tab_${tabId}_scanning`] ?? data.currentPageScanning ?? false;

        if (score !== undefined && score !== null) {
          RiskDisplay.update(score, riskType, action);
        } else if (isScanning) {
          // Show checking state
          RiskDisplay.showChecking();
        } else {
          // No score and not scanning - show waiting state
          Elements.riskScore.textContent = '--';
          Elements.riskCircle.className = 'risk-circle gray';
          Elements.riskLabel.className = 'risk-label gray';
          Elements.riskLabel.textContent = 'Not scanned';
        }

      } catch (e) {
        console.error('[Popup] Error getting tab data:', e);
      }
    },

    updateDomain(url) {
      try {
        const domain = new URL(url).hostname;
        const truncated = domain.length > 25 ? domain.substring(0, 25) + '...' : domain;
        Elements.domainValue.textContent = truncated;
        Elements.domainValue.title = domain;
      } catch {
        Elements.domainValue.textContent = '--';
      }
    }
  };

  // ============================================
  // Risk Display
  // Uses server values directly
  // ============================================

  const RiskDisplay = {
    lastUrl: null,

    showChecking() {
      Elements.riskCircle.className = 'risk-circle checking';
      Elements.riskScore.textContent = ''; // Empty interior per user decision
      Elements.riskLabel.textContent = 'Checking...';
      Elements.riskLabel.className = 'risk-label checking';
      FeedbackService.hide();
    },

    showTimeout() {
      // User decision: neutral gray on 30s timeout, not error message
      Elements.riskCircle.className = 'risk-circle timeout';
      Elements.riskScore.textContent = '--';
      Elements.riskLabel.textContent = 'Timeout';
      Elements.riskLabel.className = 'risk-label timeout';
      FeedbackService.hide();
    },

    async update(score, riskType = [], protectiveAction = 0) {
      console.log('[Popup] Updating risk display (from server):', score);

      // Show server score directly
      Elements.riskScore.textContent = score ?? '--';
      Elements.riskCircle.classList.remove('loading', 'checking', 'timeout');

      // Get color based on protective action from server (with score fallback)
      const { color, label } = this.getDisplayInfo(protectiveAction, riskType, score);

      Elements.riskCircle.className = `risk-circle ${color}`;
      Elements.riskLabel.className = `risk-label ${color}`;
      Elements.riskLabel.textContent = label;

      // Show feedback section if we have a valid score
      if (score !== null && score !== undefined) {
        try {
          const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
          if (tabs[0]?.url) {
            this.lastUrl = tabs[0].url;
            FeedbackService.show(tabs[0].url, score, riskType);
          }
        } catch (e) {
          console.error('[Popup] Error getting tab URL for feedback:', e);
        }
      }
    },

    reset() {
      this.showChecking();
    },

    getDisplayInfo(protectiveAction, riskType, score) {
      // Use protective action from server for color
      // 0=None, 1=Notify, 2=WarnBanner, 3=WarnModal, 4=Block
      if (protectiveAction >= 4) {
        return { color: 'red', label: riskType[0] || 'Blocked' };
      } else if (protectiveAction >= 2) {
        return { color: 'yellow', label: riskType[0] || 'Warning' };
      } else {
        // Fallback to score-based display if protectiveAction is 0 or 1
        // This handles cases where server doesn't set protectiveAction correctly
        // NEW SCALE: 0=error, 1=safe, 100=dangerous
        // IMPORTANT: Use score-based labels, not riskType from server (may be using old scale)
        if (score !== null && score !== undefined) {
          if (score >= 61) {
            return { color: 'red', label: 'HIGH' };
          } else if (score >= 31) {
            return { color: 'yellow', label: 'MEDIUM' };
          }
        }
        return { color: 'green', label: 'LOW' };
      }
    },

    // Legacy: for backward compatibility
    // NEW SCALE: 0=error, 1=safe, 100=dangerous
    getScoreInfo(score) {
      if (score >= 61) {
        return { color: 'red', label: 'High Risk' };
      } else if (score >= 31) {
        return { color: 'yellow', label: 'Medium Risk' };
      } else {
        return { color: 'green', label: 'Safe' };
      }
    }
  };

  // ============================================
  // Warning Display
  // Shows warning banner when remote access is detected
  // ============================================

  const WarningDisplay = {
    update(warningState) {
      if (!Elements.warningBanner) return;

      if (warningState && warningState.active) {
        Elements.warningBanner.classList.add('show');
        const toolName = warningState.toolName || 'Unknown tool';
        Elements.warningText.textContent = `Remote access detected: ${toolName}`;

        // Also update risk circle to show dangerous state
        Elements.riskCircle.className = 'risk-circle red';
        Elements.riskLabel.className = 'risk-label red';
        Elements.riskLabel.textContent = 'Dangerous';
      } else {
        Elements.warningBanner.classList.remove('show');
      }
    }
  };

  // ============================================
  // Feedback Service
  // Sends score feedback to Google Sheets
  // ============================================

  const FeedbackService = {
    // Google Sheets Web App URL - built-in default
    SHEETS_URL: 'https://script.google.com/macros/s/AKfycbyQmmjmsvgrSMUtUnb0oJJkF8uYhyDD6QGyk1MRTGNa2fix6B1zFRVghdP1BlS8pW8zKg/exec',

    currentUrl: null,
    currentScore: null,
    currentRiskType: null,

    async init() {
      // Load sheets URL from storage (can override built-in default)
      const data = await chrome.storage.local.get(['feedbackSheetsUrl']);
      if (data.feedbackSheetsUrl) {
        this.SHEETS_URL = data.feedbackSheetsUrl;
      }
    },

    show(url, score, riskType) {
      this.currentUrl = url;
      this.currentScore = score;
      this.currentRiskType = riskType;

      if (Elements.feedbackSection) {
        Elements.feedbackSection.style.display = 'block';
        Elements.feedbackSent.style.display = 'none';
        Elements.btnCorrect.disabled = false;
        Elements.btnIncorrect.disabled = false;
      }
    },

    hide() {
      if (Elements.feedbackSection) {
        Elements.feedbackSection.style.display = 'none';
      }
    },

    async sendFeedback(isCorrect) {
      console.log('[Popup] Sending feedback:', isCorrect ? 'correct' : 'incorrect');

      // Disable buttons
      Elements.btnCorrect.disabled = true;
      Elements.btnIncorrect.disabled = true;

      const feedbackData = {
        url: this.currentUrl,
        score: this.currentScore,
        riskType: Array.isArray(this.currentRiskType) ? this.currentRiskType.join(', ') : this.currentRiskType,
        isCorrect: isCorrect,
        timestamp: new Date().toISOString(),
        userAgent: navigator.userAgent
      };

      try {
        // Save to local storage for backup
        await this.saveLocally(feedbackData);

        // Send to Google Sheets if URL is configured
        if (this.SHEETS_URL) {
          await this.sendToSheets(feedbackData);
        }

        // Show success
        Elements.feedbackSent.style.display = 'block';
        console.log('[Popup] Feedback sent successfully');

      } catch (error) {
        console.error('[Popup] Error sending feedback:', error);
        // Still show success since we saved locally
        Elements.feedbackSent.style.display = 'block';
        Elements.feedbackSent.textContent = '✓ Saved locally';
      }
    },

    async saveLocally(data) {
      const stored = await chrome.storage.local.get(['feedbackHistory']);
      const history = stored.feedbackHistory || [];
      history.push(data);

      // Keep last 100 entries
      if (history.length > 100) {
        history.shift();
      }

      await chrome.storage.local.set({ feedbackHistory: history });
    },

    async sendToSheets(data) {
      if (!this.SHEETS_URL) return;

      const response = await fetch(this.SHEETS_URL, {
        method: 'POST',
        mode: 'no-cors', // Google Apps Script requires this
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data)
      });

      return response;
    }
  };

  // ============================================
  // Scan Service
  // ============================================

  const ScanService = {
    _storageListener: null,
    _timeoutId: null,

    async scan() {
      console.log('[Popup] Scan button clicked');

      this.setButtonScanning(true);
      RiskDisplay.reset();

      try {
        // Clear previous results first
        await chrome.storage.local.remove(['currentPageScore', 'currentPageRiskType', 'currentPageAction']);

        // Set up listener BEFORE sending scan request
        this.waitForResults();

        await chrome.runtime.sendMessage({ type: 'scanCurrentPage' });
      } catch (e) {
        console.error('[Popup] Scan error:', e);
        this.cleanup();
        this.handleScanError();
      }
    },

    waitForResults() {
      // Remove any existing listener
      this.cleanup();

      // Set up storage change listener
      this._storageListener = (changes, area) => {
        if (area !== 'local') return;

        if (changes.currentPageScore && changes.currentPageScore.newValue !== undefined) {
          console.log('[Popup] Got scan result via storage.onChanged');
          this.cleanup();

          const score = changes.currentPageScore.newValue;
          const riskType = changes.currentPageRiskType?.newValue || [];
          const action = changes.currentPageAction?.newValue || 0;

          RiskDisplay.update(score, riskType, action);
          PageInfoService.update();
          this.setButtonScanning(false);
        }
      };

      chrome.storage.onChanged.addListener(this._storageListener);

      // Set timeout (30 seconds)
      this._timeoutId = setTimeout(() => {
        console.log('[Popup] Scan timeout');
        this.cleanup();
        this.handleScanTimeout();
      }, CONFIG.SCAN_POLL_INTERVAL * CONFIG.SCAN_MAX_ATTEMPTS);
    },

    cleanup() {
      if (this._storageListener) {
        chrome.storage.onChanged.removeListener(this._storageListener);
        this._storageListener = null;
      }
      if (this._timeoutId) {
        clearTimeout(this._timeoutId);
        this._timeoutId = null;
      }
    },

    handleScanError() {
      this.setButtonScanning(false);
      Elements.riskCircle.classList.remove('loading');
      Elements.riskLabel.textContent = 'Error';
    },

    handleScanTimeout() {
      this.setButtonScanning(false);
      RiskDisplay.showTimeout();
    },

    setButtonScanning(scanning) {
      Elements.btnScan.disabled = scanning;
      Elements.btnScan.innerHTML = scanning
        ? '<span class="btn-icon">⏳</span> Scanning...'
        : '<span class="btn-icon">🔍</span> Scan Page';
    }
  };

  // ============================================
  // Reconnect Service
  // ============================================

  const ReconnectService = {
    async reconnect() {
      console.log('[Popup] Reconnect button clicked');

      this.setButtonConnecting(true);

      // Immediately show reconnecting status in UI
      StatusService.setStatus(ConnectionStatus.RECONNECTING);

      try {
        const result = await chrome.runtime.sendMessage({ type: 'reconnect' });

        setTimeout(async () => {
          await StatusService.update();
          this.setButtonConnecting(false);

          if (result.success) {
            ScanService.scan();
          }
        }, CONFIG.RECONNECT_DELAY);

      } catch (e) {
        console.error('[Popup] Reconnect error:', e);
        this.setButtonConnecting(false);
        await StatusService.update();
      }
    },

    setButtonConnecting(connecting) {
      Elements.btnReconnect.disabled = connecting;
      Elements.btnReconnect.innerHTML = connecting
        ? '<span class="btn-icon">⏳</span> Connecting...'
        : '<span class="btn-icon">🔄</span> Reconnect';
    }
  };

  // ============================================
  // Event Handlers
  // ============================================

  const EventHandlers = {
    init() {
      // Save email button
      // Scan button
      Elements.btnScan.addEventListener('click', () => ScanService.scan());

      // Reconnect button
      Elements.btnReconnect.addEventListener('click', () => ReconnectService.reconnect());

      // Feedback buttons
      if (Elements.btnCorrect) {
        Elements.btnCorrect.addEventListener('click', () => FeedbackService.sendFeedback(true));
      }
      if (Elements.btnIncorrect) {
        Elements.btnIncorrect.addEventListener('click', () => FeedbackService.sendFeedback(false));
      }

      // Storage changes - use server values
      chrome.storage.onChanged.addListener(async (changes, namespace) => {
        if (namespace !== 'local') return;

        // Handle email update from agent (via pong)
        if (changes.userEmail && changes.userEmail.newValue) {
          Elements.userEmail.textContent = changes.userEmail.newValue;
        }

        // Handle scanning state changes
        if (changes.currentPageScanning) {
          if (changes.currentPageScanning.newValue === true) {
            RiskDisplay.showChecking();
          }
        }

        // Handle currentPageScore changes
        if (changes.currentPageScore) {
          const newScore = changes.currentPageScore.newValue;
          if (newScore !== undefined && newScore !== null) {
            // Get all server values
            const data = await chrome.storage.local.get([
              'currentPageRiskType', 'currentPageAction'
            ]);
            RiskDisplay.update(
              newScore,
              data.currentPageRiskType || [],
              data.currentPageAction || 0
            );
          } else if (newScore === null) {
            // Score was cleared (new scan starting) - show checking state
            RiskDisplay.showChecking();
          }
        }

        // Handle appState changes (for warning state)
        if (changes.appState) {
          const newState = changes.appState.newValue;
          const oldState = changes.appState.oldValue;

          // Check if warning state changed
          const newWarning = newState?.warning;
          const oldWarning = oldState?.warning;

          if (newWarning?.active !== oldWarning?.active) {
            WarningDisplay.update({
              active: newWarning?.active || false,
              toolName: newWarning?.toolName || null
            });

            // If warning became inactive, restore normal risk display
            if (!newWarning?.active) {
              await StatusService.update();
            }
          }
        }
      });
    },

    handleSaveEmail() {
      // No-op: email now comes from desktop agent
    }
  };

  // ============================================
  // Initialize
  // ============================================

  // Trigger reconnect if disconnected when popup opens (user decision: feels responsive)
  async function triggerReconnectIfNeeded() {
    try {
      const response = await chrome.runtime.sendMessage({ type: 'getStatus' });
      if (!response.isConnectedToDesktop) {
        console.log('[Popup] Not connected - triggering immediate reconnect');
        await chrome.runtime.sendMessage({ type: 'reconnect' });
        // Update status after a short delay to show reconnecting state
        setTimeout(() => StatusService.update(), 1000);
      }
    } catch (e) {
      console.error('[Popup] Error triggering reconnect:', e);
    }
  }

  async function init() {
    console.log('[Popup] Initializing...');

    // Init DOM references
    Elements.init();

    // Set version immediately (before anything else can fail)
    try {
      const manifest = chrome.runtime.getManifest();
      const versionEl = document.getElementById('versionNumber');
      if (versionEl && manifest.version) {
        versionEl.textContent = manifest.version;
        console.log('[Popup] Version set to:', manifest.version);
      }
    } catch (e) {
      console.error('[Popup] Failed to set version:', e);
    }

    // Init services
    await FeedbackService.init();

    // Init event handlers
    EventHandlers.init();

    // Show main section (email comes from agent automatically)
    await AuthService.init();

    RiskDisplay.showChecking();
    await StatusService.update();
    await PageInfoService.update();

    // Opening popup triggers immediate reconnect attempt
    triggerReconnectIfNeeded();

    console.log('[Popup] Ready');
  }

  // Start when DOM is ready
  document.addEventListener('DOMContentLoaded', init);

})();

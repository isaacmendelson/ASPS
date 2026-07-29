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
      // NEW SCALE: 0=error, 1=safe, 100=dangerous
      // ALWAYS use score-based labels (server riskType may use old scale)
      
      // Determine label and color from score
      let color = 'green';
      let label = 'LOW';
      
      if (score !== null && score !== undefined) {
        if (score >= 61) {
          color = 'red';
          label = 'HIGH';
        } else if (score >= 31) {
          color = 'yellow';
          label = 'MEDIUM';
        }
      }
      
      // Override with protective action if severe (Block)
      if (protectiveAction >= 4) {
        return { color: 'red', label: 'BLOCKED' };
      }
      
      return { color, label };
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
  // Routes score feedback through the Backend via the background service
  // worker (ConnectionService / WebSocket).  No data is sent to third-party
  // endpoints and no PII is stored in chrome.storage without user consent.
  // ============================================

  const FeedbackService = {
    // Storage key used to record that the user has explicitly consented
    CONSENT_KEY: 'feedbackConsentGiven',

    currentUrl: null,
    currentScore: null,
    currentRiskType: null,

    async init() {
      // Nothing to load — no external URL, no override capability
    },

    // Returns true when the user has previously given consent
    async hasConsent() {
      const data = await chrome.storage.local.get([this.CONSENT_KEY]);
      return data[this.CONSENT_KEY] === true;
    },

    // Persist consent decision
    async setConsent(value) {
      await chrome.storage.local.set({ [this.CONSENT_KEY]: value });
    },

    // Show the feedback widget only when user has consented.
    // If consent has not been given, the widget remains hidden.
    async show(url, score, riskType) {
      this.currentUrl = url;
      this.currentScore = score;
      this.currentRiskType = riskType;

      if (!Elements.feedbackSection) return;

      const consented = await this.hasConsent();
      if (!consented) {
        // Offer consent inline rather than silently suppressing
        this._showConsentPrompt();
        return;
      }

      this._showFeedbackButtons();
    },

    _showConsentPrompt() {
      if (!Elements.feedbackSection) return;
      Elements.feedbackSection.style.display = 'block';
      Elements.feedbackSent.style.display = 'none';
      // Replace button row with a one-time consent message
      Elements.feedbackSection.innerHTML =
        '<div class="feedback-question">Help improve accuracy? ' +
        '<button id="btnConsentYes" class="btn-feedback btn-correct" style="font-size:0.8em;padding:2px 8px">Yes</button> ' +
        '<button id="btnConsentNo" class="btn-feedback btn-incorrect" style="font-size:0.8em;padding:2px 8px">No</button>' +
        '</div>' +
        '<div class="feedback-privacy-note" style="font-size:0.75em;color:#888;margin-top:4px">' +
        'Score and domain sent to ASPS backend only. ' +
        '<a href="#" id="btnDeleteFeedback" style="color:#888">Delete my data</a>' +
        '</div>';

      document.getElementById('btnConsentYes').addEventListener('click', async () => {
        await this.setConsent(true);
        this._showFeedbackButtons();
      });
      document.getElementById('btnConsentNo').addEventListener('click', () => {
        Elements.feedbackSection.style.display = 'none';
      });
      const delLink = document.getElementById('btnDeleteFeedback');
      if (delLink) delLink.addEventListener('click', (e) => { e.preventDefault(); this.deleteFeedbackData(); });
    },

    _showFeedbackButtons() {
      if (!Elements.feedbackSection) return;
      // Re-render the original button layout (may have been replaced by consent prompt)
      Elements.feedbackSection.innerHTML =
        '<div class="feedback-question">Is this score correct?</div>' +
        '<div class="feedback-buttons">' +
        '<button class="btn-feedback btn-correct" id="btnCorrect" title="Score is correct">&#128077;</button>' +
        '<button class="btn-feedback btn-incorrect" id="btnIncorrect" title="Score is wrong">&#128078;</button>' +
        '</div>' +
        '<div class="feedback-sent" id="feedbackSent" style="display:none">&#10003; Thanks for your feedback!</div>' +
        '<div style="font-size:0.75em;text-align:center;margin-top:4px">' +
        '<a href="#" id="btnDeleteFeedback" style="color:#888">Delete my data</a>' +
        '</div>';

      // Re-bind element references that were re-created
      Elements.btnCorrect = document.getElementById('btnCorrect');
      Elements.btnIncorrect = document.getElementById('btnIncorrect');
      Elements.feedbackSent = document.getElementById('feedbackSent');

      Elements.feedbackSection.style.display = 'block';
      Elements.btnCorrect.disabled = false;
      Elements.btnIncorrect.disabled = false;

      Elements.btnCorrect.addEventListener('click', () => FeedbackService.sendFeedback(true));
      Elements.btnIncorrect.addEventListener('click', () => FeedbackService.sendFeedback(false));

      const delLink = document.getElementById('btnDeleteFeedback');
      if (delLink) delLink.addEventListener('click', (e) => { e.preventDefault(); this.deleteFeedbackData(); });
    },

    hide() {
      if (Elements.feedbackSection) {
        Elements.feedbackSection.style.display = 'none';
      }
    },

    async sendFeedback(isCorrect) {
      console.log('[Popup] Sending feedback:', isCorrect ? 'correct' : 'incorrect');

      if (Elements.btnCorrect) Elements.btnCorrect.disabled = true;
      if (Elements.btnIncorrect) Elements.btnIncorrect.disabled = true;

      // Only include score, domain (not full URL), and risk category.
      // Deliberately exclude user-agent, full URL path, and timestamps
      // that could fingerprint the user.
      let domain = null;
      try {
        if (this.currentUrl) domain = new URL(this.currentUrl).hostname;
      } catch (_) { /* malformed URL — leave domain null */ }

      const feedbackPayload = {
        domain: domain,
        score: this.currentScore,
        riskType: Array.isArray(this.currentRiskType) ? this.currentRiskType.join(', ') : this.currentRiskType,
        isCorrect: isCorrect
      };

      try {
        // Stage in local storage for the background service worker to forward
        // to the desktop agent over the existing WebSocket.
        // NOTE: background.js must handle the 'sendFeedback' message type to
        // drain this queue over the WS connection (tracked as a follow-up task).
        const stored = await chrome.storage.local.get(['pendingFeedback']);
        const queue = stored.pendingFeedback || [];
        queue.push(feedbackPayload);
        // Keep at most 50 pending items (no PII that compounds with age)
        if (queue.length > 50) queue.shift();
        await chrome.storage.local.set({ pendingFeedback: queue });

        // Ask the background worker to deliver queued feedback now if connected
        try {
          await chrome.runtime.sendMessage({ type: 'sendFeedback', payload: feedbackPayload });
        } catch (_) {
          // Background may not have the handler yet — staged in storage above
        }

        if (Elements.feedbackSent) Elements.feedbackSent.style.display = 'block';
        console.log('[Popup] Feedback staged for backend delivery');
      } catch (error) {
        console.error('[Popup] Error staging feedback:', error);
        if (Elements.feedbackSent) {
          Elements.feedbackSent.style.display = 'block';
          Elements.feedbackSent.textContent = '✓ Noted (pending connection)';
        }
      }
    },

    // Allows the user to request deletion of any feedback tied to their account.
    // Clears locally-staged feedback and forwards a deletion request to the
    // backend via the WebSocket (best-effort).
    async deleteFeedbackData() {
      console.log('[Popup] User requested feedback data deletion');
      try {
        // Clear locally-staged queue immediately
        await chrome.storage.local.remove('pendingFeedback');
        // Revoke consent so the prompt is shown again next time
        await this.setConsent(false);
        // Forward deletion request to backend (best-effort; may not be connected)
        try {
          await chrome.runtime.sendMessage({ type: 'deleteFeedback' });
        } catch (_) {
          // Not connected — local data is already cleared above
        }
        if (Elements.feedbackSection) {
          Elements.feedbackSection.innerHTML =
            '<div class="feedback-question" style="font-size:0.8em;color:#888">Local feedback data deleted.</div>';
        }
      } catch (e) {
        console.error('[Popup] Error deleting feedback data:', e);
      }
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

      // Feedback buttons are bound dynamically by FeedbackService._showFeedbackButtons()
      // after consent is confirmed, so no static binding here.

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

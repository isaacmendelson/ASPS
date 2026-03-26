// ============================================
// AntiScam Extension - Form Monitor Service
// Monitors form submissions on tracked domains
// ============================================

class FormMonitorService {
  constructor() {
    this.trackedDomains = new Map(); // domain -> {scamKey, trackMode}
    this.alwaysSendFormSubmits = false;
    this.formSubmitHandler = null;
  }

  /**
   * Initialize the service and start monitoring
   */
  init() {
    console.log('[FormMonitor] Initializing...');
    
    // Load tracked domains from storage
    this.loadTrackedDomains();
    
    // Start monitoring form submissions
    this.startMonitoring();
  }

  /**
   * Load tracked domains from chrome.storage
   */
  async loadTrackedDomains() {
    try {
      const result = await chrome.storage.local.get([
        'trackedDomains',
        'alwaysSendFormSubmits'
      ]);

      if (result.trackedDomains && Array.isArray(result.trackedDomains)) {
        this.trackedDomains.clear();
        result.trackedDomains.forEach(item => {
          this.trackedDomains.set(item.Domain, {
            scamKey: item.ScamInProgressKey,
            trackMode: item.TrackMode,
            reportType: item.ReportType
          });
        });
        console.log('[FormMonitor] Loaded tracked domains:', this.trackedDomains.size);
      }

      this.alwaysSendFormSubmits = result.alwaysSendFormSubmits || false;
      console.log('[FormMonitor] AlwaysSendFormSubmits:', this.alwaysSendFormSubmits);

    } catch (error) {
      console.error('[FormMonitor] Error loading tracked domains:', error);
    }
  }

  /**
   * Update tracked domains (called when backend sends SetTrackedDomains)
   * @param {Array} domains - Array of {Domain, ScamInProgressKey, TrackMode, ReportType}
   */
  updateTrackedDomains(domains) {
    this.trackedDomains.clear();
    
    if (domains && Array.isArray(domains)) {
      domains.forEach(item => {
        this.trackedDomains.set(item.Domain, {
          scamKey: item.ScamInProgressKey,
          trackMode: item.TrackMode,
          reportType: item.ReportType
        });
      });
    }

    console.log('[FormMonitor] Updated tracked domains:', this.trackedDomains.size);
  }

  /**
   * Start monitoring form submissions
   */
  startMonitoring() {
    // Remove existing listener if any
    if (this.formSubmitHandler) {
      document.removeEventListener('submit', this.formSubmitHandler, true);
    }

    // Create new handler
    this.formSubmitHandler = (event) => {
      this.handleFormSubmit(event);
    };

    // Add listener in capture phase to catch forms before they submit
    document.addEventListener('submit', this.formSubmitHandler, true);
    console.log('[FormMonitor] Monitoring started');
  }

  /**
   * Stop monitoring form submissions
   */
  stopMonitoring() {
    if (this.formSubmitHandler) {
      document.removeEventListener('submit', this.formSubmitHandler, true);
      this.formSubmitHandler = null;
      console.log('[FormMonitor] Monitoring stopped');
    }
  }

  /**
   * Handle form submit event
   * @param {Event} event - Submit event
   */
  handleFormSubmit(event) {
    const form = event.target;
    
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    const currentDomain = window.location.hostname;
    const trackedInfo = this.trackedDomains.get(currentDomain);

    // Check if we should track this form
    const shouldTrack = trackedInfo || this.alwaysSendFormSubmits;

    if (!shouldTrack) {
      return;
    }

    console.log('[FormMonitor] Form submission detected:', {
      domain: currentDomain,
      tracked: !!trackedInfo,
      alwaysSend: this.alwaysSendFormSubmits
    });

    // Collect form data
    const formData = this.collectFormData(form);

    // Send to background script
    this.sendFormSubmitAlert(formData, trackedInfo);
  }

  /**
   * Collect form data for analysis
   * @param {HTMLFormElement} form - The form element
   * @returns {Object} Form data
   */
  collectFormData(form) {
    const formData = {
      url: window.location.href,
      fromUrl: document.referrer || '',
      formAction: form.action || '',
      method: (form.method || 'GET').toUpperCase(),
      fieldCount: 0,
      hasPasswordField: false,
      hasEmailField: false,
      hasCreditCard: false
    };

    // Credit card patterns
    const creditCardPatterns = /card|credit|cvv|cvc|expir|ccnum|cardnumber/i;

    // Analyze form fields
    const fields = form.querySelectorAll('input, textarea, select');
    formData.fieldCount = fields.length;

    fields.forEach(field => {
      const type = (field.type || '').toLowerCase();
      const name = (field.name || '').toLowerCase();
      const id = (field.id || '').toLowerCase();
      const placeholder = (field.placeholder || '').toLowerCase();

      // Check for password field
      if (type === 'password') {
        formData.hasPasswordField = true;
      }

      // Check for email field
      if (type === 'email' || name.includes('email') || id.includes('email')) {
        formData.hasEmailField = true;
      }

      // Check for credit card fields
      const fieldText = `${name} ${id} ${placeholder}`;
      if (creditCardPatterns.test(fieldText)) {
        formData.hasCreditCard = true;
      }
    });

    return formData;
  }

  /**
   * Send form submit alert to background script
   * @param {Object} formData - Collected form data
   * @param {Object} trackedInfo - Tracked domain info (if any)
   */
  sendFormSubmitAlert(formData, trackedInfo) {
    const message = {
      type: 'form:submitted',
      data: {
        ...formData,
        scamInProgressKey: trackedInfo ? trackedInfo.scamKey : '',
        timestamp: new Date().toISOString()
      }
    };

    // Send to background script
    try {
      chrome.runtime.sendMessage(message, (response) => {
        if (chrome.runtime.lastError) {
          console.error('[FormMonitor] Error sending form alert:', chrome.runtime.lastError);
        } else {
          console.log('[FormMonitor] Form alert sent successfully');
        }
      });
    } catch (error) {
      console.error('[FormMonitor] Exception sending form alert:', error);
    }
  }

  /**
   * Check if current domain is tracked
   * @returns {boolean}
   */
  isCurrentDomainTracked() {
    const currentDomain = window.location.hostname;
    return this.trackedDomains.has(currentDomain);
  }

  /**
   * Get tracked info for current domain
   * @returns {Object|null}
   */
  getTrackedInfoForCurrentDomain() {
    const currentDomain = window.location.hostname;
    return this.trackedDomains.get(currentDomain) || null;
  }
}

// Create singleton instance
export const formMonitorService = new FormMonitorService();
export default formMonitorService;

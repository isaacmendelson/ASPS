import { describe, test, expect, beforeEach, afterEach, jest } from '@jest/globals';

// ── Extracted from content.js for unit testing ──────────────────────────────
// FormMonitor is defined inside the content.js IIFE and is not exported.
// The implementation below is a verbatim copy of the relevant object from
// content.js. If content.js changes, keep this in sync.
// (FR-040)

const FormMonitor = {
  trackedDomains: new Map(),
  alwaysSendFormSubmits: false,
  formSubmitHandler: null,

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
  },

  startMonitoring() {
    if (this.formSubmitHandler) {
      document.removeEventListener('submit', this.formSubmitHandler, true);
    }
    this.formSubmitHandler = (event) => {
      this.handleFormSubmit(event);
    };
    document.addEventListener('submit', this.formSubmitHandler, true);
  },

  handleFormSubmit(event) {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;

    const currentDomain = window.location.hostname;
    const trackedInfo = this.trackedDomains.get(currentDomain);
    const shouldTrack = trackedInfo || this.alwaysSendFormSubmits;
    if (!shouldTrack) return;

    const formData = this.collectFormData(form);
    this.sendFormSubmitAlert(formData, trackedInfo);
  },

  collectFormData(form) {
    const creditCardPatterns = /card|credit|cvv|cvc|expir|ccnum|cardnumber/i;
    const formData = {
      url:             window.location.href,
      fromUrl:         document.referrer || '',
      formAction:      form.action || '',
      method:          (form.method || 'GET').toUpperCase(),
      fieldCount:      0,
      hasPasswordField: false,
      hasEmailField:    false,
      hasCreditCard:    false
    };

    const fields = form.querySelectorAll('input, textarea, select');
    formData.fieldCount = fields.length;

    fields.forEach(field => {
      const type        = (field.type        || '').toLowerCase();
      const name        = (field.name        || '').toLowerCase();
      const id          = (field.id          || '').toLowerCase();
      const placeholder = (field.placeholder || '').toLowerCase();

      if (type === 'password') formData.hasPasswordField = true;

      if (type === 'email' || name.includes('email') || id.includes('email')) {
        formData.hasEmailField = true;
      }

      const fieldText = `${name} ${id} ${placeholder}`;
      if (creditCardPatterns.test(fieldText)) formData.hasCreditCard = true;
    });

    return formData;
  },

  sendFormSubmitAlert(formData, trackedInfo) {
    const message = {
      type: 'form:submitted',
      data: {
        ...formData,
        scamInProgressKey: trackedInfo ? trackedInfo.scamKey : '',
        timestamp: new Date().toISOString()
      }
    };
    chrome.runtime.sendMessage(message, (response) => {
      if (chrome.runtime.lastError) {
        console.error('[FormMonitor] Error sending form alert:', chrome.runtime.lastError);
      }
    });
  }
};

// ─────────────────────────────────────────────────────────────────────────────

// Helper: build an HTMLFormElement with the given fields and append to body.
function makeForm({ action = '', method = 'post', fields = [] } = {}) {
  const form = document.createElement('form');
  form.action = action;
  form.method = method;
  fields.forEach(({ type, name, id, placeholder }) => {
    const input = document.createElement('input');
    if (type)        input.type        = type;
    if (name)        input.name        = name;
    if (id)          input.id          = id;
    if (placeholder) input.placeholder = placeholder;
    form.appendChild(input);
  });
  document.body.appendChild(form);
  return form;
}

// Helper: fire a submit event whose target is `form`.
function fireSubmit(form) {
  const event = new Event('submit', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'target', { value: form, configurable: true });
  FormMonitor.formSubmitHandler && FormMonitor.formSubmitHandler(event);
}

// ─────────────────────────────────────────────────────────────────────────────

describe('FormMonitor', () => {
  beforeEach(() => {
    // Reset FormMonitor state before every test
    FormMonitor.trackedDomains        = new Map();
    FormMonitor.alwaysSendFormSubmits = false;
    FormMonitor.formSubmitHandler     = null;

    // Clear call history then provide a fresh implementation
    chrome.runtime.sendMessage.mockClear();
    chrome.runtime.sendMessage.mockImplementation((msg, callback) => {
      if (callback) callback({ success: true });
    });

    // Clean the DOM
    document.body.innerHTML = '';
  });

  afterEach(() => {
    // Remove any lingering submit listener to avoid cross-test pollution
    if (FormMonitor.formSubmitHandler) {
      document.removeEventListener('submit', FormMonitor.formSubmitHandler, true);
    }
  });

  // ── Monitoring lifecycle ───────────────────────────────────────────────────

  describe('startMonitoring', () => {
    test('attaches a capture-phase submit listener to document', () => {
      // Verifies the listener is registered with useCapture=true (MV3 requirement)
      const spy = jest.spyOn(document, 'addEventListener');
      FormMonitor.startMonitoring();
      expect(spy).toHaveBeenCalledWith('submit', expect.any(Function), true);
      spy.mockRestore();
    });

    test('replaces the old handler when called a second time', () => {
      // Ensures there is no double-listener if startMonitoring is called twice
      const removeSpy = jest.spyOn(document, 'removeEventListener');
      FormMonitor.startMonitoring();
      const firstHandler = FormMonitor.formSubmitHandler;
      FormMonitor.startMonitoring();
      expect(removeSpy).toHaveBeenCalledWith('submit', firstHandler, true);
      removeSpy.mockRestore();
    });

    test('stores a reference to the handler in formSubmitHandler', () => {
      // Handler must be stored so it can be removed later
      FormMonitor.startMonitoring();
      expect(typeof FormMonitor.formSubmitHandler).toBe('function');
    });
  });

  // ── collectFormData — field detection ─────────────────────────────────────

  describe('collectFormData — field detection', () => {
    test('password field sets hasPasswordField=true', () => {
      // Password inputs must be flagged so the backend can assess credential exposure
      const form = makeForm({ fields: [{ type: 'password', name: 'pwd' }] });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasPasswordField).toBe(true);
    });

    test('email-type input sets hasEmailField=true', () => {
      // Standard <input type="email"> must be detected
      const form = makeForm({ fields: [{ type: 'email', name: 'user' }] });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasEmailField).toBe(true);
    });

    test('text input with name containing "email" sets hasEmailField=true', () => {
      // Many forms use type="text" but name="email_address" — must still detect
      const form = makeForm({ fields: [{ type: 'text', name: 'email_address' }] });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasEmailField).toBe(true);
    });

    test('input name containing "card" sets hasCreditCard=true', () => {
      // "cardnumber", "card_number" etc. must be flagged
      const form = makeForm({ fields: [{ type: 'text', name: 'cardnumber' }] });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasCreditCard).toBe(true);
    });

    test('input name containing "cc" / "ccnum" sets hasCreditCard=true', () => {
      // Abbreviated credit card field naming convention
      const form = makeForm({ fields: [{ type: 'text', name: 'ccnum' }] });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasCreditCard).toBe(true);
    });

    test('credit card detected by placeholder text', () => {
      // Some forms name the field generically but describe it via placeholder
      const form = makeForm({ fields: [{ type: 'text', name: 'field1', placeholder: 'card number' }] });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasCreditCard).toBe(true);
    });

    test('no sensitive fields → all flags false, fieldCount equals number of inputs', () => {
      // Baseline: plain username/phone form must produce no false positives
      const form = makeForm({
        fields: [
          { type: 'text', name: 'username' },
          { type: 'text', name: 'phone' }
        ]
      });
      const data = FormMonitor.collectFormData(form);
      expect(data.hasPasswordField).toBe(false);
      expect(data.hasEmailField).toBe(false);
      expect(data.hasCreditCard).toBe(false);
      expect(data.fieldCount).toBe(2);
    });

    test('formAction and method are captured from the form element', () => {
      // Backend needs the form destination to reconstruct the submission intent
      const form = makeForm({ action: 'https://example.com/login', method: 'post' });
      const data = FormMonitor.collectFormData(form);
      expect(data.formAction).toBe('https://example.com/login');
      expect(data.method).toBe('POST');
    });

    test('method defaults to "GET" when form.method is empty', () => {
      // HTMLFormElement.method is '' when not set in some jsdom versions
      const form = document.createElement('form');
      Object.defineProperty(form, 'method', { get: () => '', configurable: true });
      const data = FormMonitor.collectFormData(form);
      expect(data.method).toBe('GET');
    });

    test('textarea and select fields are counted in fieldCount', () => {
      // fieldCount must include all interactive form controls, not just inputs
      const form = document.createElement('form');
      form.appendChild(document.createElement('textarea'));
      const sel = document.createElement('select');
      form.appendChild(sel);
      document.body.appendChild(form);
      const data = FormMonitor.collectFormData(form);
      expect(data.fieldCount).toBe(2);
    });
  });

  // ── sendFormSubmitAlert — messaging ───────────────────────────────────────

  describe('sendFormSubmitAlert', () => {
    const baseFormData = {
      url: 'https://example.com',
      fromUrl: '',
      formAction: '/login',
      method: 'POST',
      fieldCount: 1,
      hasPasswordField: true,
      hasEmailField: false,
      hasCreditCard: false
    };

    test('calls chrome.runtime.sendMessage with type "form:submitted"', () => {
      // Verifies the correct WS message type is used for the agent protocol
      FormMonitor.sendFormSubmitAlert(baseFormData, null);
      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({ type: 'form:submitted' }),
        expect.any(Function)
      );
    });

    test('message data includes all formData fields plus a timestamp', () => {
      // Agent must receive the full field-detection result
      FormMonitor.sendFormSubmitAlert(baseFormData, null);
      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          data: expect.objectContaining({
            hasPasswordField: true,
            method:           'POST',
            timestamp:        expect.any(String)
          })
        }),
        expect.any(Function)
      );
    });

    test('scamInProgressKey is populated from trackedInfo.scamKey when present', () => {
      // Backend correlates the form event to a ScamInProgress record via this key
      const trackedInfo = { scamKey: 'abc-123', trackMode: 1, reportType: 2 };
      FormMonitor.sendFormSubmitAlert(baseFormData, trackedInfo);
      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          data: expect.objectContaining({ scamInProgressKey: 'abc-123' })
        }),
        expect.any(Function)
      );
    });

    test('scamInProgressKey is empty string when trackedInfo is null', () => {
      // Generic (alwaysSendFormSubmits) submissions have no ScamInProgress key
      FormMonitor.sendFormSubmitAlert(baseFormData, null);
      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          data: expect.objectContaining({ scamInProgressKey: '' })
        }),
        expect.any(Function)
      );
    });
  });

  // ── handleFormSubmit — tracking gate ──────────────────────────────────────

  describe('handleFormSubmit — tracking gate', () => {
    test('does NOT send when domain is not tracked and alwaysSendFormSubmits=false', () => {
      // Default mode: only report forms on monitored domains
      FormMonitor.alwaysSendFormSubmits = false;
      const form = makeForm({ fields: [{ type: 'password', name: 'p' }] });
      fireSubmit(form);
      expect(chrome.runtime.sendMessage).not.toHaveBeenCalled();
    });

    test('sends alert when alwaysSendFormSubmits=true regardless of domain', () => {
      // Surveillance mode: report every password form even on untracked domains
      FormMonitor.alwaysSendFormSubmits = true;
      FormMonitor.startMonitoring();
      const form = makeForm({ fields: [{ type: 'password', name: 'p' }] });
      fireSubmit(form);
      expect(chrome.runtime.sendMessage).toHaveBeenCalled();
    });

    test('sends alert when current domain is in trackedDomains', () => {
      // window.location.hostname is "example.com" in jsdom (set by jest.setup.cjs)
      FormMonitor.trackedDomains.set('example.com', {
        scamKey: 'k1', trackMode: 1, reportType: 1
      });
      FormMonitor.startMonitoring();
      const form = makeForm({ fields: [{ type: 'text', name: 'x' }] });
      fireSubmit(form);
      expect(chrome.runtime.sendMessage).toHaveBeenCalled();
    });

    test('ignores event when event.target is not an HTMLFormElement', () => {
      // Guard against synthetic events fired by non-form elements
      FormMonitor.alwaysSendFormSubmits = true;
      FormMonitor.startMonitoring();
      const event = new Event('submit', { bubbles: true });
      const div = document.createElement('div');
      Object.defineProperty(event, 'target', { value: div, configurable: true });
      FormMonitor.formSubmitHandler(event);
      expect(chrome.runtime.sendMessage).not.toHaveBeenCalled();
    });
  });

  // ── updateTrackedDomains ───────────────────────────────────────────────────

  describe('updateTrackedDomains', () => {
    test('populates trackedDomains map from domain array', () => {
      // Domains pushed from the background script must be stored correctly
      FormMonitor.updateTrackedDomains([
        { Domain: 'scam.com', ScamInProgressKey: 'key1', TrackMode: 2, ReportType: 1 }
      ]);
      expect(FormMonitor.trackedDomains.has('scam.com')).toBe(true);
      expect(FormMonitor.trackedDomains.get('scam.com').scamKey).toBe('key1');
    });

    test('stores TrackMode and ReportType alongside scamKey', () => {
      FormMonitor.updateTrackedDomains([
        { Domain: 'bank.com', ScamInProgressKey: 'k2', TrackMode: 3, ReportType: 4 }
      ]);
      const entry = FormMonitor.trackedDomains.get('bank.com');
      expect(entry.trackMode).toBe(3);
      expect(entry.reportType).toBe(4);
    });

    test('clears previous domains before loading new set', () => {
      // Stale domains must not persist across updates
      FormMonitor.trackedDomains.set('old.com', {});
      FormMonitor.updateTrackedDomains([
        { Domain: 'new.com', ScamInProgressKey: 'k', TrackMode: 1, ReportType: 1 }
      ]);
      expect(FormMonitor.trackedDomains.has('old.com')).toBe(false);
      expect(FormMonitor.trackedDomains.has('new.com')).toBe(true);
    });

    test('handles empty array without error', () => {
      // Agent may send an empty list when all scams are resolved
      expect(() => FormMonitor.updateTrackedDomains([])).not.toThrow();
      expect(FormMonitor.trackedDomains.size).toBe(0);
    });

    test('handles null/undefined gracefully without throwing', () => {
      // Defensive: malformed message from agent must not crash the content script
      expect(() => FormMonitor.updateTrackedDomains(null)).not.toThrow();
      expect(() => FormMonitor.updateTrackedDomains(undefined)).not.toThrow();
    });

    test('supports multiple domains in a single update', () => {
      FormMonitor.updateTrackedDomains([
        { Domain: 'a.com', ScamInProgressKey: 'k1', TrackMode: 1, ReportType: 1 },
        { Domain: 'b.com', ScamInProgressKey: 'k2', TrackMode: 1, ReportType: 1 }
      ]);
      expect(FormMonitor.trackedDomains.size).toBe(2);
    });
  });
});

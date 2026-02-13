/**
 * ShadowContainer - Shadow DOM container with re-injection defense
 *
 * Creates a closed shadow DOM container that resists manipulation by
 * malicious page JavaScript. If the host element is removed from the DOM,
 * it will be automatically re-injected.
 *
 * @module warning/ShadowContainer
 */

/**
 * Shadow DOM container with MutationObserver-based re-injection defense.
 * Uses closed shadow root so page JS cannot access shadowRoot property.
 */
class ShadowContainer {
  constructor() {
    /** @private @type {ShadowRoot|null} */
    this._shadowRoot = null;

    /** @private @type {HTMLElement|null} */
    this._hostElement = null;

    /** @private @type {MutationObserver|null} */
    this._observer = null;

    /** @private @type {{html: string, styles: string}|null} */
    this._content = null;

    /** @private @type {number[]} Re-injection timestamps for debounce */
    this._reinjectionTimestamps = [];

    /** @private @type {number} Total re-injection count for attack detection */
    this._totalReinjections = 0;

    /** @private @type {number} Max re-injections per second (debounce) */
    this._maxRejectionsPerSecond = 3;

    /** @private @type {number} Threshold for excessive re-injections warning */
    this._excessiveReinjectionThreshold = 10;

    /** @private @type {boolean} Whether container has been destroyed */
    this._destroyed = false;
  }

  /**
   * Creates the shadow container and injects content.
   *
   * @param {string} html - HTML content to inject into shadow DOM
   * @param {string} styles - CSS styles to inject into shadow DOM
   * @returns {ShadowRoot} The created shadow root (for internal use only)
   */
  create(html, styles) {
    // Clean up any existing container first
    if (this._hostElement && this._hostElement.parentNode) {
      this._observer?.disconnect();
      this._hostElement.parentNode.removeChild(this._hostElement);
    }

    this._destroyed = false;

    // Create host element with unique ID
    const timestamp = Date.now();
    const random = Math.random().toString(36).substring(2, 11);
    const hostId = `asps-${timestamp}-${random}`;

    this._hostElement = document.createElement('div');
    this._hostElement.id = hostId;

    // Attach closed shadow root - page cannot access via element.shadowRoot
    this._shadowRoot = this._hostElement.attachShadow({ mode: 'closed' });

    // Inject styles via style element
    const styleElement = document.createElement('style');
    styleElement.textContent = styles;
    this._shadowRoot.appendChild(styleElement);

    // Inject content wrapper div with provided HTML
    const contentWrapper = document.createElement('div');
    contentWrapper.className = 'shadow-content';
    contentWrapper.innerHTML = html;
    this._shadowRoot.appendChild(contentWrapper);

    // Store content for re-injection
    this._content = { html, styles };

    // Append host to document.body
    document.body.appendChild(this._hostElement);

    // Set up MutationObserver defense
    this._setupDefense();

    return this._shadowRoot;
  }

  /**
   * Sets up MutationObserver to detect and respond to removal of host element.
   * @private
   */
  _setupDefense() {
    // Disconnect any existing observer
    if (this._observer) {
      this._observer.disconnect();
    }

    this._observer = new MutationObserver((mutations) => {
      if (this._destroyed) {
        return;
      }

      for (const mutation of mutations) {
        if (mutation.type === 'childList') {
          for (const removedNode of mutation.removedNodes) {
            if (removedNode === this._hostElement) {
              console.warn('[AntiScam] Warning container removed by page - attempting re-injection');
              this._handleRemoval();
              return;
            }
          }
        }
      }
    });

    // Observe document.body for childList mutations (not subtree)
    this._observer.observe(document.body, {
      childList: true,
      subtree: false
    });
  }

  /**
   * Handles removal of host element with debounce to prevent infinite loops.
   * @private
   */
  _handleRemoval() {
    if (this._destroyed) {
      return;
    }

    const now = Date.now();

    // Clean up old timestamps (older than 1 second)
    this._reinjectionTimestamps = this._reinjectionTimestamps.filter(
      ts => now - ts < 1000
    );

    // Check debounce: max 3 re-injections per second
    if (this._reinjectionTimestamps.length >= this._maxRejectionsPerSecond) {
      console.error('[AntiScam] Re-injection rate limit exceeded - possible attack detected');
      return;
    }

    // Track this re-injection
    this._reinjectionTimestamps.push(now);
    this._totalReinjections++;

    // Log excessive re-injections as potential attack
    if (this._totalReinjections === this._excessiveReinjectionThreshold) {
      console.error(
        `[AntiScam] Excessive re-injections detected (${this._totalReinjections}+) - ` +
        'page may be attempting to suppress warning'
      );
    }

    // Perform re-injection
    this._reinject();
  }

  /**
   * Re-injects the shadow container with stored content.
   * @private
   */
  _reinject() {
    if (this._content && !this._destroyed) {
      this.create(this._content.html, this._content.styles);
    }
  }

  /**
   * Gets the shadow root for internal manipulation.
   * @returns {ShadowRoot|null}
   */
  getShadowRoot() {
    return this._shadowRoot;
  }

  /**
   * Gets the host element.
   * @returns {HTMLElement|null}
   */
  getHostElement() {
    return this._hostElement;
  }

  /**
   * Gets the total number of re-injections that have occurred.
   * @returns {number}
   */
  getReinjectionCount() {
    return this._totalReinjections;
  }

  /**
   * Destroys the container, disconnecting observers and removing from DOM.
   * Call this when the warning is legitimately dismissed.
   */
  destroy() {
    this._destroyed = true;

    // Disconnect MutationObserver
    if (this._observer) {
      this._observer.disconnect();
      this._observer = null;
    }

    // Remove host element from parent
    if (this._hostElement && this._hostElement.parentNode) {
      this._hostElement.parentNode.removeChild(this._hostElement);
    }

    // Clear references
    this._shadowRoot = null;
    this._hostElement = null;
    this._content = null;
    this._reinjectionTimestamps = [];
  }
}

// Export as both default and named export
export { ShadowContainer };
export default ShadowContainer;

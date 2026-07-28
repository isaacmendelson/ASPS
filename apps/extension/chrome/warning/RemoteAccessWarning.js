/**
 * RemoteAccessWarning - Shadow DOM protected warning overlay
 *
 * Displays a full-page warning when incoming remote access is detected.
 * The warning is protected by Shadow DOM and cannot be removed by
 * malicious page JavaScript.
 *
 * @module warning/RemoteAccessWarning
 */

import { ShadowContainer } from './ShadowContainer.js';
import { FrictionController } from './FrictionController.js';
import { WARNING_STYLES } from './warning-styles.js';

/**
 * Generates the HTML content for the warning overlay.
 *
 * @param {string} toolName The name of the detected remote access tool
 * @param {string|null} [statusMessage] Optional status line (e.g. disconnect failure details)
 * @returns {string} HTML string for the warning
 */
function escapeHTML(str) {
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function getWarningHTML(toolName, statusMessage) {
  const toolDisplay = escapeHTML(toolName || 'Remote access software');
  const statusRow = statusMessage
    ? `<p class="status-message">${escapeHTML(statusMessage)}</p>`
    : '';

  return `
    <div class="overlay">
      <div class="warning-card">
        <div class="warning-icon">\u{1F6E1}\u{FE0F}</div>
        <h1 class="warning-title">We're looking out for you</h1>
        <p class="warning-subtitle">
          <span class="tool-badge">${toolDisplay} detected</span>
        </p>

        <div class="warning-body">
          <p><strong>Someone may be controlling your computer remotely.</strong></p>
          <p>Scammers use remote access tools to steal money, passwords, and personal information. They often pretend to be tech support, banks, or government agencies.</p>
          <p>If you didn't invite someone you personally know and trust to connect, please close this session immediately.</p>
          ${statusRow}
        </div>

        <div class="checkbox-row">
          <input type="checkbox" id="trust-checkbox" class="trust-checkbox">
          <label for="trust-checkbox" class="checkbox-label">I know this person and trust them</label>
        </div>

        <div class="button-row">
          <button class="close-session-btn">Close session</button>
          <button class="continue-btn" disabled>
            Continue anyway
            <span class="timer-display"></span>
          </button>
        </div>
      </div>
    </div>
  `;
}

/**
 * Remote access warning overlay with Shadow DOM protection.
 * Displays contextual warning with friction mechanism for bypass.
 */
class RemoteAccessWarning {
  constructor() {
    /** @private @type {ShadowContainer|null} */
    this._container = null;

    /** @private @type {FrictionController|null} */
    this._friction = null;

    /** @private @type {function():void|null} */
    this._onCloseSession = null;

    /** @private @type {function():void|null} */
    this._onContinue = null;

    /** @private @type {function():void|null} */
    this._onDismiss = null;
  }

  /**
   * Shows the remote access warning overlay.
   *
   * @param {Object} options Configuration options
   * @param {string} [options.toolName] Name of detected remote access tool
   * @param {number} [options.toolId] ID of detected tool
   * @param {function():void} [options.onCloseSession] Callback when user clicks "Close session"
   * @param {function():void} [options.onContinue] Callback when user completes friction and clicks "Continue anyway"
   * @param {function():void} [options.onDismiss] Callback when warning is dismissed by any means
   */
  show(options = {}) {
    // If already showing, don't create another
    if (this._container) {
      console.log('[RemoteAccessWarning] Already showing, ignoring show request');
      return;
    }

    const { toolName, toolId, onCloseSession, onContinue, onDismiss, statusMessage } = options;

    // Store callbacks
    this._onCloseSession = onCloseSession || null;
    this._onContinue = onContinue || null;
    this._onDismiss = onDismiss || null;

    // Create shadow container
    this._container = new ShadowContainer();
    const shadowRoot = this._container.create(
      getWarningHTML(toolName || 'Remote access software', statusMessage || null),
      WARNING_STYLES
    );

    // Create and initialize friction controller
    this._friction = new FrictionController({
      timerDuration: 7,
      onStateChange: (canContinue) => {
        console.log('[RemoteAccessWarning] Friction state changed:', { canContinue });
      }
    });
    this._friction.init(shadowRoot);

    // Setup button click listeners
    this._setupEventListeners(shadowRoot);

    console.log('[RemoteAccessWarning] Warning displayed for:', toolName || 'unknown tool');
  }

  /**
   * Sets up event listeners for buttons within the shadow DOM.
   *
   * @param {ShadowRoot} shadowRoot
   * @private
   */
  _setupEventListeners(shadowRoot) {
    // Close session button
    const closeBtn = shadowRoot.querySelector('.close-session-btn');
    if (closeBtn) {
      closeBtn.addEventListener('click', () => {
        this._handleCloseSession();
      });
    }

    // Continue anyway button
    const continueBtn = shadowRoot.querySelector('.continue-btn');
    if (continueBtn) {
      continueBtn.addEventListener('click', () => {
        this._handleContinue();
      });
    }
  }

  /**
   * Handles the "Close session" button click.
   * @private
   */
  _handleCloseSession() {
    console.log('[RemoteAccessWarning] Close session clicked');

    // Show "disconnecting" state — keep warning visible until result arrives
    const shadowRoot = this._container?.shadowRoot;
    const closeBtn = shadowRoot?.querySelector('.close-session-btn');
    if (closeBtn) {
      closeBtn.disabled = true;
      closeBtn.textContent = 'Disconnecting...';
    }

    // Call callback if provided — the callback decides whether to hide or re-show
    if (this._onCloseSession) {
      try {
        this._onCloseSession();
      } catch (err) {
        console.error('[RemoteAccessWarning] Error in onCloseSession callback:', err);
        if (closeBtn) {
          closeBtn.disabled = false;
          closeBtn.textContent = 'Close session';
        }
      }
    }
  }

  /**
   * Handles the "Continue anyway" button click.
   * Only proceeds if friction is complete.
   * @private
   */
  _handleContinue() {
    // Check friction is complete
    if (!this._friction || !this._friction.canContinue()) {
      console.log('[RemoteAccessWarning] Continue blocked - friction not complete');
      return;
    }

    console.log('[RemoteAccessWarning] Continue anyway clicked (friction complete)');

    // Call callback if provided
    if (this._onContinue) {
      try {
        this._onContinue();
      } catch (err) {
        console.error('[RemoteAccessWarning] Error in onContinue callback:', err);
      }
    }

    // Hide the warning
    this.hide();
  }

  /**
   * Hides and destroys the warning overlay.
   */
  hide() {
    console.log('[RemoteAccessWarning] Hiding warning');

    // Destroy friction controller
    if (this._friction) {
      this._friction.destroy();
      this._friction = null;
    }

    // Destroy container
    if (this._container) {
      this._container.destroy();
      this._container = null;
    }

    // Call dismiss callback
    if (this._onDismiss) {
      try {
        this._onDismiss();
      } catch (err) {
        console.error('[RemoteAccessWarning] Error in onDismiss callback:', err);
      }
    }

    // Clear callback references
    this._onCloseSession = null;
    this._onContinue = null;
    this._onDismiss = null;
  }

  /**
   * Checks if the warning is currently being displayed.
   *
   * @returns {boolean}
   */
  isShowing() {
    return this._container !== null;
  }
}

// Export class
export { RemoteAccessWarning };
export default RemoteAccessWarning;

// Export singleton instance for convenience
export const remoteAccessWarning = new RemoteAccessWarning();

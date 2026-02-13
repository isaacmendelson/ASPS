/**
 * FrictionController - Timer and checkbox friction mechanism
 *
 * Manages the deliberate friction required before allowing users to bypass
 * the remote access warning. Both a countdown timer AND explicit checkbox
 * confirmation are required to enable the "Continue anyway" button.
 *
 * @module warning/FrictionController
 */

/**
 * Controls the friction mechanism for warning bypass.
 * Requires both timer completion (default 7s) AND checkbox confirmation.
 */
class FrictionController {
  /**
   * @param {Object} options Configuration options
   * @param {number} [options.timerDuration=7] Timer duration in seconds
   * @param {function(boolean):void} [options.onStateChange] Callback when canContinue state changes
   */
  constructor(options = {}) {
    /** @private @type {number} Timer duration in seconds */
    this.timerDuration = options.timerDuration || 7;

    /** @private @type {function(boolean):void|null} */
    this._onStateChange = options.onStateChange || null;

    /** @private @type {boolean} */
    this._timerComplete = false;

    /** @private @type {boolean} */
    this._checkboxChecked = false;

    /** @private @type {number|null} Interval ID for cleanup */
    this._intervalId = null;

    /** @private @type {HTMLButtonElement|null} */
    this._continueButton = null;

    /** @private @type {HTMLElement|null} */
    this._timerDisplay = null;

    /** @private @type {HTMLInputElement|null} */
    this._checkbox = null;
  }

  /**
   * Initialize the friction controller with DOM elements from shadow root.
   *
   * @param {ShadowRoot} shadowRoot The shadow root containing friction UI elements
   */
  init(shadowRoot) {
    // Query required elements
    this._continueButton = shadowRoot.querySelector('.continue-btn');
    this._timerDisplay = shadowRoot.querySelector('.timer-display');
    this._checkbox = shadowRoot.querySelector('.trust-checkbox');

    // Set initial button state (disabled)
    this._updateButtonState();

    // Setup checkbox change listener
    if (this._checkbox) {
      this._checkbox.addEventListener('change', () => {
        this._checkboxChecked = this._checkbox.checked;
        this._updateState();
      });
    }

    // Start the countdown timer
    this._startTimer();
  }

  /**
   * Starts the countdown timer.
   * @private
   */
  _startTimer() {
    let remaining = this.timerDuration;

    // Update display initially
    this._updateTimerDisplay(remaining);

    // Start interval
    this._intervalId = setInterval(() => {
      remaining--;
      this._updateTimerDisplay(remaining);

      if (remaining <= 0) {
        clearInterval(this._intervalId);
        this._intervalId = null;
        this._timerComplete = true;
        this._updateState();
      }
    }, 1000);
  }

  /**
   * Updates the timer display text and styling.
   *
   * @param {number} seconds Remaining seconds
   * @private
   */
  _updateTimerDisplay(seconds) {
    if (!this._timerDisplay) return;

    if (seconds > 0) {
      this._timerDisplay.textContent = `Wait ${seconds}s`;
      this._timerDisplay.classList.add('counting');
    } else {
      this._timerDisplay.textContent = '';
      this._timerDisplay.classList.remove('counting');
    }
  }

  /**
   * Updates state and notifies callback.
   * @private
   */
  _updateState() {
    this._updateButtonState();

    // Call state change callback if provided
    if (this._onStateChange) {
      const canContinue = this._timerComplete && this._checkboxChecked;
      this._onStateChange(canContinue);
    }
  }

  /**
   * Updates the continue button's disabled state and enabled class.
   * @private
   */
  _updateButtonState() {
    const canContinue = this._timerComplete && this._checkboxChecked;

    if (this._continueButton) {
      this._continueButton.disabled = !canContinue;
      this._continueButton.classList.toggle('enabled', canContinue);
    }
  }

  /**
   * Checks if the continue action is allowed.
   *
   * @returns {boolean} True if both timer and checkbox conditions are met
   */
  canContinue() {
    return this._timerComplete && this._checkboxChecked;
  }

  /**
   * Checks if the timer has completed.
   *
   * @returns {boolean}
   */
  isTimerComplete() {
    return this._timerComplete;
  }

  /**
   * Checks if the checkbox is checked.
   *
   * @returns {boolean}
   */
  isCheckboxChecked() {
    return this._checkboxChecked;
  }

  /**
   * Destroys the friction controller, cleaning up resources.
   */
  destroy() {
    // Clear interval if running
    if (this._intervalId) {
      clearInterval(this._intervalId);
      this._intervalId = null;
    }

    // Clear references
    this._continueButton = null;
    this._timerDisplay = null;
    this._checkbox = null;
    this._onStateChange = null;
  }
}

// Export as both default and named export
export { FrictionController };
export default FrictionController;

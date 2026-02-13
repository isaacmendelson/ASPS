/**
 * CSS-in-JS styles for warning overlay
 *
 * All styles are contained within this JavaScript string for injection
 * into Shadow DOM. This ensures styles travel with the shadow container
 * and are fully isolated from page styles.
 *
 * @module warning/warning-styles
 */

/**
 * Complete CSS styles for the warning overlay.
 * Injected into closed shadow DOM to prevent page CSS interference.
 *
 * Design principles:
 * - Dark neutral background (#1a1a2e) - non-alarming, professional
 * - Amber accent (#fbbf24) for warning elements - attention without panic
 * - Green primary action (#22c55e) - "Close session" is the safe/positive choice
 * - Muted secondary action - "Continue anyway" requires deliberate friction
 *
 * @type {string}
 */
export const WARNING_STYLES = `
  /* ========================================
   * Host element (shadow container)
   * Reset all inherited styles, establish fixed positioning
   * ======================================== */
  :host {
    all: initial;
    position: fixed !important;
    top: 0 !important;
    left: 0 !important;
    right: 0 !important;
    bottom: 0 !important;
    z-index: 2147483647 !important;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif !important;
    pointer-events: auto !important;
  }

  /* Universal box-sizing within shadow DOM */
  *, *::before, *::after {
    box-sizing: border-box;
  }

  /* ========================================
   * Overlay backdrop
   * Full coverage, blur effect, centered flex
   * ======================================== */
  .overlay {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.85);
    backdrop-filter: blur(8px);
    -webkit-backdrop-filter: blur(8px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 20px;
  }

  /* ========================================
   * Warning card container
   * Main modal with dark neutral background
   * ======================================== */
  .warning-card {
    background: #1a1a2e;
    border-radius: 16px;
    max-width: 520px;
    width: 90%;
    padding: 32px;
    color: white;
    text-align: center;
    box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5),
                0 0 0 1px rgba(255, 255, 255, 0.1);
    animation: cardAppear 0.3s ease-out;
  }

  @keyframes cardAppear {
    from {
      opacity: 0;
      transform: scale(0.95) translateY(-10px);
    }
    to {
      opacity: 1;
      transform: scale(1) translateY(0);
    }
  }

  /* ========================================
   * Typography
   * Title, subtitle, body text styles
   * ======================================== */
  .warning-icon {
    font-size: 64px;
    margin-bottom: 16px;
    line-height: 1;
  }

  .warning-title {
    font-size: 24px;
    font-weight: 700;
    margin: 0 0 8px 0;
    color: #fbbf24;
    line-height: 1.2;
  }

  .warning-subtitle {
    font-size: 16px;
    color: rgba(255, 255, 255, 0.8);
    margin: 0 0 24px 0;
    line-height: 1.4;
  }

  .warning-body {
    background: rgba(255, 255, 255, 0.1);
    border-radius: 12px;
    padding: 20px;
    margin-bottom: 24px;
    text-align: left;
  }

  .warning-body p {
    margin: 0 0 12px 0;
    font-size: 15px;
    line-height: 1.6;
    color: rgba(255, 255, 255, 0.9);
  }

  .warning-body p:last-child {
    margin-bottom: 0;
  }

  .warning-body strong {
    color: white;
    font-weight: 600;
  }

  /* ========================================
   * Tool badge
   * Displays detected tool name
   * ======================================== */
  .tool-badge {
    display: inline-block;
    background: #ef4444;
    color: white;
    padding: 4px 12px;
    border-radius: 20px;
    font-weight: 600;
    font-size: 14px;
    text-transform: none;
  }

  /* ========================================
   * Checkbox row
   * Trust confirmation checkbox and label
   * ======================================== */
  .checkbox-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    margin-bottom: 20px;
  }

  .trust-checkbox {
    width: 20px;
    height: 20px;
    cursor: pointer;
    accent-color: #fbbf24;
  }

  .checkbox-label {
    font-size: 14px;
    color: rgba(255, 255, 255, 0.9);
    cursor: pointer;
    user-select: none;
  }

  /* ========================================
   * Button row
   * Primary (close) and secondary (continue) actions
   * ======================================== */
  .button-row {
    display: flex;
    gap: 12px;
    justify-content: center;
    flex-wrap: wrap;
  }

  /* Primary action: Close session (safe choice) */
  .close-session-btn {
    background: linear-gradient(135deg, #22c55e 0%, #16a34a 100%);
    color: white;
    border: none;
    padding: 16px 32px;
    border-radius: 10px;
    font-size: 16px;
    font-weight: 700;
    cursor: pointer;
    transition: all 0.2s ease;
    box-shadow: 0 4px 15px rgba(34, 197, 94, 0.4);
  }

  .close-session-btn:hover {
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(34, 197, 94, 0.6);
  }

  .close-session-btn:active {
    transform: translateY(0);
    box-shadow: 0 2px 10px rgba(34, 197, 94, 0.4);
  }

  .close-session-btn:focus {
    outline: 2px solid #22c55e;
    outline-offset: 2px;
  }

  /* Secondary action: Continue anyway (requires friction) */
  .continue-btn {
    background: rgba(255, 255, 255, 0.1);
    color: rgba(255, 255, 255, 0.5);
    border: 1px solid rgba(255, 255, 255, 0.2);
    padding: 12px 24px;
    border-radius: 8px;
    font-size: 14px;
    font-weight: 500;
    cursor: not-allowed;
    transition: all 0.2s ease;
  }

  .continue-btn.enabled {
    color: rgba(255, 255, 255, 0.8);
    cursor: pointer;
  }

  .continue-btn.enabled:hover {
    background: rgba(255, 255, 255, 0.15);
    border-color: rgba(255, 255, 255, 0.3);
  }

  .continue-btn.enabled:focus {
    outline: 2px solid rgba(255, 255, 255, 0.5);
    outline-offset: 2px;
  }

  /* ========================================
   * Timer display
   * Countdown before continue button enables
   * ======================================== */
  .timer-display {
    font-size: 12px;
    color: rgba(255, 255, 255, 0.5);
    margin-left: 8px;
    font-weight: 400;
  }

  .timer-display.counting {
    color: #fbbf24;
  }

  /* ========================================
   * Responsive adjustments
   * ======================================== */
  @media (max-width: 480px) {
    .warning-card {
      padding: 24px 20px;
    }

    .warning-title {
      font-size: 20px;
    }

    .warning-body {
      padding: 16px;
    }

    .warning-body p {
      font-size: 14px;
    }

    .button-row {
      flex-direction: column;
    }

    .close-session-btn,
    .continue-btn {
      width: 100%;
    }
  }
`;

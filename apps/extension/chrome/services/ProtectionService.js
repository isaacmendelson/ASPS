// ============================================
// AntiScam Extension - Protection Service
// Handles warning display and page blocking
// ============================================
//
// CANONICAL RISK SCALE (ASPS-624)
//   Higher score = more dangerous.  0-20: safe, 21-40: low concern,
//   41-60: moderate, 61-80: high, 81-100: critical.
//
//   The Backend provides the protective action directly (protectiveAction field
//   in scan results).  PROTECTIVE_ACTION constants are in ascending severity:
//   NONE(0) < NOTIFY(1) < WARN_BANNER(2) < WARN_MODAL(3) < BLOCK(4).
//
//   There is intentionally no client-side score→action mapping.  Any such
//   mapping must live in the Backend scoring policy, not here.
// ============================================

import { MSG, PROTECTIVE_ACTION, WARNING_STYLE } from '../messaging/MessageTypes.js';

class ProtectionService {
  // Execute protective action on the specified tab.
  //
  // targetTabId — the numeric Chrome tab ID of the tab that originated the
  //   scan.  When provided the action targets that tab directly.  Pass null
  //   (or omit) only as a last resort; in that case we fall back to the
  //   currently-active tab (the old behaviour, which was the DT-6/EX-2 bug).
  async executeAction(action, riskType, score, targetTabId = null) {
    let tabId;
    if (targetTabId != null && !Number.isNaN(Number(targetTabId))) {
      tabId = Number(targetTabId);
    } else {
      const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
      if (!tabs[0]) return;
      tabId = tabs[0].id;
      console.warn('[ProtectionService] No targetTabId supplied — falling back to active tab:', tabId);
    }

    console.log(`[ProtectionService] Executing action: ${action} on tab ${tabId}`);

    switch (action) {
      case PROTECTIVE_ACTION.WARN_BANNER:
        await this.showWarning(tabId, WARNING_STYLE.BANNER, riskType, score);
        break;

      case PROTECTIVE_ACTION.WARN_MODAL:
        await this.showWarning(tabId, WARNING_STYLE.MODAL, riskType, score);
        break;

      case PROTECTIVE_ACTION.BLOCK:
        await this.blockPage(tabId, riskType, score);
        break;

      case PROTECTIVE_ACTION.NOTIFY:
        await this.showNotification(riskType, score);
        break;

      case PROTECTIVE_ACTION.ENABLE_URL_TRACKING:
        // URL tracking is handled separately via enableUrlTracking in background.js
        // This action contains the domain and duration in the action message
        console.log('[ProtectionService] EnableUrlTracking action received - handled by background.js');
        break;

      case PROTECTIVE_ACTION.NONE:
      default:
        // No action needed
        break;
    }
  }

  // Show warning on page
  async showWarning(tabId, style, riskType, score) {
    try {
      await chrome.tabs.sendMessage(tabId, {
        type: MSG.SHOW_WARNING,
        style: style,
        riskType: riskType,
        score: score
      });
    } catch (e) {
      console.error('[ProtectionService] Error showing warning:', e);
    }
  }

  // Block page
  async blockPage(tabId, riskType, score) {
    try {
      await chrome.tabs.sendMessage(tabId, {
        type: MSG.BLOCK_PAGE,
        riskType: riskType,
        score: score
      });
    } catch (e) {
      console.error('[ProtectionService] Error blocking page:', e);
    }
  }

  // Remove warning from page
  async removeWarning(tabId) {
    try {
      await chrome.tabs.sendMessage(tabId, {
        type: MSG.REMOVE_WARNING
      });
    } catch (e) {
      console.error('[ProtectionService] Error removing warning:', e);
    }
  }

  // Show browser notification
  async showNotification(riskType, score) {
    const riskLabels = Array.isArray(riskType)
      ? riskType.map(r => this.getRiskLabel(r)).join(', ')
      : this.getRiskLabel(riskType);

    try {
      await chrome.notifications.create({
        type: 'basic',
        iconUrl: 'icons/icon128.png',
        title: 'AntiScam Alert',
        message: `Risk detected: ${riskLabels}\nRisk Score: ${score}/100`,
        priority: 2
      });
    } catch (e) {
      console.error('[ProtectionService] Error showing notification:', e);
    }
  }

  // Get risk label from type
  getRiskLabel(riskType) {
    const labels = {
      0: 'Safe',
      1: 'Phishing',
      2: 'Cloaking',
      3: 'Impersonation',
      4: 'Fake Domain',
      5: 'Unknown Risk'
    };
    return labels[riskType] || 'Unknown';
  }

  // Get action name from type
  getActionName(action) {
    const names = {
      [PROTECTIVE_ACTION.NONE]: 'None',
      [PROTECTIVE_ACTION.NOTIFY]: 'Notification',
      [PROTECTIVE_ACTION.WARN_BANNER]: 'Warning Banner',
      [PROTECTIVE_ACTION.WARN_MODAL]: 'Warning Modal',
      [PROTECTIVE_ACTION.BLOCK]: 'Block Page'
    };
    return names[action] || 'Unknown';
  }

}

// Singleton instance
export const protectionService = new ProtectionService();
export default protectionService;

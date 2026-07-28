// ============================================
// ProtectionService unit tests — ASPS-624
//
// Verifies:
//   1. PROTECTIVE_ACTION constants are in strict ascending severity order.
//   2. determineAction() has been removed (no client-side score→action mapping).
//   3. executeAction() dispatches to the correct sub-method for each action.
//   4. Utility helpers (getRiskLabel, getActionName) return expected strings.
// ============================================

import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('ProtectionService — ASPS-624', () => {
  let protectionService;
  let PROTECTIVE_ACTION;

  beforeEach(async () => {
    // Fresh module import per test so mocks are clean.
    jest.resetModules();

    // chrome.tabs.sendMessage must exist before the module loads.
    chrome.tabs.sendMessage.mockImplementation((_tabId, _msg, cb) => {
      if (cb) cb({ success: true });
    });

    const ps = await import('@/services/ProtectionService.js');
    const mt = await import('@/messaging/MessageTypes.js');
    protectionService = ps.protectionService;
    PROTECTIVE_ACTION = mt.PROTECTIVE_ACTION;
  });

  // ------------------------------------------------------------------
  // 1. PROTECTIVE_ACTION ordering — canonical scale (ASPS-624)
  // ------------------------------------------------------------------
  describe('PROTECTIVE_ACTION constant ordering', () => {
    test('NONE has the lowest severity value', () => {
      expect(PROTECTIVE_ACTION.NONE).toBeLessThan(PROTECTIVE_ACTION.NOTIFY);
    });

    test('NOTIFY < WARN_BANNER', () => {
      expect(PROTECTIVE_ACTION.NOTIFY).toBeLessThan(PROTECTIVE_ACTION.WARN_BANNER);
    });

    test('WARN_BANNER < WARN_MODAL', () => {
      expect(PROTECTIVE_ACTION.WARN_BANNER).toBeLessThan(PROTECTIVE_ACTION.WARN_MODAL);
    });

    test('WARN_MODAL < BLOCK', () => {
      expect(PROTECTIVE_ACTION.WARN_MODAL).toBeLessThan(PROTECTIVE_ACTION.BLOCK);
    });

    test('BLOCK has the highest severity among standard actions', () => {
      const standardActions = [
        PROTECTIVE_ACTION.NONE,
        PROTECTIVE_ACTION.NOTIFY,
        PROTECTIVE_ACTION.WARN_BANNER,
        PROTECTIVE_ACTION.WARN_MODAL,
        PROTECTIVE_ACTION.BLOCK,
      ];
      const max = Math.max(...standardActions);
      expect(PROTECTIVE_ACTION.BLOCK).toBe(max);
    });

    test('exact values match documented contract (NONE=0 NOTIFY=1 WARN_BANNER=2 WARN_MODAL=3 BLOCK=4)', () => {
      expect(PROTECTIVE_ACTION.NONE).toBe(0);
      expect(PROTECTIVE_ACTION.NOTIFY).toBe(1);
      expect(PROTECTIVE_ACTION.WARN_BANNER).toBe(2);
      expect(PROTECTIVE_ACTION.WARN_MODAL).toBe(3);
      expect(PROTECTIVE_ACTION.BLOCK).toBe(4);
    });
  });

  // ------------------------------------------------------------------
  // 2. determineAction() must NOT exist (ASPS-624 removal)
  // ------------------------------------------------------------------
  describe('determineAction removed', () => {
    test('protectionService has no determineAction method', () => {
      expect(typeof protectionService.determineAction).toBe('undefined');
    });

    test('ProtectionService prototype has no determineAction', () => {
      expect(Object.prototype.hasOwnProperty.call(
        Object.getPrototypeOf(protectionService),
        'determineAction'
      )).toBe(false);
    });
  });

  // ------------------------------------------------------------------
  // 3. executeAction() dispatches correctly
  // ------------------------------------------------------------------
  describe('executeAction dispatch', () => {
    const TAB_ID = 42;

    test('WARN_BANNER sends a showWarning message', async () => {
      await protectionService.executeAction(PROTECTIVE_ACTION.WARN_BANNER, 1, 65, TAB_ID);
      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        TAB_ID,
        expect.objectContaining({ type: 'warning:show' })
      );
    });

    test('WARN_MODAL sends a showWarning message', async () => {
      await protectionService.executeAction(PROTECTIVE_ACTION.WARN_MODAL, 1, 75, TAB_ID);
      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        TAB_ID,
        expect.objectContaining({ type: 'warning:show' })
      );
    });

    test('BLOCK sends a blockPage message', async () => {
      await protectionService.executeAction(PROTECTIVE_ACTION.BLOCK, 1, 90, TAB_ID);
      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        TAB_ID,
        expect.objectContaining({ type: 'warning:block' })
      );
    });

    test('NONE does not send any tab message', async () => {
      chrome.tabs.sendMessage.mockClear();
      await protectionService.executeAction(PROTECTIVE_ACTION.NONE, 0, 5, TAB_ID);
      expect(chrome.tabs.sendMessage).not.toHaveBeenCalled();
    });

    test('NOTIFY creates a browser notification', async () => {
      chrome.notifications.create.mockImplementation((_opts, cb) => { if (cb) cb('id'); });
      await protectionService.executeAction(PROTECTIVE_ACTION.NOTIFY, 1, 45, TAB_ID);
      expect(chrome.notifications.create).toHaveBeenCalled();
    });
  });

  // ------------------------------------------------------------------
  // 4. Utility helpers
  // ------------------------------------------------------------------
  describe('getRiskLabel', () => {
    test('returns Phishing for type 1', () => {
      expect(protectionService.getRiskLabel(1)).toBe('Phishing');
    });

    test('returns Unknown for unrecognised type', () => {
      expect(protectionService.getRiskLabel(999)).toBe('Unknown');
    });

    test('returns Safe for type 0', () => {
      expect(protectionService.getRiskLabel(0)).toBe('Safe');
    });
  });

  describe('getActionName', () => {
    test('returns None for PROTECTIVE_ACTION.NONE', () => {
      expect(protectionService.getActionName(PROTECTIVE_ACTION.NONE)).toBe('None');
    });

    test('returns Block Page for PROTECTIVE_ACTION.BLOCK', () => {
      expect(protectionService.getActionName(PROTECTIVE_ACTION.BLOCK)).toBe('Block Page');
    });

    test('returns Warning Banner for PROTECTIVE_ACTION.WARN_BANNER', () => {
      expect(protectionService.getActionName(PROTECTIVE_ACTION.WARN_BANNER)).toBe('Warning Banner');
    });

    test('returns Unknown for an unrecognised action value', () => {
      expect(protectionService.getActionName(99)).toBe('Unknown');
    });
  });
});

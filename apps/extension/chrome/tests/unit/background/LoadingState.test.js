import { describe, test, expect, beforeEach, jest } from '@jest/globals';

// ── ASPS-734: startLoadingState() must clear stateManager scan state ────────
//
// Bug: startLoadingState() in background.js cleared currentPageScore/etc in
// chrome.storage.local but did NOT clear stateManager's in-memory 'scan.score'
// (and related keys). The popup's StatusService reads via buildStatusResponse()
// which pulls from stateManager, not storage — so navigating to a new page
// showed the stale score from the previous scan until "Scan Page" was clicked
// (scanCurrentTab() explicitly resets stateManager.set('scan.score', null)).
//
// Test strategy mirrors TabStateReporting.test.js / DangerStateService.test.js:
// the function body is extracted verbatim from background.js and exercised
// against mock iconService / stateManager / chrome.storage.local collaborators.
// Keep this extraction in sync with background.js if that function changes —
// this test IS the contract for startLoadingState().

// ── startLoadingState (verbatim from background.js, post ASPS-734 fix) ──────
function startLoadingState(iconService, stateManager) {
  iconService.startLoadingAnimation();
  stateManager.set('scan.score', null);
  stateManager.set('scan.riskType', null);
  stateManager.set('scan.protectiveAction', null);
  chrome.storage.local.set({
    currentPageScanning: true,
    currentPageScore: null,
    currentPageRiskType: [],
    currentPageAction: 0
  });
}

describe('startLoadingState — ASPS-734 stale score on navigation', () => {
  let mockIconService;
  let mockStateManager;

  beforeEach(() => {
    mockIconService = {
      startLoadingAnimation: jest.fn(),
    };
    mockStateManager = {
      set: jest.fn(),
    };
    chrome.storage.local.set.mockImplementation(() => Promise.resolve());
  });

  test('clears stateManager scan.score so the popup does not show a stale score', () => {
    startLoadingState(mockIconService, mockStateManager);
    expect(mockStateManager.set).toHaveBeenCalledWith('scan.score', null);
  });

  test('clears stateManager scan.riskType', () => {
    startLoadingState(mockIconService, mockStateManager);
    expect(mockStateManager.set).toHaveBeenCalledWith('scan.riskType', null);
  });

  test('clears stateManager scan.protectiveAction', () => {
    startLoadingState(mockIconService, mockStateManager);
    expect(mockStateManager.set).toHaveBeenCalledWith('scan.protectiveAction', null);
  });

  test('still clears chrome.storage.local currentPage* keys (unchanged behavior)', () => {
    startLoadingState(mockIconService, mockStateManager);
    expect(chrome.storage.local.set).toHaveBeenCalledWith(
      expect.objectContaining({
        currentPageScanning: true,
        currentPageScore: null,
        currentPageRiskType: [],
        currentPageAction: 0
      })
    );
  });

  test('still starts the loading icon animation (unchanged behavior)', () => {
    startLoadingState(mockIconService, mockStateManager);
    expect(mockIconService.startLoadingAnimation).toHaveBeenCalledTimes(1);
  });
});

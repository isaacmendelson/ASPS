import { describe, test, expect, beforeEach, jest } from '@jest/globals';

// ── ASPS-750: chrome.tabs.onActivated must sync stateManager, not just storage ──
//
// Bug (reverse split-brain of ASPS-734): the onActivated handler in
// background.js wrote the activated tab's score/riskType/action to
// chrome.storage.local (tab_*, currentPage*) but never touched
// stateManager. buildStatusResponse() (read by the popup's StatusService)
// pulls from stateManager, not storage. popup.js triggerReconnectIfNeeded()
// re-runs StatusService.update() ~1s after popup open whenever the desktop
// agent is disconnected — that re-run served the PREVIOUS tab's stale
// stateManager score, overwriting the correct storage-derived value that
// PageInfoService.update() had just set.
//
// Test strategy mirrors LoadingState.test.js / TabStateReporting.test.js:
// the handler body is extracted verbatim from background.js and exercised
// against a mocked chrome.tabs/chrome.storage.local (jest-chrome) plus
// injected cacheService/stateManager/stopLoadingState collaborators.

// ── handleTabActivated (verbatim from background.js, post ASPS-750 fix) ─────
async function handleTabActivated(activeInfo, cacheService, stateManager, stopLoadingState) {
  try {
    const tabId = activeInfo.tabId;
    const tab   = await chrome.tabs.get(tabId);

    const data = await chrome.storage.local.get([
      `tab_${tabId}_score`,
      `tab_${tabId}_riskType`,
      `tab_${tabId}_action`
    ]);

    if (data[`tab_${tabId}_score`] != null) {
      stopLoadingState();
      const score     = data[`tab_${tabId}_score`];
      const riskType  = data[`tab_${tabId}_riskType`] || [];
      const action    = data[`tab_${tabId}_action`]   || 0;
      stateManager.update({
        'scan.score':            score,
        'scan.riskType':         riskType,
        'scan.protectiveAction': action
      });
      chrome.storage.local.set({
        currentPageScore:      score,
        currentPageRiskType:   riskType,
        currentPageAction:     action,
        currentPageScanning:   false
      });
    } else if (tab.url?.startsWith('http')) {
      const cached = cacheService.get(tab.url);
      if (cached?.score != null) {
        stopLoadingState();
        const riskType = cached.riskType || [];
        const action   = cached.protectiveAction || 0;
        stateManager.update({
          'scan.score':            cached.score,
          'scan.riskType':         riskType,
          'scan.protectiveAction': action
        });
        chrome.storage.local.set({
          [`tab_${tabId}_score`]:    cached.score,
          [`tab_${tabId}_riskType`]: riskType,
          [`tab_${tabId}_action`]:   action,
          currentPageScore:          cached.score,
          currentPageRiskType:       riskType,
          currentPageAction:         action,
          currentPageScanning:       false
        });
      } else {
        stateManager.update({
          'scan.score':            null,
          'scan.riskType':         [],
          'scan.protectiveAction': 0
        });
        chrome.storage.local.set({
          currentPageScore:    null,
          currentPageRiskType: [],
          currentPageAction:   0,
          currentPageScanning: false
        });
      }
    } else {
      // ASPS-750 code-review fix: non-http tab with no stored per-tab
      // score (e.g. chrome://newtab/) — clear stateManager + currentPage*
      // instead of leaving the previously activated tab's stale value.
      stateManager.update({
        'scan.score':            null,
        'scan.riskType':         [],
        'scan.protectiveAction': 0
      });
      chrome.storage.local.set({
        currentPageScore:    null,
        currentPageRiskType: [],
        currentPageAction:   0,
        currentPageScanning: false
      });
    }
  } catch (e) {
    console.error('[Background] Tab activation error:', e);
  }
}

describe('chrome.tabs.onActivated — ASPS-750 stale score on tab switch', () => {
  let mockCacheService;
  let mockStateManager;
  let mockStopLoadingState;

  beforeEach(() => {
    mockCacheService = { get: jest.fn() };
    mockStateManager = { update: jest.fn() };
    mockStopLoadingState = jest.fn();
    chrome.storage.local.get.mockImplementation(() => Promise.resolve({}));
    chrome.storage.local.set.mockImplementation(() => Promise.resolve());
    chrome.tabs.get.mockImplementation(() => Promise.resolve({ url: 'https://example.com' }));
  });

  test('per-tab stored score → syncs stateManager.scan.* to the activated tab, not the previous tab', async () => {
    chrome.storage.local.get.mockImplementation(() => Promise.resolve({
      tab_7_score: 42,
      tab_7_riskType: ['phishing'],
      tab_7_action: 1
    }));

    await handleTabActivated({ tabId: 7 }, mockCacheService, mockStateManager, mockStopLoadingState);

    expect(mockStateManager.update).toHaveBeenCalledWith(expect.objectContaining({
      'scan.score': 42,
      'scan.riskType': ['phishing'],
      'scan.protectiveAction': 1
    }));
  });

  test('cache-hit branch → also syncs stateManager.scan.* (not just chrome.storage.local)', async () => {
    mockCacheService.get.mockReturnValue({ score: 17, riskType: ['malware'], protectiveAction: 2 });

    await handleTabActivated({ tabId: 9 }, mockCacheService, mockStateManager, mockStopLoadingState);

    expect(mockStateManager.update).toHaveBeenCalledWith(expect.objectContaining({
      'scan.score': 17,
      'scan.riskType': ['malware'],
      'scan.protectiveAction': 2
    }));
  });

  test('no cached data for the activated tab → clears stateManager.scan.score instead of leaving the previous tab\'s value', async () => {
    mockCacheService.get.mockReturnValue(undefined);

    await handleTabActivated({ tabId: 11 }, mockCacheService, mockStateManager, mockStopLoadingState);

    expect(mockStateManager.update).toHaveBeenCalledWith(expect.objectContaining({
      'scan.score': null
    }));
  });

  test('per-tab score path still writes chrome.storage.local currentPage* (unchanged behavior)', async () => {
    chrome.storage.local.get.mockImplementation(() => Promise.resolve({
      tab_7_score: 42,
      tab_7_riskType: ['phishing'],
      tab_7_action: 1
    }));

    await handleTabActivated({ tabId: 7 }, mockCacheService, mockStateManager, mockStopLoadingState);

    expect(chrome.storage.local.set).toHaveBeenCalledWith(expect.objectContaining({
      currentPageScore: 42,
      currentPageRiskType: ['phishing'],
      currentPageAction: 1,
      currentPageScanning: false
    }));
    expect(mockStopLoadingState).toHaveBeenCalledTimes(1);
  });

  // ── ASPS-750 code-review Major: missing final `else` for non-http tabs ──
  //
  // Activating a tab with no `tab_<id>_score` entry AND a non-http URL
  // (chrome://newtab/, chrome://settings, the New Tab Page, etc.) fell
  // through both branches — neither the per-tab-score branch nor the
  // `tab.url?.startsWith('http')` branch matched — so nothing ran and
  // stateManager kept whatever the PREVIOUSLY activated tab had left in
  // scan.score/riskType/protectiveAction. That's the exact reverse
  // split-brain ASPS-750 exists to fix, just in the non-http edge case.
  test('non-http tab with no stored score → clears stateManager and currentPage* instead of leaving the previous tab\'s stale score', async () => {
    // Previous tab (id 7) had a real score synced into stateManager.
    chrome.storage.local.get.mockImplementation(() => Promise.resolve({
      tab_7_score: 42,
      tab_7_riskType: ['phishing'],
      tab_7_action: 1
    }));
    await handleTabActivated({ tabId: 7 }, mockCacheService, mockStateManager, mockStopLoadingState);
    expect(mockStateManager.update).toHaveBeenLastCalledWith(expect.objectContaining({
      'scan.score': 42
    }));

    // Now activate a non-http tab (e.g. chrome://newtab/) with no stored score.
    mockStateManager.update.mockClear();
    chrome.storage.local.set.mockClear();
    chrome.storage.local.get.mockImplementation(() => Promise.resolve({}));
    chrome.tabs.get.mockImplementation(() => Promise.resolve({ url: 'chrome://newtab/' }));

    await handleTabActivated({ tabId: 12 }, mockCacheService, mockStateManager, mockStopLoadingState);

    expect(mockStateManager.update).toHaveBeenCalledWith({
      'scan.score':            null,
      'scan.riskType':         [],
      'scan.protectiveAction': 0
    });
    expect(chrome.storage.local.set).toHaveBeenCalledWith({
      currentPageScore:    null,
      currentPageRiskType: [],
      currentPageAction:   0,
      currentPageScanning: false
    });
  });
});

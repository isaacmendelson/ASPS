import { describe, test, expect, beforeEach, afterEach, jest } from '@jest/globals';

// ============================================================================
// IconService unit tests — ASPS-758 rewrite (last of the ASPS-738 quarantine
// cleanup; after this the `extension` array in
// scripts/test-baseline-exceptions.json is empty).
//
// Root cause of the original quarantine (confirmed by inspection): every one
// of the previous 29 tests crashed inside `beforeEach` before a single
// assertion ran. jest-chrome 0.8.0 predates MV3's `chrome.action` namespace
// entirely (it only ships the MV2 `chrome.browserAction`), so
// `chrome.action.setIcon.mockImplementation(...)` threw
// "Cannot read properties of undefined (reading 'setIcon')" immediately.
// IconService (services/IconService.js) is a direct chrome.action consumer
// (setIcon / setBadgeText / setBadgeBackgroundColor — it never calls
// chrome.action.setTitle or any chrome.tabs API; icon state is a single
// global, not per-tab), so a faithful test-local `chrome.action` double is
// required for every test in this file. Scoped to this file only — this is
// the exact same polyfill pattern already used in
// tests/integration/extension-flow.test.js (ASPS-755) and does not touch
// tests/setup/jest.setup.cjs, so it cannot destabilize any other suite.
//
// Two real (current) behaviors this rewrite deliberately asserts as-is,
// rather than smoothing over as "should be X" — flagged here and in the
// ASPS-758 handoff per the "no silent side-fixes" rule, NOT fixed (this task
// is test-only, no production changes):
//
//   1. setColor()'s "skip redraw for the same color" guard
//      (`if (this.currentColor === color) return;`) is unreachable in
//      practice. setColor() unconditionally calls stopLoadingAnimation()
//      first, and stopLoadingAnimation() unconditionally sets
//      `this.currentColor = null` at the end — even when no loading
//      animation was ever running. So by the time the guard runs,
//      currentColor has just been reset to null, and the check almost never
//      short-circuits. Net effect: calling setColor('green') twice in a row
//      redraws (and re-persists to storage) both times. See "setColor()"
//      describe block below.
//   2. IconService only ever subscribes to `connection.desktop` on
//      StateManager. The `scan.score` subscription is explicitly commented
//      out in the source (background.js's setColorByAction() owns
//      score-based icon updates instead, to avoid a race with
//      protectiveAction) — see "Construction / state subscription contract".
// ============================================================================

const stateManager = {
  subscribe: jest.fn(),
  get: jest.fn(),
  update: jest.fn()
};

jest.unstable_mockModule('../../../state/StateManager.js', () => ({
  default: stateManager
}));

// jest-chrome 0.8.0 has no chrome.action namespace at all (see file header).
if (!chrome.action) {
  chrome.action = {
    setIcon: jest.fn(),
    setBadgeText: jest.fn(),
    setBadgeBackgroundColor: jest.fn()
  };
}

const { iconService } = await import('../../../services/IconService.js');

// The constructor subscribes to StateManager exactly once, at the top-level
// `await import(...)` above — before any beforeEach (and its
// jest.clearAllMocks()) has run. Capture that call list now, or it would be
// wiped before the first test even starts.
const subscribeCallsAtConstruction = [...stateManager.subscribe.mock.calls];
const connectionDesktopCallback = subscribeCallsAtConstruction.find(
  ([key]) => key === 'connection.desktop'
)?.[1];

function makeOffscreenCanvasMock() {
  return jest.fn(() => ({
    getContext: jest.fn(() => ({
      clearRect: jest.fn(),
      beginPath: jest.fn(),
      moveTo: jest.fn(),
      lineTo: jest.fn(),
      quadraticCurveTo: jest.fn(),
      closePath: jest.fn(),
      fill: jest.fn(),
      stroke: jest.fn(),
      fillText: jest.fn(),
      getImageData: jest.fn(() => ({}))
    }))
  }));
}

describe('IconService', () => {
  beforeEach(() => {
    jest.clearAllMocks();

    chrome.action.setIcon.mockImplementation(() => Promise.resolve());
    chrome.action.setBadgeText.mockImplementation(() => Promise.resolve());
    chrome.action.setBadgeBackgroundColor.mockImplementation(() => Promise.resolve());
    chrome.storage.local.set.mockImplementation(() => Promise.resolve());

    global.OffscreenCanvas = makeOffscreenCanvasMock();

    // IconService is a singleton (default export instantiated once at
    // module load). Reset its mutable fields between tests instead of
    // re-importing — matches the CacheService/ConnectionService rewrite
    // pattern (ASPS-756/757).
    iconService.currentColor = null;
    iconService.isLoading = false;
    iconService.animationInterval = null;
    iconService.frame = 0;
    iconService.loadingBadgeTimeout = null;
  });

  afterEach(() => {
    if (iconService.animationInterval) clearInterval(iconService.animationInterval);
    if (iconService.loadingBadgeTimeout) clearTimeout(iconService.loadingBadgeTimeout);
    jest.useRealTimers();
  });

  describe('Construction / state subscription contract', () => {
    test('subscribes exactly once to connection.desktop with a callback', () => {
      const connectionSubs = subscribeCallsAtConstruction.filter(([key]) => key === 'connection.desktop');
      expect(connectionSubs).toHaveLength(1);
      expect(connectionSubs[0][1]).toEqual(expect.any(Function));
    });

    test('does not subscribe to scan.score (owned by background.js setColorByAction instead)', () => {
      const scoreSubs = subscribeCallsAtConstruction.filter(([key]) => key === 'scan.score');
      expect(scoreSubs).toHaveLength(0);
    });

    test('connection.desktop callback sets the icon green when connected', () => {
      connectionDesktopCallback(true);
      expect(iconService.getColor()).toBe('green');
    });

    test('connection.desktop callback sets the icon gray when disconnected', () => {
      connectionDesktopCallback(false);
      expect(iconService.getColor()).toBe('gray');
    });
  });

  describe('setColor()', () => {
    test.each(['green', 'yellow', 'red', 'gray'])('sets the icon to %s and draws via chrome.action.setIcon', (color) => {
      iconService.setColor(color);

      expect(iconService.getColor()).toBe(color);
      expect(chrome.action.setIcon).toHaveBeenCalledWith(expect.objectContaining({ imageData: expect.anything() }));
    });

    test('falls back to the gray fill color for an unrecognized color name, but still stores the given name', () => {
      iconService.setColor('purple');

      // colors['purple'] is undefined -> this.colors.gray is used as the fill,
      // but currentColor itself is whatever was passed in (no validation).
      expect(iconService.getColor()).toBe('purple');
      expect(chrome.action.setIcon).toHaveBeenCalled();
      expect(chrome.storage.local.set).toHaveBeenCalledWith({ iconColor: 'purple' });
    });

    test('persists {iconColor} to chrome.storage.local on every successful call', () => {
      iconService.setColor('red');
      expect(chrome.storage.local.set).toHaveBeenCalledWith({ iconColor: 'red' });
    });

    test('always clears the badge text first, via the internal stopLoadingAnimation() call', () => {
      iconService.setColor('green');
      expect(chrome.action.setBadgeText).toHaveBeenCalledWith({ text: '' });
    });

    test('REAL BEHAVIOR: calling setColor with the same color twice still redraws both times', () => {
      // See file header note (1): stopLoadingAnimation() unconditionally
      // resets currentColor to null before the "same color" check runs, so
      // the check never short-circuits in current code.
      iconService.setColor('green');
      chrome.action.setIcon.mockClear();
      chrome.storage.local.set.mockClear();

      iconService.setColor('green');

      expect(iconService.getColor()).toBe('green');
      expect(chrome.action.setIcon).toHaveBeenCalledTimes(1);
      expect(chrome.storage.local.set).toHaveBeenCalledWith({ iconColor: 'green' });
    });
  });

  describe('setColor() error handling', () => {
    test('a synchronous chrome.action.setIcon throw does not escape setColor(), but currentColor is still updated', () => {
      chrome.action.setIcon.mockImplementation(() => {
        throw new Error('Icon error');
      });

      expect(() => iconService.setColor('green')).not.toThrow();
      // currentColor is assigned before the try/catch that wraps the draw,
      // so it reflects the requested color even though the draw failed.
      expect(iconService.getColor()).toBe('green');
      // storage.set is after setIcon inside the same try block, so it is
      // never reached once setIcon throws.
      expect(chrome.storage.local.set).not.toHaveBeenCalled();
    });

    test('an OffscreenCanvas construction throw does not escape setColor(), but currentColor is still updated', () => {
      global.OffscreenCanvas = jest.fn(() => {
        throw new Error('Canvas error');
      });

      expect(() => iconService.setColor('red')).not.toThrow();
      expect(iconService.getColor()).toBe('red');
      expect(chrome.action.setIcon).not.toHaveBeenCalled();
    });
  });

  describe('setColorByScore() — legacy score-based color', () => {
    test('sets green for low risk (score < 31)', () => {
      iconService.setColorByScore(20);
      expect(iconService.getColor()).toBe('green');
    });

    test('sets yellow for medium risk (31 <= score < 61)', () => {
      iconService.setColorByScore(45);
      expect(iconService.getColor()).toBe('yellow');
    });

    test('sets red for high risk (score >= 61)', () => {
      iconService.setColorByScore(75);
      expect(iconService.getColor()).toBe('red');
    });

    test('boundary: score 31 is yellow (medium risk starts here)', () => {
      iconService.setColorByScore(31);
      expect(iconService.getColor()).toBe('yellow');
    });

    test('boundary: score 61 is red (high risk starts here)', () => {
      iconService.setColorByScore(61);
      expect(iconService.getColor()).toBe('red');
    });
  });

  describe('setColorByAction() — protectiveAction with score fallback', () => {
    test('protectiveAction 4 (Block) -> red', () => {
      iconService.setColorByAction(4);
      expect(iconService.getColor()).toBe('red');
    });

    test('protectiveAction 2 (WarnBanner) -> yellow', () => {
      iconService.setColorByAction(2);
      expect(iconService.getColor()).toBe('yellow');
    });

    test('protectiveAction 3 (WarnModal) -> yellow', () => {
      iconService.setColorByAction(3);
      expect(iconService.getColor()).toBe('yellow');
    });

    test('protectiveAction 1 (Notify) falls back to score: 70 -> red', () => {
      iconService.setColorByAction(1, 70);
      expect(iconService.getColor()).toBe('red');
    });

    test('protectiveAction 0 (None) falls back to score: 20 -> green', () => {
      iconService.setColorByAction(0, 20);
      expect(iconService.getColor()).toBe('green');
    });

    test('protectiveAction 0 with no score provided defaults to green', () => {
      iconService.setColorByAction(0);
      expect(iconService.getColor()).toBe('green');
    });

    test('score-fallback boundary: protectiveAction 1, score 31 -> yellow', () => {
      iconService.setColorByAction(1, 31);
      expect(iconService.getColor()).toBe('yellow');
    });

    test('score-fallback boundary: protectiveAction 1, score 61 -> red', () => {
      iconService.setColorByAction(1, 61);
      expect(iconService.getColor()).toBe('red');
    });
  });

  describe('Loading animation', () => {
    test('startLoadingAnimation() sets isLoading, creates an interval, and resets the frame counter', () => {
      jest.useFakeTimers();

      iconService.frame = 7;
      iconService.startLoadingAnimation();

      expect(iconService.isLoading).toBe(true);
      expect(iconService.animationInterval).toBeDefined();
      expect(iconService.animationInterval).not.toBeNull();
      expect(iconService.frame).toBe(0);
    });

    test('draws a frame via chrome.action.setIcon roughly every 50ms while animating', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      jest.advanceTimersByTime(150); // ~3 ticks

      expect(chrome.action.setIcon).toHaveBeenCalledTimes(3);
      expect(iconService.frame).toBe(3);
    });

    test('shows the loading badge (spinner + gray background) after a 500ms delay', async () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      await jest.advanceTimersByTimeAsync(500);

      expect(chrome.action.setBadgeText).toHaveBeenCalledWith(
        expect.objectContaining({ text: '↻' })
      );
      expect(chrome.action.setBadgeBackgroundColor).toHaveBeenCalledWith(
        expect.objectContaining({ color: '#9E9E9E' })
      );
    });

    test('suppresses the loading badge if stopLoadingAnimation() runs before the 500ms delay elapses', async () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      jest.advanceTimersByTime(200);
      iconService.stopLoadingAnimation();
      chrome.action.setBadgeText.mockClear();

      await jest.advanceTimersByTimeAsync(1000);

      expect(chrome.action.setBadgeText).not.toHaveBeenCalledWith(
        expect.objectContaining({ text: '↻' })
      );
    });

    test('stopLoadingAnimation() clears isLoading/interval/timeout, clears the badge, resets currentColor, and stops further frame draws', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      jest.advanceTimersByTime(100); // a couple of frames drawn
      chrome.action.setIcon.mockClear();

      iconService.stopLoadingAnimation();

      expect(iconService.isLoading).toBe(false);
      expect(iconService.animationInterval).toBeNull();
      expect(iconService.loadingBadgeTimeout).toBeNull();
      expect(iconService.currentColor).toBeNull();
      expect(chrome.action.setBadgeText).toHaveBeenCalledWith({ text: '' });

      // Interval was really cleared -- no more draws even if time advances.
      jest.advanceTimersByTime(200);
      expect(chrome.action.setIcon).not.toHaveBeenCalled();
    });

    test('starting the animation again while already animating does not create a new interval', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      const firstInterval = iconService.animationInterval;

      iconService.startLoadingAnimation();
      const secondInterval = iconService.animationInterval;

      expect(firstInterval).toBe(secondInterval);
    });

    test('drawLoadingFrame errors (OffscreenCanvas throws) are caught, do not throw, and the frame counter does not advance', () => {
      jest.useFakeTimers();
      global.OffscreenCanvas = jest.fn(() => {
        throw new Error('Canvas error');
      });

      expect(() => iconService.startLoadingAnimation()).not.toThrow();
      expect(() => jest.advanceTimersByTime(100)).not.toThrow();

      // drawLoadingFrame's frame++ is after the throwing canvas construction,
      // inside the same try block, so it is never reached.
      expect(iconService.frame).toBe(0);
    });
  });

  describe('update()', () => {
    test('sets gray when not connected (does not read scan.score)', () => {
      stateManager.get.mockReturnValueOnce(false); // connection.desktop

      iconService.update();

      expect(stateManager.get).toHaveBeenCalledWith('connection.desktop');
      expect(iconService.getColor()).toBe('gray');
    });

    test('uses the score-based color when connected and a score is available', () => {
      stateManager.get
        .mockReturnValueOnce(true)  // connection.desktop
        .mockReturnValueOnce(65);   // scan.score

      iconService.update();

      expect(stateManager.get).toHaveBeenCalledWith('scan.score');
      expect(iconService.getColor()).toBe('red');
    });

    test('defaults to green when connected but no score is available yet', () => {
      stateManager.get
        .mockReturnValueOnce(true)  // connection.desktop
        .mockReturnValueOnce(null); // scan.score

      iconService.update();

      expect(iconService.getColor()).toBe('green');
    });
  });

  describe('getColor()', () => {
    test('returns null before any color has been set', () => {
      expect(iconService.getColor()).toBeNull();
    });
  });
});

import { describe, test, expect, beforeEach, afterEach, jest } from '@jest/globals';

describe('IconService', () => {
  let iconService;
  let mockStateManager;

  beforeEach(async () => {
    // Mock StateManager
    mockStateManager = {
      subscribe: jest.fn(),
      get: jest.fn(),
      update: jest.fn()
    };

    // Mock chrome APIs
    chrome.action.setIcon.mockImplementation((details, callback) => {
      if (callback) callback();
    });
    chrome.action.setBadgeText.mockImplementation((details, callback) => {
      if (callback) callback();
    });
    chrome.action.setBadgeBackgroundColor.mockImplementation((details, callback) => {
      if (callback) callback();
    });
    chrome.storage.local.set.mockImplementation((data, callback) => {
      if (callback) callback();
    });

    // Mock OffscreenCanvas
    global.OffscreenCanvas = jest.fn(() => ({
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

    // Mock modules
    jest.unstable_mockModule('@/state/StateManager.js', () => ({
      default: mockStateManager
    }));

    // Import after mocks are set up
    const module = await import('@/services/IconService.js');
    iconService = module.iconService;
  });

  afterEach(() => {
    // Clear any intervals/timeouts
    if (iconService.animationInterval) {
      clearInterval(iconService.animationInterval);
    }
    if (iconService.loadingBadgeTimeout) {
      clearTimeout(iconService.loadingBadgeTimeout);
    }
  });

  describe('Icon Color Management', () => {
    test('should set icon to green', () => {
      iconService.setColor('green');

      expect(iconService.getColor()).toBe('green');
      expect(chrome.action.setIcon).toHaveBeenCalled();
      expect(chrome.storage.local.set).toHaveBeenCalledWith(
        { iconColor: 'green' },
        undefined
      );
    });

    test('should set icon to yellow', () => {
      iconService.setColor('yellow');

      expect(iconService.getColor()).toBe('yellow');
    });

    test('should set icon to red', () => {
      iconService.setColor('red');

      expect(iconService.getColor()).toBe('red');
    });

    test('should set icon to gray', () => {
      iconService.setColor('gray');

      expect(iconService.getColor()).toBe('gray');
    });

    test('should not update if color is the same', () => {
      iconService.setColor('green');
      chrome.action.setIcon.mockClear();

      iconService.setColor('green');

      expect(chrome.action.setIcon).not.toHaveBeenCalled();
    });
  });

  describe('Score-Based Color', () => {
    test('should set green for low risk (score < 31)', () => {
      iconService.setColorByScore(20);

      expect(iconService.getColor()).toBe('green');
    });

    test('should set yellow for medium risk (31 <= score < 61)', () => {
      iconService.setColorByScore(45);

      expect(iconService.getColor()).toBe('yellow');
    });

    test('should set red for high risk (score >= 61)', () => {
      iconService.setColorByScore(75);

      expect(iconService.getColor()).toBe('red');
    });

    test('should handle edge case score 31', () => {
      iconService.setColorByScore(31);

      expect(iconService.getColor()).toBe('yellow');
    });

    test('should handle edge case score 61', () => {
      iconService.setColorByScore(61);

      expect(iconService.getColor()).toBe('red');
    });
  });

  describe('Action-Based Color', () => {
    test('should set red for block action (4)', () => {
      iconService.setColorByAction(4);

      expect(iconService.getColor()).toBe('red');
    });

    test('should set yellow for warning actions (2-3)', () => {
      iconService.setColorByAction(2);
      expect(iconService.getColor()).toBe('yellow');

      iconService.setColorByAction(3);
      expect(iconService.getColor()).toBe('yellow');
    });

    test('should fallback to score for notify action (1)', () => {
      iconService.setColorByAction(1, 70);

      expect(iconService.getColor()).toBe('red');
    });

    test('should fallback to score for none action (0)', () => {
      iconService.setColorByAction(0, 20);

      expect(iconService.getColor()).toBe('green');
    });

    test('should default to green if no score provided with low action', () => {
      iconService.setColorByAction(0);

      expect(iconService.getColor()).toBe('green');
    });
  });

  describe('Loading Animation', () => {
    test('should start loading animation', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();

      expect(iconService.isLoading).toBe(true);
      expect(iconService.animationInterval).toBeDefined();

      jest.useRealTimers();
    });

    test('should show loading badge after delay', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();

      // Fast-forward 500ms
      jest.advanceTimersByTime(500);

      expect(chrome.action.setBadgeText).toHaveBeenCalledWith(
        expect.objectContaining({ text: '\u21BB' })
      );

      jest.useRealTimers();
    });

    test('should stop loading animation', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      iconService.stopLoadingAnimation();

      expect(iconService.isLoading).toBe(false);
      expect(iconService.animationInterval).toBeNull();
      expect(chrome.action.setBadgeText).toHaveBeenCalledWith({ text: '' });

      jest.useRealTimers();
    });

    test('should not restart animation if already animating', () => {
      jest.useFakeTimers();

      iconService.startLoadingAnimation();
      const firstInterval = iconService.animationInterval;

      iconService.startLoadingAnimation();
      const secondInterval = iconService.animationInterval;

      expect(firstInterval).toBe(secondInterval);

      jest.useRealTimers();
    });

    test('should reset currentColor after loading', () => {
      jest.useFakeTimers();

      iconService.setColor('green');
      iconService.startLoadingAnimation();
      iconService.stopLoadingAnimation();

      expect(iconService.currentColor).toBeNull();

      jest.useRealTimers();
    });
  });

  describe('State Management Integration', () => {
    test('should subscribe to connection state changes', () => {
      expect(mockStateManager.subscribe).toHaveBeenCalledWith(
        'connection.desktop',
        expect.any(Function)
      );
    });

    test('should subscribe to scan score changes', () => {
      expect(mockStateManager.subscribe).toHaveBeenCalledWith(
        'scan.score',
        expect.any(Function)
      );
    });

    test('should update icon when connection state changes', () => {
      const connectionCallback = mockStateManager.subscribe.mock.calls.find(
        call => call[0] === 'connection.desktop'
      )?.[1];

      if (connectionCallback) {
        connectionCallback(true);
        expect(iconService.getColor()).toBe('green');

        connectionCallback(false);
        expect(iconService.getColor()).toBe('gray');
      }
    });

    test('should update icon when scan score changes', () => {
      mockStateManager.get.mockReturnValue(true); // Connected

      const scoreCallback = mockStateManager.subscribe.mock.calls.find(
        call => call[0] === 'scan.score'
      )?.[1];

      if (scoreCallback) {
        scoreCallback(75);
        expect(iconService.getColor()).toBe('red');

        scoreCallback(45);
        expect(iconService.getColor()).toBe('yellow');

        scoreCallback(20);
        expect(iconService.getColor()).toBe('green');
      }
    });
  });

  describe('Update Method', () => {
    test('should set gray when not connected', () => {
      mockStateManager.get.mockReturnValueOnce(false);

      iconService.update();

      expect(iconService.getColor()).toBe('gray');
    });

    test('should use score when connected and score available', () => {
      mockStateManager.get
        .mockReturnValueOnce(true)  // connected
        .mockReturnValueOnce(65);   // score

      iconService.update();

      expect(iconService.getColor()).toBe('red');
    });

    test('should default to green when connected but no score', () => {
      mockStateManager.get
        .mockReturnValueOnce(true)  // connected
        .mockReturnValueOnce(null); // score

      iconService.update();

      expect(iconService.getColor()).toBe('green');
    });
  });

  describe('Error Handling', () => {
    test('should handle setIcon errors gracefully', () => {
      chrome.action.setIcon.mockImplementation(() => {
        throw new Error('Icon error');
      });

      expect(() => iconService.setColor('green')).not.toThrow();
    });

    test('should handle drawLoadingFrame errors gracefully', () => {
      jest.useFakeTimers();

      global.OffscreenCanvas = jest.fn(() => {
        throw new Error('Canvas error');
      });

      expect(() => iconService.startLoadingAnimation()).not.toThrow();

      jest.useRealTimers();
    });
  });
});

import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('AuthService', () => {
  let authService;
  let mockStateManager;

  beforeEach(async () => {
    // Mock StateManager
    mockStateManager = {
      update: jest.fn(),
      get: jest.fn()
    };

    // Mock chrome APIs
    chrome.storage.local.get.mockImplementation((keys, callback) => {
      callback({});
    });

    // Mock modules
    jest.unstable_mockModule('@/state/StateManager.js', () => ({
      default: mockStateManager
    }));

    // Import after mocks are set up
    const module = await import('@/services/AuthService.js');
    authService = module.authService;
  });

  describe('Initialization', () => {
    test('should initialize with stored email', async () => {
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({ userEmail: 'test@example.com' });
      });

      const result = await authService.init();

      expect(result).toBe(true);
      expect(mockStateManager.update).toHaveBeenCalledWith({
        'user.loggedIn': true,
        'user.email': 'test@example.com'
      });
    });

    test('should return false when no email stored', async () => {
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({});
      });

      const result = await authService.init();

      expect(result).toBe(false);
      expect(mockStateManager.update).not.toHaveBeenCalled();
    });

    test('should handle initialization errors gracefully', async () => {
      chrome.storage.local.get.mockImplementation(() => {
        throw new Error('Storage error');
      });

      const result = await authService.init();

      expect(result).toBe(false);
    });
  });

  describe('Authentication Status', () => {
    test('should return true when user is signed in', () => {
      mockStateManager.get.mockReturnValue(true);

      const result = authService.isSignedIn();

      expect(result).toBe(true);
      expect(mockStateManager.get).toHaveBeenCalledWith('user.loggedIn');
    });

    test('should return false when user is not signed in', () => {
      mockStateManager.get.mockReturnValue(false);

      const result = authService.isSignedIn();

      expect(result).toBe(false);
    });

    test('should handle null state gracefully', () => {
      mockStateManager.get.mockReturnValue(null);

      const result = authService.isSignedIn();

      expect(result).toBe(false);
    });
  });

  describe('User Email Retrieval', () => {
    test('should return stored email', () => {
      const email = 'test@example.com';
      mockStateManager.get.mockReturnValue(email);

      const result = authService.getEmail();

      expect(result).toBe(email);
      expect(mockStateManager.get).toHaveBeenCalledWith('user.email');
    });

    test('should return null when no email set', () => {
      mockStateManager.get.mockReturnValue(null);

      const result = authService.getEmail();

      expect(result).toBeNull();
    });
  });

  describe('State Management Integration', () => {
    test('should update state manager on successful init', async () => {
      const testEmail = 'integration@test.com';
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({ userEmail: testEmail });
      });

      await authService.init();

      expect(mockStateManager.update).toHaveBeenCalledTimes(1);
      expect(mockStateManager.update).toHaveBeenCalledWith(
        expect.objectContaining({
          'user.loggedIn': true,
          'user.email': testEmail
        })
      );
    });

    test('should not update state when init fails', async () => {
      chrome.storage.local.get.mockImplementation((keys, callback) => {
        callback({});
      });

      await authService.init();

      expect(mockStateManager.update).not.toHaveBeenCalled();
    });
  });
});

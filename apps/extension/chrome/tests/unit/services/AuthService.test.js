import { describe, test, expect, beforeEach, jest } from '@jest/globals';

// NOTE: ASPS-753 fix. AuthService.init() calls
// `await chrome.storage.local.get(['userEmail'])` — the MV3 Promise-based
// signature (no callback argument). The original mocks here used the legacy
// callback-style `(keys, callback) => callback(data)` shape. Since AuthService
// never passes a callback, `callback` was `undefined` and calling it threw
// synchronously inside the mock; that throw was swallowed by init()'s own
// try/catch, so every test silently observed `init()` returning `false`
// regardless of what the mock intended to return. Fixed by mocking the
// Promise-based signature that current chrome.storage.local.get() actually
// uses.
//
// Also: this file re-mocks '@/state/StateManager.js' and dynamically
// re-imports '@/services/AuthService.js' inside every beforeEach, but without
// jest.resetModules() the ESM module registry keeps returning the cached
// module graph from the *first* import. That meant every test after the very
// first one exercised an AuthService singleton still wired to the original
// test's mockStateManager instance, not the fresh one just created — so
// mockStateManager.get.mockReturnValue(...) had no effect on it. Calling
// jest.resetModules() before each mock+import forces a fresh module graph
// (and a fresh AuthService singleton) per test, matching the actual per-test
// mock object.
describe('AuthService', () => {
  let authService;
  let mockStateManager;

  beforeEach(async () => {
    jest.resetModules();

    // Mock StateManager
    mockStateManager = {
      update: jest.fn(),
      get: jest.fn()
    };

    // Mock chrome APIs (Promise-based, matching current chrome.storage.local.get())
    chrome.storage.local.get.mockResolvedValue({});

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
      chrome.storage.local.get.mockResolvedValue({ userEmail: 'test@example.com' });

      const result = await authService.init();

      expect(result).toBe(true);
      expect(mockStateManager.update).toHaveBeenCalledWith({
        'user.loggedIn': true,
        'user.email': 'test@example.com'
      });
    });

    test('should return false when no email stored', async () => {
      chrome.storage.local.get.mockResolvedValue({});

      const result = await authService.init();

      expect(result).toBe(false);
      expect(mockStateManager.update).not.toHaveBeenCalled();
    });

    test('should handle initialization errors gracefully', async () => {
      chrome.storage.local.get.mockRejectedValue(new Error('Storage error'));

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
      chrome.storage.local.get.mockResolvedValue({ userEmail: testEmail });

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
      chrome.storage.local.get.mockResolvedValue({});

      await authService.init();

      expect(mockStateManager.update).not.toHaveBeenCalled();
    });
  });
});

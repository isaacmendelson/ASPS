// ============================================
// AntiScam Extension - Auth Service
// Handles email-only authentication via chrome.storage.local
// ============================================

import stateManager from '../state/StateManager.js';

class AuthService {
  // Initialize auth state from storage
  async init() {
    try {
      const data = await chrome.storage.local.get(['userEmail']);

      if (data.userEmail) {
        stateManager.update({
          'user.loggedIn': true,
          'user.email': data.userEmail
        });

        console.log('[AuthService] User loaded:', data.userEmail);
        return true;
      }

      return false;
    } catch (e) {
      console.error('[AuthService] Init error:', e);
      return false;
    }
  }

  // Check if user is signed in
  isSignedIn() {
    return stateManager.get('user.loggedIn') === true;
  }

  // Get current user email
  getEmail() {
    return stateManager.get('user.email');
  }
}

// Singleton instance
export const authService = new AuthService();
export default authService;

import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('ProtectionService', () => {
  let protectionService;

  beforeEach(async () => {
    // Mock chrome APIs
    chrome.tabs.sendMessage.mockImplementation((tabId, msg, callback) => {
      if (callback) callback({ success: true });
    });

    chrome.tabs.update.mockImplementation((tabId, props, callback) => {
      if (callback) callback({ id: tabId, url: props.url });
    });

    const module = await import('@/services/ProtectionService.js');
    protectionService = module.protectionService;
  });

  describe('Warning Display', () => {
    test('should show warning on page', async () => {
      const tabId = 123;
      const riskData = {
        score: 45,
        riskType: [1],
        riskLabels: ['Phishing']
      };

      await protectionService.showWarning(tabId, riskData);

      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: 'showWarning',
          data: expect.objectContaining({
            score: riskData.score,
            riskLabels: riskData.riskLabels
          })
        }),
        expect.any(Function)
      );
    });

    test('should remove warning from page', async () => {
      const tabId = 123;

      await protectionService.removeWarning(tabId);

      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: 'removeWarning'
        }),
        expect.any(Function)
      );
    });
  });

  describe('Page Blocking', () => {
    test('should block dangerous page', async () => {
      const tabId = 123;
      const riskData = {
        score: 20,
        riskType: [1, 2],
        riskLabels: ['Phishing', 'Cloaking'],
        originalUrl: 'https://malicious-site.com'
      };

      await protectionService.blockPage(tabId, riskData);

      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: 'blockPage',
          data: expect.objectContaining({
            score: riskData.score,
            riskLabels: riskData.riskLabels
          })
        }),
        expect.any(Function)
      );
    });

    test('should allow user to bypass warning', async () => {
      const tabId = 123;
      const originalUrl = 'https://flagged-site.com';

      await protectionService.bypassWarning(tabId, originalUrl);

      // Should remove the warning/block overlay
      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: 'removeWarning'
        }),
        expect.any(Function)
      );
    });
  });

  describe('Notification System', () => {
    test('should create browser notification for blocked page', async () => {
      chrome.notifications.create.mockImplementation((id, options, callback) => {
        if (callback) callback(id);
      });

      const riskData = {
        score: 15,
        riskType: [1],
        riskLabels: ['Phishing'],
        url: 'https://phishing-site.com'
      };

      await protectionService.notifyUser(riskData);

      expect(chrome.notifications.create).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          type: 'basic',
          iconUrl: expect.any(String),
          title: expect.stringContaining('blocked'),
          message: expect.stringContaining(riskData.url)
        }),
        expect.any(Function)
      );
    });

    test('should create browser notification for warning', async () => {
      chrome.notifications.create.mockImplementation((id, options, callback) => {
        if (callback) callback(id);
      });

      const riskData = {
        score: 60,
        riskType: [1],
        riskLabels: ['Phishing'],
        url: 'https://suspicious-site.com'
      };

      await protectionService.notifyUser(riskData);

      expect(chrome.notifications.create).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          type: 'basic',
          title: expect.stringContaining('Warning'),
          message: expect.stringContaining(riskData.url)
        }),
        expect.any(Function)
      );
    });
  });

  describe('Remote Access Protection', () => {
    test('should warn about remote access tools', async () => {
      const tabId = 123;
      const toolName = 'TeamViewer';

      await protectionService.showRemoteAccessWarning(tabId, toolName);

      expect(chrome.tabs.sendMessage).toHaveBeenCalledWith(
        tabId,
        expect.objectContaining({
          type: expect.stringContaining('remote_access'),
          data: expect.objectContaining({
            toolName
          })
        }),
        expect.any(Function)
      );
    });

    test('should handle remote access session closure', async () => {
      const sessionInfo = {
        toolName: 'AnyDesk',
        sessionId: 'abc123'
      };

      await protectionService.closeRemoteSession(sessionInfo);

      // Should notify backend about session closure
      // Implementation depends on actual service
      expect(chrome.runtime.sendMessage).toHaveBeenCalled();
    });
  });

  describe('User Feedback', () => {
    test('should submit correct prediction feedback', async () => {
      const feedbackData = {
        url: 'https://example.com',
        score: 95,
        userFeedback: 'correct'
      };

      await protectionService.submitFeedback(feedbackData);

      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          type: expect.stringContaining('feedback'),
          data: expect.objectContaining({
            url: feedbackData.url,
            feedback: 'correct'
          })
        })
      );
    });

    test('should submit incorrect prediction feedback', async () => {
      const feedbackData = {
        url: 'https://example.com',
        score: 45,
        userFeedback: 'incorrect'
      };

      await protectionService.submitFeedback(feedbackData);

      expect(chrome.runtime.sendMessage).toHaveBeenCalledWith(
        expect.objectContaining({
          type: expect.stringContaining('feedback'),
          data: expect.objectContaining({
            url: feedbackData.url,
            feedback: 'incorrect'
          })
        })
      );
    });
  });
});

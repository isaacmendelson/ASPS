import { describe, test, expect, beforeEach, jest } from '@jest/globals';

describe('MessageQueueService', () => {
  let messageQueueService;

  beforeEach(async () => {
    // Mock chrome APIs
    chrome.storage.session.set.mockImplementation((data, callback) => {
      if (callback) callback();
      return Promise.resolve();
    });
    chrome.storage.session.get.mockImplementation((keys, callback) => {
      if (callback) callback({});
      return Promise.resolve({});
    });
    chrome.storage.session.remove.mockImplementation((keys, callback) => {
      if (callback) callback();
      return Promise.resolve();
    });

    // Import module
    const module = await import('@/services/MessageQueueService.js');
    messageQueueService = module.messageQueueService;
    
    // Clear queue and reset defaults before each test
    messageQueueService.clear();
    messageQueueService.maxSize = 100;
  });

  describe('Enqueue Operations', () => {
    test('should enqueue a regular message', () => {
      const message = { type: 'regular_message', data: 'test' };

      const result = messageQueueService.enqueue(message);

      expect(result).toBe(true);
      expect(messageQueueService.size).toBe(1);
    });

    test('should enqueue priority messages first', () => {
      const regularMsg = { type: 'regular', data: 'test' };
      const priorityMsg = { type: 'url_result', data: 'priority' };

      messageQueueService.enqueue(regularMsg);
      messageQueueService.enqueue(priorityMsg);

      const flushed = messageQueueService.flush();

      expect(flushed[0].type).toBe('url_result'); // Priority first
      expect(flushed[1].type).toBe('regular');
    });

    test('should maintain priority order', () => {
      messageQueueService.enqueue({ type: 'regular1' });
      messageQueueService.enqueue({ type: 'url_result' });
      messageQueueService.enqueue({ type: 'regular2' });
      messageQueueService.enqueue({ type: 'risk_alert' });

      const flushed = messageQueueService.flush();

      expect(flushed[0].type).toBe('url_result');
      expect(flushed[1].type).toBe('risk_alert');
      expect(flushed[2].type).toBe('regular1');
      expect(flushed[3].type).toBe('regular2');
    });

    test('should drop oldest non-priority when exceeding max size', () => {
      // Set small max size for testing
      messageQueueService.maxSize = 3;

      messageQueueService.enqueue({ type: 'regular1' });
      messageQueueService.enqueue({ type: 'regular2' });
      messageQueueService.enqueue({ type: 'regular3' });
      messageQueueService.enqueue({ type: 'regular4' });

      expect(messageQueueService.size).toBe(3);
      
      const flushed = messageQueueService.flush();
      expect(flushed.find(m => m.type === 'regular1')).toBeUndefined(); // Oldest dropped
    });

    test('should drop oldest priority when all messages are priority', () => {
      messageQueueService.maxSize = 2;

      messageQueueService.enqueue({ type: 'url_result' });
      messageQueueService.enqueue({ type: 'risk_alert' });
      messageQueueService.enqueue({ type: 'url_check' });

      expect(messageQueueService.size).toBe(2);
      
      const flushed = messageQueueService.flush();
      expect(flushed.find(m => m.type === 'url_result')).toBeUndefined(); // Oldest priority dropped
    });

    test('should persist queue to session storage', async () => {
      const message = { type: 'test', data: 'value' };

      messageQueueService.enqueue(message);

      // Wait for async persist
      await new Promise(resolve => setTimeout(resolve, 100));

      expect(chrome.storage.session.set).toHaveBeenCalledWith(
        expect.objectContaining({
          messageQueue: expect.arrayContaining([
            expect.objectContaining({
              message: expect.objectContaining({ type: 'test' }),
              priority: false
            })
          ])
        })
      );
    });
  });

  describe('Flush Operations', () => {
    test('should flush all valid messages', () => {
      messageQueueService.enqueue({ type: 'msg1' });
      messageQueueService.enqueue({ type: 'msg2' });

      const flushed = messageQueueService.flush();

      expect(flushed.length).toBe(2);
      expect(messageQueueService.size).toBe(0);
    });

    test('should filter out expired messages', () => {
      // Mock old timestamp
      const oldMessage = {
        message: { type: 'old' },
        timestamp: Date.now() - (6 * 60 * 1000), // 6 minutes ago
        priority: false
      };

      messageQueueService.queue.push(oldMessage);
      messageQueueService.enqueue({ type: 'new' });

      const flushed = messageQueueService.flush();

      expect(flushed.length).toBe(1);
      expect(flushed[0].type).toBe('new');
    });

    test('should clear session storage after flush', async () => {
      messageQueueService.enqueue({ type: 'test' });
      messageQueueService.flush();

      // Wait for async operation
      await new Promise(resolve => setTimeout(resolve, 100));

      expect(chrome.storage.session.remove).toHaveBeenCalledWith('messageQueue');
    });

    test('should return empty array when queue is empty', () => {
      const flushed = messageQueueService.flush();

      expect(flushed).toEqual([]);
      expect(messageQueueService.size).toBe(0);
    });
  });

  describe('Priority Detection', () => {
    test('should identify url_result as priority', () => {
      const isPriority = messageQueueService.isPriorityMessage({ type: 'url_result' });

      expect(isPriority).toBe(true);
    });

    test('should identify risk_alert as priority', () => {
      const isPriority = messageQueueService.isPriorityMessage({ type: 'risk_alert' });

      expect(isPriority).toBe(true);
    });

    test('should identify url_check as priority', () => {
      const isPriority = messageQueueService.isPriorityMessage({ type: 'url_check' });

      expect(isPriority).toBe(true);
    });

    test('should identify regular messages as non-priority', () => {
      const isPriority = messageQueueService.isPriorityMessage({ type: 'regular' });

      expect(isPriority).toBe(false);
    });
  });

  describe('Queue Management', () => {
    test('should report correct queue size', () => {
      expect(messageQueueService.size).toBe(0);

      messageQueueService.enqueue({ type: 'test1' });
      expect(messageQueueService.size).toBe(1);

      messageQueueService.enqueue({ type: 'test2' });
      expect(messageQueueService.size).toBe(2);
    });

    test('should check if queue has messages', () => {
      expect(messageQueueService.hasMessages).toBe(false);

      messageQueueService.enqueue({ type: 'test' });
      expect(messageQueueService.hasMessages).toBe(true);

      messageQueueService.flush();
      expect(messageQueueService.hasMessages).toBe(false);
    });

    test('should clear queue', () => {
      messageQueueService.enqueue({ type: 'test1' });
      messageQueueService.enqueue({ type: 'test2' });

      messageQueueService.clear();

      expect(messageQueueService.size).toBe(0);
      expect(messageQueueService.hasMessages).toBe(false);
    });
  });

  describe('Restore Operations', () => {
    test('should restore valid messages from session storage', async () => {
      const storedQueue = [
        {
          message: { type: 'restored1' },
          timestamp: Date.now() - 1000,
          priority: false
        },
        {
          message: { type: 'restored2' },
          timestamp: Date.now() - 2000,
          priority: true
        }
      ];

      chrome.storage.session.get.mockResolvedValue({ messageQueue: storedQueue });

      await messageQueueService.restore();

      expect(messageQueueService.size).toBe(2);
    });

    test('should filter expired messages during restore', async () => {
      const storedQueue = [
        {
          message: { type: 'expired' },
          timestamp: Date.now() - (6 * 60 * 1000), // 6 minutes old
          priority: false
        },
        {
          message: { type: 'valid' },
          timestamp: Date.now() - 1000,
          priority: false
        }
      ];

      chrome.storage.session.get.mockResolvedValue({ messageQueue: storedQueue });

      await messageQueueService.restore();

      expect(messageQueueService.size).toBe(1);
      expect(messageQueueService.queue[0].message.type).toBe('valid');
    });

    test('should handle missing session data', async () => {
      chrome.storage.session.get.mockResolvedValue({});

      await messageQueueService.restore();

      expect(messageQueueService.size).toBe(0);
    });

    test('should handle restore errors gracefully', async () => {
      chrome.storage.session.get.mockRejectedValue(new Error('Storage error'));

      await expect(messageQueueService.restore()).resolves.not.toThrow();
    });
  });

  describe('Statistics', () => {
    test('should return accurate stats', () => {
      messageQueueService.enqueue({ type: 'regular' });
      messageQueueService.enqueue({ type: 'url_result' });
      messageQueueService.enqueue({ type: 'risk_alert' });

      const stats = messageQueueService.getStats();

      expect(stats.size).toBe(3);
      expect(stats.priorityCount).toBe(2);
      expect(stats.maxSize).toBe(100);
      expect(stats.ttlSeconds).toBe(300);
      expect(stats.oldestAgeSeconds).toBeGreaterThanOrEqual(0);
    });

    test('should return zero age for empty queue', () => {
      const stats = messageQueueService.getStats();

      expect(stats.size).toBe(0);
      expect(stats.oldestAgeSeconds).toBe(0);
    });
  });

  describe('DequeueOne — safe single-item pop (EX-6 queue reliability)', () => {
    test('should return null when queue is empty', () => {
      const item = messageQueueService.dequeueOne();
      expect(item).toBeNull();
    });

    test('should remove and return the first item', () => {
      messageQueueService.enqueue({ type: 'first' });
      messageQueueService.enqueue({ type: 'second' });

      const item = messageQueueService.dequeueOne();

      expect(item).not.toBeNull();
      expect(item.message.type).toBe('first');
      expect(messageQueueService.size).toBe(1);
    });

    test('should remove items one-at-a-time leaving the rest in queue', () => {
      messageQueueService.enqueue({ type: 'a' });
      messageQueueService.enqueue({ type: 'b' });
      messageQueueService.enqueue({ type: 'c' });

      messageQueueService.dequeueOne();
      expect(messageQueueService.size).toBe(2);

      messageQueueService.dequeueOne();
      expect(messageQueueService.size).toBe(1);

      const last = messageQueueService.dequeueOne();
      expect(last.message.type).toBe('c');
      expect(messageQueueService.size).toBe(0);
    });

    test('should discard expired items and return the first valid one', () => {
      // Inject an expired item directly
      messageQueueService.queue.push({
        message: { type: 'expired' },
        timestamp: Date.now() - (6 * 60 * 1000),
        priority: false
      });
      messageQueueService.enqueue({ type: 'valid' });

      const item = messageQueueService.dequeueOne();

      expect(item).not.toBeNull();
      expect(item.message.type).toBe('valid');
      expect(messageQueueService.size).toBe(0);
    });

    test('should return null when all items are expired', () => {
      messageQueueService.queue.push({
        message: { type: 'expired1' },
        timestamp: Date.now() - (6 * 60 * 1000),
        priority: false
      });
      messageQueueService.queue.push({
        message: { type: 'expired2' },
        timestamp: Date.now() - (7 * 60 * 1000),
        priority: false
      });

      const item = messageQueueService.dequeueOne();

      expect(item).toBeNull();
      expect(messageQueueService.size).toBe(0);
    });

    test('remaining items stay in queue after mid-flush failure simulation', () => {
      // Simulate ConnectionService.flushQueue() behaviour: dequeue one item at
      // a time, stop on failure, re-enqueue the failed item so it is retried.
      messageQueueService.enqueue({ type: 'msg1' });
      messageQueueService.enqueue({ type: 'msg2' });
      messageQueueService.enqueue({ type: 'msg3' });

      // First item dequeued and "sent" successfully — removed from queue
      const first = messageQueueService.dequeueOne();
      expect(first.message.type).toBe('msg1');
      expect(messageQueueService.size).toBe(2); // msg2 and msg3 remain

      // Second item dequeued — send attempt begins
      const second = messageQueueService.dequeueOne();
      expect(second.message.type).toBe('msg2');
      expect(messageQueueService.size).toBe(1); // only msg3 remains

      // "Connection drops" before send completes — put msg2 back
      messageQueueService.enqueue(second.message);
      // Queue now has msg3 (was never dequeued) + re-enqueued msg2
      expect(messageQueueService.size).toBe(2);

      // Verify no messages were silently lost
      const remaining = messageQueueService.flush();
      expect(remaining.length).toBe(2);
      const types = remaining.map(m => m.type);
      expect(types).toContain('msg2');
      expect(types).toContain('msg3');
    });
  });

  describe('messageId assignment (EX-6 deduplication)', () => {
    test('should assign a messageId to each enqueued item', () => {
      messageQueueService.enqueue({ type: 'test' });
      const item = messageQueueService.dequeueOne();
      expect(item.message.messageId).toBeDefined();
      expect(typeof item.message.messageId).toBe('string');
      expect(item.message.messageId.length).toBeGreaterThan(0);
    });

    test('should preserve an existing messageId without overwriting', () => {
      const existingId = 'my-existing-id-123';
      messageQueueService.enqueue({ type: 'test', messageId: existingId });
      const item = messageQueueService.dequeueOne();
      expect(item.message.messageId).toBe(existingId);
    });

    test('should assign unique messageIds to different messages', () => {
      messageQueueService.enqueue({ type: 'a' });
      messageQueueService.enqueue({ type: 'b' });

      const a = messageQueueService.dequeueOne();
      const b = messageQueueService.dequeueOne();

      expect(a.message.messageId).not.toBe(b.message.messageId);
    });
  });

  describe('Error Handling', () => {
    test('should handle persist errors gracefully', async () => {
      chrome.storage.session.set.mockRejectedValue(new Error('Storage error'));

      expect(() => messageQueueService.enqueue({ type: 'test' })).not.toThrow();
    });

    test('should handle clear errors gracefully', async () => {
      chrome.storage.session.remove.mockRejectedValue(new Error('Storage error'));

      expect(() => messageQueueService.clear()).not.toThrow();
    });
  });
});

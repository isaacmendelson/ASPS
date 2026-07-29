// ============================================
// AntiScam Extension - Message Queue Service
// Buffers messages during disconnection
// ============================================

class MessageQueueService {
  constructor() {
    this.queue = [];
    this.maxSize = 100;        // User decision: 100 max
    this.ttlMs = 5 * 60 * 1000; // User decision: 5 minutes
  }

  /**
   * Add message to queue
   * @param {Object} message - Message to queue
   * @returns {boolean} - True if queued, false if dropped
   */
  enqueue(message) {
    // Determine if high priority (risk alerts)
    const isPriority = this.isPriorityMessage(message);

    // Assign a unique messageId for deduplication unless one already exists
    const msgWithId = message.messageId
      ? message
      : { ...message, messageId: `${Date.now()}-${Math.random().toString(36).slice(2, 9)}` };

    const item = {
      message: msgWithId,
      timestamp: Date.now(),
      priority: isPriority
    };

    if (isPriority) {
      // Insert after existing priority messages
      const firstNonPriority = this.queue.findIndex(i => !i.priority);
      if (firstNonPriority === -1) {
        this.queue.push(item);
      } else {
        this.queue.splice(firstNonPriority, 0, item);
      }
    } else {
      this.queue.push(item);
    }

    // Enforce size limit - drop oldest non-priority first
    while (this.queue.length > this.maxSize) {
      const oldestNonPriority = this.queue.findIndex(i => !i.priority);
      if (oldestNonPriority !== -1) {
        const dropped = this.queue.splice(oldestNonPriority, 1)[0];
        console.log('[MessageQueue] Dropped oldest non-priority:', dropped.message.type);
      } else {
        // All priority, drop oldest
        const dropped = this.queue.shift();
        console.log('[MessageQueue] Dropped oldest priority:', dropped.message.type);
      }
    }

    console.log(`[MessageQueue] Queued: ${message.type} (priority: ${isPriority}, size: ${this.queue.length})`);

    // Persist to chrome.storage.session (fire-and-forget, survives SW termination)
    this.persist();

    return true;
  }

  /**
   * Flush queue, returning valid (non-expired) messages
   * @returns {Array} - Array of messages to send
   */
  flush() {
    const now = Date.now();

    // Filter out expired messages
    const valid = this.queue.filter(item => {
      const age = now - item.timestamp;
      if (age >= this.ttlMs) {
        console.log(`[MessageQueue] Expired (${Math.round(age/1000)}s old): ${item.message.type}`);
        return false;
      }
      return true;
    });

    const messages = valid.map(item => item.message);
    const expiredCount = this.queue.length - valid.length;

    this.queue = [];

    // Clear persisted data after flush
    chrome.storage.session.remove('messageQueue').catch(() => {});

    console.log(`[MessageQueue] Flushed: ${messages.length} messages (${expiredCount} expired)`);
    return messages;
  }

  /**
   * Check if message is high priority
   */
  isPriorityMessage(message) {
    // Risk alerts and URL results are priority
    const priorityTypes = ['url_result', 'risk_alert', 'url_check'];
    return priorityTypes.includes(message.type);
  }

  /**
   * Remove and return the next non-expired message from the front of the queue,
   * or null if the queue is empty. Expired items are discarded silently.
   * Use this for safe one-at-a-time delivery during flush so that only
   * successfully-sent messages are removed — callers must call this in a loop
   * and stop (without calling again) when delivery fails.
   * @returns {{message: Object, timestamp: number, priority: boolean}|null}
   */
  dequeueOne() {
    const now = Date.now();

    // Discard expired items from the front
    while (this.queue.length > 0) {
      const item = this.queue[0];
      const age = now - item.timestamp;
      if (age >= this.ttlMs) {
        this.queue.shift();
        console.log(`[MessageQueue] Expired on dequeue (${Math.round(age / 1000)}s old): ${item.message.type}`);
        continue;
      }
      // Found a valid item — remove and return it
      this.queue.shift();
      this.persist();
      return item;
    }

    return null;
  }

  /**
   * Get current queue size
   */
  get size() {
    return this.queue.length;
  }

  /**
   * Check if queue has messages
   */
  get hasMessages() {
    return this.queue.length > 0;
  }

  /**
   * Clear the queue
   */
  clear() {
    const count = this.queue.length;
    this.queue = [];
    // Clear persisted data
    chrome.storage.session.remove('messageQueue').catch(() => {});
    console.log(`[MessageQueue] Cleared: ${count} messages`);
  }

  /**
   * Persist queue to chrome.storage.session (survives SW termination)
   * Fire-and-forget: errors are logged but don't block enqueue
   */
  async persist() {
    try {
      await chrome.storage.session.set({
        messageQueue: this.queue.map(item => ({
          message: item.message,
          timestamp: item.timestamp,
          priority: item.priority
        }))
      });
      console.log(`[MessageQueue] Persisted ${this.queue.length} messages to session storage`);
    } catch (e) {
      console.error('[MessageQueue] Failed to persist to session storage:', e);
    }
  }

  /**
   * Restore queue from chrome.storage.session (after SW restart)
   * Filters out expired messages based on TTL
   */
  async restore() {
    try {
      const data = await chrome.storage.session.get('messageQueue');
      if (!data.messageQueue || !Array.isArray(data.messageQueue)) {
        console.log('[MessageQueue] No persisted queue to restore');
        return;
      }

      const now = Date.now();
      const restored = data.messageQueue.filter(item => {
        const age = now - item.timestamp;
        if (age >= this.ttlMs) {
          console.log(`[MessageQueue] Skipping expired persisted message (${Math.round(age / 1000)}s old): ${item.message.type}`);
          return false;
        }
        return true;
      });

      if (restored.length > 0) {
        this.queue = restored;
        console.log(`[MessageQueue] Restored ${restored.length} messages from session storage (${data.messageQueue.length - restored.length} expired)`);
      } else {
        console.log('[MessageQueue] All persisted messages expired, nothing to restore');
      }

      // Clear persisted data after restore (will be re-persisted on next enqueue)
      await chrome.storage.session.remove('messageQueue');
    } catch (e) {
      console.error('[MessageQueue] Failed to restore from session storage:', e);
    }
  }

  /**
   * Get queue stats for debugging
   */
  getStats() {
    const now = Date.now();
    const priorityCount = this.queue.filter(i => i.priority).length;
    const oldestAge = this.queue.length > 0
      ? Math.round((now - this.queue[0].timestamp) / 1000)
      : 0;

    return {
      size: this.queue.length,
      priorityCount,
      oldestAgeSeconds: oldestAge,
      maxSize: this.maxSize,
      ttlSeconds: this.ttlMs / 1000
    };
  }
}

// Singleton instance
export const messageQueueService = new MessageQueueService();
export { MessageQueueService };
export default messageQueueService;

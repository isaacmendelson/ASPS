// ============================================
// AntiScam Extension - Cache Service
// Manages URL result caching with TTL
// ============================================

import stateManager from '../state/StateManager.js';

class CacheService {
  constructor() {
    this.cache = new Map();
    this.config = {
      defaultTTL: 3600, // 1 hour in seconds
      maxEntries: 1000,
      persistKey: 'urlCache'
    };
  }

  // Initialize cache from storage
  async init() {
    try {
      const stored = await chrome.storage.local.get([this.config.persistKey]);

      if (stored[this.config.persistKey]) {
        const now = Date.now();

        for (const [domain, entry] of Object.entries(stored[this.config.persistKey])) {
          const expired = (now - entry.savedAt) > (entry.data.ttl * 1000);

          if (!expired) {
            this.cache.set(domain, entry);
          }
        }
      }

      this.updateCacheState();
      console.log(`[CacheService] Loaded ${this.cache.size} cached entries`);
    } catch (e) {
      console.error('[CacheService] Init error:', e);
    }
  }

  // Get cached result
  get(url) {
    const domain = this.extractDomain(url);
    const entry = this.cache.get(domain);

    if (!entry) {
      return null;
    }

    // Check if expired
    const now = Date.now();
    const ttl = (entry.data.ttl || this.config.defaultTTL) * 1000;
    const expired = (now - entry.savedAt) > ttl;

    if (expired) {
      this.cache.delete(domain);
      this.schedulePersist();
      return null;
    }

    return {
      ...entry.data,
      fromCache: true,
      cachedAt: entry.savedAt
    };
  }

  // Set cache entry
  set(url, data) {
    const domain = this.extractDomain(url);

    // Enforce max entries
    if (this.cache.size >= this.config.maxEntries) {
      this.evictOldest();
    }

    const entry = {
      data: {
        ...data,
        ttl: data.ttl || this.config.defaultTTL
      },
      savedAt: Date.now()
    };

    this.cache.set(domain, entry);
    this.schedulePersist();
    this.updateCacheState();

    console.log(`[CacheService] Cached result for ${domain}`);
  }

  // Check if URL is cached
  has(url) {
    const result = this.get(url);
    return result !== null;
  }

  // Delete cache entry
  delete(url) {
    const domain = this.extractDomain(url);
    const deleted = this.cache.delete(domain);

    if (deleted) {
      this.schedulePersist();
      this.updateCacheState();
    }

    return deleted;
  }

  // Clear all cache
  clear() {
    this.cache.clear();
    chrome.storage.local.remove([this.config.persistKey]);
    stateManager.update({
      'cache.size': 0,
      'cache.lastCleared': Date.now()
    });
    console.log('[CacheService] Cache cleared');
  }

  // Get cache size
  size() {
    return this.cache.size;
  }

  // Get all cached domains
  getDomains() {
    return Array.from(this.cache.keys());
  }

  // Extract domain from URL
  extractDomain(url) {
    try {
      return new URL(url).hostname;
    } catch {
      return url;
    }
  }

  // Evict oldest entries
  evictOldest() {
    const entries = Array.from(this.cache.entries());

    // Sort by savedAt (oldest first)
    entries.sort((a, b) => a[1].savedAt - b[1].savedAt);

    // Remove oldest 10%
    const toRemove = Math.max(1, Math.floor(entries.length * 0.1));

    for (let i = 0; i < toRemove; i++) {
      this.cache.delete(entries[i][0]);
    }

    console.log(`[CacheService] Evicted ${toRemove} oldest entries`);
  }

  // Update cache state in StateManager
  updateCacheState() {
    stateManager.set('cache.size', this.cache.size);
  }

  // Persist cache to storage (debounced)
  persistTimeout = null;
  schedulePersist() {
    if (this.persistTimeout) {
      clearTimeout(this.persistTimeout);
    }
    this.persistTimeout = setTimeout(() => {
      this.persist();
    }, 500);
  }

  async persist() {
    try {
      const cacheObj = {};

      this.cache.forEach((value, key) => {
        cacheObj[key] = value;
      });

      await chrome.storage.local.set({ [this.config.persistKey]: cacheObj });
    } catch (e) {
      console.error('[CacheService] Persist error:', e);
    }
  }

  // Cleanup expired entries
  cleanup() {
    const now = Date.now();
    let removed = 0;

    for (const [domain, entry] of this.cache.entries()) {
      const ttl = (entry.data.ttl || this.config.defaultTTL) * 1000;
      const expired = (now - entry.savedAt) > ttl;

      if (expired) {
        this.cache.delete(domain);
        removed++;
      }
    }

    if (removed > 0) {
      this.schedulePersist();
      this.updateCacheState();
      console.log(`[CacheService] Cleaned up ${removed} expired entries`);
    }

    return removed;
  }

  // Get cache statistics
  getStats() {
    const now = Date.now();
    let expiringSoon = 0;
    let totalSize = 0;

    for (const [, entry] of this.cache.entries()) {
      const ttl = (entry.data.ttl || this.config.defaultTTL) * 1000;
      const remaining = (entry.savedAt + ttl) - now;

      if (remaining < 300000) { // Less than 5 minutes
        expiringSoon++;
      }

      totalSize += JSON.stringify(entry).length;
    }

    return {
      entries: this.cache.size,
      expiringSoon,
      totalSize,
      maxEntries: this.config.maxEntries
    };
  }
}

// Singleton instance
export const cacheService = new CacheService();
export default cacheService;

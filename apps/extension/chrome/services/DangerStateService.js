// ============================================
// AntiScam Extension - DangerStateService
// ASPS-622: Persist and restore danger/tracking state across
// MV3 service-worker restarts.
//
// Persists to chrome.storage.session (survives SW termination within the
// same browser session, cleared on browser exit).  Falls back to
// chrome.storage.local when session storage is unavailable (older Chrome,
// or the API isn't exposed by the test environment).
//
// Keys stored:
//   dangerState  →  { immediateDangerMode, isDeviceRemoteControlled,
//                     trackedDomainsEntries, sensitiveDomainCacheEntries,
//                     persistedAt }
// ============================================

const STORAGE_KEY            = 'dangerState';
const SESSION_FALLBACK_KEY   = 'dangerState_session'; // used when session unavailable
const STALE_THRESHOLD_MS     = 24 * 60 * 60 * 1000;  // 24 h — sessions older than this are discarded

// ── Helpers ──────────────────────────────────────────────────────────────────

/** Convert a trackedDomains Map to a JSON-safe entries array. */
function serializeMap(map) {
  return Array.from(map.entries());
}

/** Rebuild a Map from a stored entries array. */
function deserializeMap(entries) {
  return new Map(entries || []);
}

/**
 * Return true if a trackedDomains entry has passed its TTL.
 * Entries without an expiresAt field are treated as permanent.
 */
function isEntryExpired(entry) {
  if (!entry) return false;
  return typeof entry.expiresAt === 'number' && Date.now() > entry.expiresAt;
}

/** Remove expired entries from a trackedDomains Map. */
function pruneExpiredEntries(map) {
  const pruned = new Map();
  for (const [key, val] of map) {
    if (!isEntryExpired(val)) {
      pruned.set(key, val);
    }
  }
  return pruned;
}

/** Return the best available storage area. */
function getStorage() {
  if (typeof chrome !== 'undefined' &&
      chrome.storage &&
      chrome.storage.session) {
    return chrome.storage.session;
  }
  // Fallback (Chrome < 102 or test environments that don't expose session)
  return chrome.storage.local;
}

// ─────────────────────────────────────────────────────────────────────────────

class DangerStateService {

  /**
   * Persist the current in-memory danger/tracking state to storage.
   *
   * Call this whenever any of the four state variables changes:
   *   - immediateDangerMode
   *   - isDeviceRemoteControlled
   *   - trackedDomains (Map)
   *   - sensitiveDomainCache (Map)
   *
   * @param {object} state
   * @param {boolean}  state.immediateDangerMode
   * @param {boolean}  state.isDeviceRemoteControlled
   * @param {Map}      state.trackedDomains
   * @param {Map}      state.sensitiveDomainCache
   */
  async persist({ immediateDangerMode, isDeviceRemoteControlled, trackedDomains, sensitiveDomainCache }) {
    const payload = {
      immediateDangerMode:        !!immediateDangerMode,
      isDeviceRemoteControlled:   !!isDeviceRemoteControlled,
      trackedDomainsEntries:      serializeMap(trackedDomains || new Map()),
      sensitiveDomainCacheEntries: serializeMap(sensitiveDomainCache || new Map()),
      persistedAt:                Date.now(),
    };

    try {
      await getStorage().set({ [STORAGE_KEY]: payload });
      console.log('[DangerStateService] State persisted');
    } catch (e) {
      console.warn('[DangerStateService] persist failed:', e);
    }
  }

  /**
   * Restore danger/tracking state from storage.
   * Must be awaited in init() BEFORE setupWebSocketHandlers() is called.
   *
   * Returns an object with four keys:
   *   { immediateDangerMode, isDeviceRemoteControlled, trackedDomains, sensitiveDomainCache }
   *
   * Expired trackedDomains entries are pruned on restore.
   * Stale sessions (> 24 h old) are discarded and treated as a clean state.
   */
  async restore() {
    const defaultState = {
      immediateDangerMode:      false,
      isDeviceRemoteControlled: false,
      trackedDomains:           new Map(),
      sensitiveDomainCache:     new Map(),
    };

    try {
      const stored = await getStorage().get([STORAGE_KEY]);
      const saved  = stored[STORAGE_KEY];

      if (!saved) {
        console.log('[DangerStateService] No persisted state — using defaults');
        return defaultState;
      }

      // Discard stale sessions so danger mode from a previous browser
      // session (> 24 h ago) never re-activates silently.
      if (typeof saved.persistedAt === 'number') {
        const ageMs = Date.now() - saved.persistedAt;
        if (ageMs > STALE_THRESHOLD_MS) {
          console.log('[DangerStateService] Persisted state is stale (age=%dh) — discarding',
            Math.floor(ageMs / 3_600_000));
          await this._clearStorage();
          return defaultState;
        }
      }

      const trackedDomains = pruneExpiredEntries(
        deserializeMap(saved.trackedDomainsEntries)
      );

      const sensitiveDomainCache = deserializeMap(saved.sensitiveDomainCacheEntries);

      console.log(
        '[DangerStateService] Restored: dangerMode=%s rc=%s tracked=%d sensitive=%d',
        saved.immediateDangerMode,
        saved.isDeviceRemoteControlled,
        trackedDomains.size,
        sensitiveDomainCache.size
      );

      return {
        immediateDangerMode:      !!saved.immediateDangerMode,
        isDeviceRemoteControlled: !!saved.isDeviceRemoteControlled,
        trackedDomains,
        sensitiveDomainCache,
      };
    } catch (e) {
      console.warn('[DangerStateService] restore failed:', e);
      return defaultState;
    }
  }

  /** Clear persisted state (used internally when stale). */
  async _clearStorage() {
    try {
      await getStorage().remove([STORAGE_KEY]);
    } catch (_) { /* ignore */ }
  }
}

// Exported helpers (used by background.js directly when rebuilding Maps)
export { serializeMap, deserializeMap, pruneExpiredEntries, isEntryExpired };

export const dangerStateService = new DangerStateService();
export default dangerStateService;

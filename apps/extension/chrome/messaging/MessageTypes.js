// ============================================
// AntiScam Extension - Message Types
// Centralized message type definitions
// ============================================

export const MSG = {
  // ==========================================
  // Connection Messages
  // ==========================================
  CONNECTION_CHECK: 'connection:check',
  CONNECTION_STATUS: 'connection:status',
  CONNECTION_RECONNECT: 'connection:reconnect',

  // ==========================================
  // Scan Messages
  // ==========================================
  SCAN_URL: 'scan:url',
  SCAN_RESULT: 'scan:result',
  SCAN_PAGE: 'scan:page',
  SCAN_CURRENT: 'scan:current',

  // ==========================================
  // Page Info Messages
  // ==========================================
  PAGE_INFO_REQUEST: 'page:info:request',
  PAGE_INFO_RESPONSE: 'page:info:response',

  // Sent from background to content script to ask whether the user appears to
  // be logged in to this tab (DOM scan for logout indicators in he/en/fr/ru).
  // Response is { loggedIn: true|false|null }.
  CHECK_LOGGED_IN_REQUEST:  'auth:check_logged_in:request',
  CHECK_LOGGED_IN_RESPONSE: 'auth:check_logged_in:response',

  // ==========================================
  // Warning/Protection Messages
  // ==========================================
  SHOW_WARNING: 'warning:show',
  BLOCK_PAGE: 'warning:block',
  REMOVE_WARNING: 'warning:remove',

  // ==========================================
  // Auth Messages
  // ==========================================
  AUTH_SIGN_IN: 'auth:signin',
  AUTH_SIGN_OUT: 'auth:signout',
  AUTH_STATUS: 'auth:status',

  // ==========================================
  // Cache Messages
  // ==========================================
  CACHE_CLEAR: 'cache:clear',
  CACHE_GET: 'cache:get',
  CACHE_SET: 'cache:set',

  // ==========================================
  // Status Messages
  // ==========================================
  STATUS_GET: 'status:get',
  STATUS_UPDATE: 'status:update',

  // ==========================================
  // Desktop App Messages (WebSocket)
  // ==========================================
  WS_PING: 'ping',
  WS_PONG: 'pong',
  WS_URL_CHECK: 'url_check',
  WS_URL_RESULT: 'url_result',
  WS_USER_AUTH: 'user_auth',
  WS_USER_SIGNOUT: 'user_signout',
  WS_NOTIFICATION: 'notification',
  WS_ERROR: 'error',
  WS_GET_BROWSER_TABS: 'get_browser_tabs',
  WS_BROWSER_TABS_RESPONSE: 'browser_tabs_response',
  WS_TRACK_URL_ALERT: 'track_url_alert',

  // ImmediateDanger lifecycle (broadcast by the agent when the backend tells
  // it ImmediateDanger started/ended). The extension uses these to set its
  // local immediateDangerMode flag.
  WS_IMMEDIATE_DANGER_STARTED: 'immediate_danger_started',
  WS_IMMEDIATE_DANGER_ENDED:   'immediate_danger_ended',

  // Sent by the extension to the agent whenever a tab is closed during
  // ImmediateDanger mode. Forwarded to the backend as a TabClosedAlert.
  WS_TAB_CLOSED_ALERT: 'tab_closed_alert',

  // Sent by the extension to the agent whenever a tab's URL changes
  // (existing or new) while IsDeviceRemoteControlled is true. Forwarded
  // to the backend as a TabChangedAlert. Lets the backend re-evaluate
  // ImmediateDanger immediately on a mid-session login to a sensitive site.
  WS_TAB_CHANGED_ALERT: 'tab_changed_alert',

  // Broadcast by the agent whenever its IsDeviceRemoteControlled flag
  // toggles (active incoming session = open). Extension caches this and
  // uses it as a gate for WS_TAB_CHANGED_ALERT / WS_TAB_CLOSED_ALERT.
  WS_SET_REMOTE_CONTROLLED: 'set_remote_controlled',

  // Sent by the extension to the agent on every reconnect to request an
  // authoritative state snapshot (danger mode, remote-controlled flag,
  // tracked domains). The agent responds by re-pushing the relevant messages
  // (WS_IMMEDIATE_DANGER_STARTED/_ENDED, WS_SET_REMOTE_CONTROLLED,
  // tracked_domains:set) so the extension can reconcile any drift caused by
  // a service-worker restart during a live session.
  WS_STATE_SYNC_REQUEST: 'state_sync_request',

  // ==========================================
  // Remote Access Warning Messages
  // ==========================================
  REMOTE_ACCESS_ALERT: 'remote_access_alert',           // From desktop when session detected
  REMOTE_ACCESS_WARNING_SHOW: 'warning:remote_access',  // To content script to show warning
  REMOTE_ACCESS_WARNING_DISMISS: 'warning:remote_dismiss', // Dismiss warning across tabs
  REMOTE_ACCESS_CLOSE_SESSION: 'remote:close_session',  // Request desktop to close session
  REMOTE_ACCESS_CONTINUED: 'remote:continued_anyway',   // User chose to continue
  REMOTE_ACCESS_SESSION_END: 'remote_access_session_end', // Session ended
  REMOTE_ACCESS_APP_CLOSED: 'remote_access_app_closed', // App closed

  // ==========================================
  // Tracking / Domain Messages
  // ==========================================
  TRACKED_DOMAINS_SET: 'tracked_domains:set',        // Backend → extension: set tracked domains
  TRACKED_DOMAINS_UPDATED: 'tracked_domains:updated', // Background → content: domains updated

  // ==========================================
  // Form Messages
  // ==========================================
  FORM_SUBMITTED: 'form:submitted',  // Content → background: form submitted

  // ==========================================
  // Close Session Result
  // ==========================================
  REMOTE_ACCESS_CLOSE_SESSION_RESULT: 'remote:close_session_result'
};

// Warning styles
export const WARNING_STYLE = {
  BANNER: 'banner',
  MODAL: 'modal',
  BLOCK: 'block'
};

// Protective action types (matching backend)
export const PROTECTIVE_ACTION = {
  NONE: 0,
  NOTIFY: 1,
  WARN_BANNER: 2,
  WARN_MODAL: 3,
  BLOCK: 4,
  ENABLE_URL_TRACKING: 8  // Matches backend ProtectiveActionType.EnableUrlTracking
};

// Risk types (matching backend)
export const RISK_TYPE = {
  SAFE: 0,
  PHISHING: 1,
  CLOAKING: 2,
  IMPERSONATION: 3,
  FAKE_DOMAIN: 4,
  UNKNOWN: 5
};

// Risk labels
export const RISK_LABELS = {
  [RISK_TYPE.SAFE]: 'Safe',
  [RISK_TYPE.PHISHING]: 'Phishing',
  [RISK_TYPE.CLOAKING]: 'Cloaking',
  [RISK_TYPE.IMPERSONATION]: 'Impersonation',
  [RISK_TYPE.FAKE_DOMAIN]: 'Fake Domain',
  [RISK_TYPE.UNKNOWN]: 'Unknown Risk'
};

// Remote access tool IDs (matching desktop config.py REMOTE_ACCESS_TOOLS)
export const REMOTE_TOOL = {
  ANYDESK: 1,
  TEAMVIEWER: 2,
  CHROME_RD: 3,
  ULTRAVIEWER: 4,
  LOGMEIN: 5,
  RUSTDESK: 6,
  AMMYY: 7,
  RDP: 8,
  QUICK_ASSIST: 9,
  CONNECTWISE: 10,
  UNKNOWN: 99
};

// Tool display names for UI
export const REMOTE_TOOL_NAMES = {
  [REMOTE_TOOL.ANYDESK]: 'AnyDesk',
  [REMOTE_TOOL.TEAMVIEWER]: 'TeamViewer',
  [REMOTE_TOOL.CHROME_RD]: 'Chrome Remote Desktop',
  [REMOTE_TOOL.ULTRAVIEWER]: 'UltraViewer',
  [REMOTE_TOOL.LOGMEIN]: 'LogMeIn',
  [REMOTE_TOOL.RUSTDESK]: 'RustDesk',
  [REMOTE_TOOL.AMMYY]: 'Ammyy Admin',
  [REMOTE_TOOL.RDP]: 'Remote Desktop',
  [REMOTE_TOOL.QUICK_ASSIST]: 'Quick Assist',
  [REMOTE_TOOL.CONNECTWISE]: 'ConnectWise',
  [REMOTE_TOOL.UNKNOWN]: 'Remote access software'
};

export default MSG;

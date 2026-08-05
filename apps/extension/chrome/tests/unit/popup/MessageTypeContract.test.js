// ASPS-669 regression test
//
// Root cause: popup.js sent messages using old plain-string type names
// ('getStatus', 'scanCurrentPage', 'reconnect') while the background
// MessageBus (background.js) only registers handlers on the new
// namespaced constants from messaging/MessageTypes.js
// (MSG.STATUS_GET = 'status:get', MSG.SCAN_CURRENT = 'scan:current',
// MSG.CONNECTION_RECONNECT = 'connection:reconnect'). With no matching
// handler, the popup's `chrome.runtime.sendMessage({ type: 'getStatus' })`
// always resolved to `{ error: 'No handler for message type: getStatus' }`,
// so the risk score never rendered and the UI stayed on "CHECKING..."
// forever.
//
// popup.js is a plain script (IIFE, not an ES module) and is not imported
// directly by background.js/MessageBus.js, so there is no natural seam to
// import it as a module. Rather than duplicating a verbatim copy (as done
// for content.js's FormMonitor — see tests/unit/content/FormMonitor.test.js),
// this test reads the real popup.js source and asserts, by static contract,
// that every `chrome.runtime.sendMessage({ type: '...' })` call site uses a
// message type string that is a real MSG.* constant from MessageTypes.js —
// the same constants background.js registers handlers against. This
// directly targets the root cause and fails again if a future edit
// reintroduces a stale/legacy type string.

import { describe, test, expect } from '@jest/globals';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { MSG } from '../../../messaging/MessageTypes.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const popupSource = fs.readFileSync(
  path.resolve(__dirname, '../../../popup.js'),
  'utf8'
);

// Extract every `type: '...'` literal passed directly to chrome.runtime.sendMessage(...)
function extractSentMessageTypes(source) {
  const regex = /chrome\.runtime\.sendMessage\(\{\s*type:\s*'([^']+)'/g;
  const types = [];
  let match;
  while ((match = regex.exec(source)) !== null) {
    types.push(match[1]);
  }
  return types;
}

// Handler types registered on messageBus in background.js as of ASPS-669.
// Kept here (rather than parsing background.js) because background.js has
// heavy side-effecting imports (WebSocket connection, chrome.alarms, etc.)
// that make it unsuitable to import directly in a unit test.
const BACKGROUND_MESSAGEBUS_HANDLED_TYPES = new Set([
  MSG.STATUS_GET,
  MSG.CONNECTION_RECONNECT,
  MSG.CACHE_CLEAR,
  MSG.SCAN_CURRENT,
  MSG.AUTH_SIGN_IN,
  MSG.AUTH_SIGN_OUT,
  MSG.REMOTE_ACCESS_WARNING_DISMISS,
  MSG.REMOTE_ACCESS_CLOSE_SESSION,
  MSG.REMOTE_ACCESS_CONTINUED,
  MSG.FORM_SUBMITTED
]);

// Message types popup.js sends that are intentionally NOT yet handled by
// background.js (staged in chrome.storage.local as a fallback, tracked as a
// documented follow-up in popup.js — see FeedbackService.sendFeedback /
// deleteFeedbackData). These are excluded from the "must have a handler"
// assertion below so this test stays focused on the ASPS-669 regression.
const KNOWN_UNHANDLED_FOLLOWUP_TYPES = new Set(['sendFeedback', 'deleteFeedback']);

describe('popup.js message type contract (ASPS-669 regression)', () => {
  const sentTypes = extractSentMessageTypes(popupSource);

  test('popup.js has sendMessage call sites to verify', () => {
    expect(sentTypes.length).toBeGreaterThan(0);
  });

  test('status polling uses MSG.STATUS_GET, not the legacy "getStatus" string', () => {
    expect(sentTypes).toContain(MSG.STATUS_GET);
    expect(sentTypes).not.toContain('getStatus');
  });

  test('manual scan trigger uses MSG.SCAN_CURRENT, not the legacy "scanCurrentPage" string', () => {
    expect(sentTypes).toContain(MSG.SCAN_CURRENT);
    expect(sentTypes).not.toContain('scanCurrentPage');
  });

  test('reconnect action uses MSG.CONNECTION_RECONNECT, not the legacy "reconnect" string', () => {
    expect(sentTypes).toContain(MSG.CONNECTION_RECONNECT);
    expect(sentTypes).not.toContain('reconnect');
  });

  test('every popup.js sendMessage type (excluding documented follow-ups) has a registered MessageBus handler', () => {
    const unmatched = sentTypes.filter(
      (t) => !KNOWN_UNHANDLED_FOLLOWUP_TYPES.has(t) && !BACKGROUND_MESSAGEBUS_HANDLED_TYPES.has(t)
    );
    expect(unmatched).toEqual([]);
  });
});

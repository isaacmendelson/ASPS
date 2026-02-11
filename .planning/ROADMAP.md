# Roadmap: ASPS Score Flow Repair

## Overview

The ASPS anti-phishing system has a broken score delivery pipeline: URLs are analyzed but results never reach the Chrome Extension. This roadmap repairs the pipeline link by link, starting with baseline communication verification, then fixing the async notification bridge (the most likely root cause), restoring end-to-end score flow, re-enabling CurveMQ encryption, and finally hardening reliability. Each phase builds on the previous, following the data flow from Extension through Desktop App to Backend and back.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Diagnose and Verify Baseline Communication** - Confirm each individual link (ZMQ REQ/REP, WebSocket) works in isolation
- [ ] **Phase 2: Fix Async Notification Bridge** - Repair the thread-to-asyncio bridge so ZMQ SUB notifications reach the WebSocket broadcast
- [ ] **Phase 3: Restore End-to-End Score Flow** - Verify the full pipeline works: URL submitted from Extension returns a score displayed in the popup
- [ ] **Phase 4: Restore CurveMQ Security** - Re-enable encrypted ZMQ communication between Desktop App and Backend
- [ ] **Phase 5: Harden Reliability and Document** - Add failure recovery, connection resilience, and produce the bug report for the server team

## Phase Details

### Phase 1: Diagnose and Verify Baseline Communication
**Goal**: Each communication link in the pipeline can send and receive messages independently, with diagnostic evidence confirming success or identifying the exact failure point
**Depends on**: Nothing (first phase)
**Requirements**: COMM-01, COMM-03
**Success Criteria** (what must be TRUE):
  1. Desktop App connects to Backend via ZMQ REQ on port 50001 and receives an acknowledgment response to a test alert
  2. Chrome Extension connects to Desktop App via WebSocket and receives a response to a test message
  3. Backend ports (50001, 50002, 5555, 5556) are confirmed listening and accepting connections
  4. Diagnostic logging is in place at each link boundary showing message send/receive with timestamps
**Plans**: TBD

Plans:
- [ ] 01-01: Verify backend ports and ZMQ REQ/REP round-trip
- [ ] 01-02: Verify WebSocket connectivity and Extension-to-Desktop message exchange

### Phase 2: Fix Async Notification Bridge
**Goal**: Notifications published by Backend on ZMQ PUB (port 50002) are received by Desktop App's SUB socket and successfully bridged from the ZMQ background thread to the asyncio event loop for WebSocket broadcast
**Depends on**: Phase 1
**Requirements**: COMM-02, COMM-04
**Success Criteria** (what must be TRUE):
  1. Desktop App's ZMQ SUB socket receives notification messages published by Backend on port 50002
  2. NotificationHandler bridges received messages from the ZMQ thread to the main asyncio event loop using thread-safe scheduling (not asyncio.run() fallback)
  3. ExtensionServer.broadcast() executes on the correct event loop and sends to all connected WebSocket clients
  4. Multipart ZMQ frames (topic + message) are received atomically via recv_multipart()
**Plans**: TBD

Plans:
- [ ] 02-01: Fix ZMQ SUB subscription and verify PUB/SUB message delivery
- [ ] 02-02: Fix thread-to-asyncio bridge in NotificationHandler

### Phase 3: Restore End-to-End Score Flow
**Goal**: A user visiting a URL in Chrome sees a threat score displayed in the Extension popup, proving the entire pipeline works from submission to display
**Depends on**: Phase 2
**Requirements**: FLOW-01, FLOW-02, FLOW-03, FLOW-04
**Success Criteria** (what must be TRUE):
  1. A URL submitted from the Chrome Extension reaches the Backend and triggers analysis (visible in Backend logs)
  2. The analysis result (threat score) is published by Backend and received by Desktop App (visible in Desktop App logs)
  3. Desktop App forwards the score to the Chrome Extension via WebSocket (visible in Extension console)
  4. Chrome Extension displays the threat score in the popup UI and updates the extension icon badge
  5. The full round-trip completes in under 10 seconds for a test URL
**Plans**: TBD

Plans:
- [ ] 03-01: Wire up and test complete URL-to-score pipeline
- [ ] 03-02: Verify Extension UI displays received scores correctly

### Phase 4: Restore CurveMQ Security
**Goal**: All ZMQ communication between Desktop App (pyzmq) and Backend (NetMQ) is encrypted via CurveMQ, with proper key exchange and no silent handshake failures
**Depends on**: Phase 3
**Requirements**: SEC-01, SEC-02
**Success Criteria** (what must be TRUE):
  1. Backend runs with CurveEnabled=true and Desktop App connects successfully using CURVE client keys on both REQ and SUB sockets
  2. Server public key is correctly distributed from Backend to Desktop App (via config or token response)
  3. End-to-end score flow still works with encryption enabled (no regression from Phase 3)
**Plans**: TBD

Plans:
- [ ] 04-01: Implement CurveMQ client in Desktop App and verify encrypted communication

### Phase 5: Harden Reliability and Document
**Goal**: The system recovers gracefully from common failure scenarios (lost ZMQ responses, WebSocket disconnects, service worker termination) and a detailed bug report is delivered to the server team
**Depends on**: Phase 4
**Requirements**: REL-01, REL-02, REL-03, DOC-01
**Success Criteria** (what must be TRUE):
  1. ZMQ REQ socket recovers from a lost/timed-out response without permanently corrupting the REQ/REP state machine
  2. WebSocket reconnection between Extension and Desktop App works automatically after a disconnect, with pending results delivered after reconnection
  3. Chrome Extension service worker survives keepalive cycles and re-establishes WebSocket connection after termination
  4. A bug report document exists describing: what was broken, why it broke, how it was fixed, and recommendations for the server team
**Plans**: TBD

Plans:
- [ ] 05-01: Add ZMQ REQ recovery and WebSocket reconnection resilience
- [ ] 05-02: Harden Chrome Extension service worker lifecycle
- [ ] 05-03: Write bug report for server team

## Progress

**Execution Order:**
Phases execute in numeric order: 1 --> 2 --> 3 --> 4 --> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Diagnose and Verify Baseline Communication | 0/2 | Not started | - |
| 2. Fix Async Notification Bridge | 0/2 | Not started | - |
| 3. Restore End-to-End Score Flow | 0/2 | Not started | - |
| 4. Restore CurveMQ Security | 0/1 | Not started | - |
| 5. Harden Reliability and Document | 0/3 | Not started | - |

---
*Roadmap created: 2026-02-12*
*Last updated: 2026-02-12*

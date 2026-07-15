# ASPS Unified System Requirements

**Version:** 2026-07-15  
**Status:** Consolidated requirements draft  
**Scope:** Existing authoritative ASPS specification plus functional definitions uploaded on 2026-07-15  

## 1. Purpose

ASPS is a distributed real-time anti-scam protection platform. It protects users from phishing, investment scams, tech-support scams, recovery scams, unauthorized remote-access abuse, and related fraud journeys by correlating signals across user devices, browser activity, messages, phone calls, remote-access sessions, and intelligence sources.

The system must support three analysis layers:

1. **Device Layer** - analyzes atomic device-level events such as URL visits, remote-access state changes, phone events, and tracked-domain interactions.
2. **User Layer** - correlates multi-device and historical events into user-level risk, scam journey state, and protective actions.
3. **Intelligence Layer** - enriches analysis with risky domains, lead-list exposure, blacklisted phone numbers, malicious message links, and crawled content from suspicious sites.

## 2. Current Baseline

The current authoritative specification describes these major components:

- **ASPSBackend**: .NET backend host service, real-time analysis engine, NetMQ/CURVE listener and publisher, CQRS gateway, EF Core persistence, MySQL database, ASView in-memory read model.
- **WebApi/Admin Portal**: ASP.NET Core Razor Pages, REST controllers, SignalR, Keycloak OIDC, CQRS over NetMQ.
- **Desktop Agent**: Python/Windows endpoint monitor that communicates with browser extensions and backend.
- **Chrome Extension**: Manifest V3 extension for URL reporting, user warnings, and local browser actions.
- **URL Analyzer**: Python/FastAPI or subprocess analyzer for URL risk scoring, phishing checks, WHOIS/rules/ML enrichment.
- **Mobile Agent**: planned; wire protocol partly defined.
- **Users Portal/User Layer UI**: planned/design artifact.

## 3. New Information Added by the Uploaded Documents

The uploaded documents add or clarify the following requirements beyond the existing authoritative specification.

### 3.1 Three-Layer Fraud Protection Model

The system model must explicitly distinguish Device Layer, User Layer, and Intelligence Layer. The existing spec already mentions device and user analysis, but the new documents make the three-layer model a core product requirement.

### 3.2 TrackUrlAlert and Track Mode

The browser extension must support tracked-domain behavior:

- Maintain a local `TrackedDomains` list received from the backend.
- Default mode is `TrackMode.Surf`, where the extension reports URL navigation and suppresses duplicate reports per domain for a configured interval.
- `TrackMode.Click` reports clicks and navigations on tracked domains.
- For tracked domains, the extension must emit the report type specified by the backend, such as `UrlAlert` or `TrackUrlAlert`.
- A `TrackUrlAlert` must include `Timestamp`, `DeviceUid`, `Url`, optional `FromUrl`, optional `Duration`, `ScamInProgressKey`, `IPAddress`, `UserAgent`, optional `TabId`, and optional `Timezone`.

### 3.3 Extended URL / Tracked URL Analysis

The backend must handle a tracked/extended URL alert path:

- `RealTimeAlertListener` must receive the new alert type and set `ReceivedAt`.
- `AlertPersistenceActor` must persist tracked/extended URL alerts.
- `UDAnalysis` must route tracked/extended URL alerts to a dedicated analyzer.
- `AnalysisPersistenceActor` must persist corresponding analysis results.
- `ASView` must update in-memory state for tracked/extended URL alerts and analysis results.

Naming must be normalized during implementation because the source documents use several variants: `TrackUrlAlert`, `TrackUrAlert`, `ExtendedUrlAlert`, and `ExtendedUrlReport`.

### 3.4 FraudUrlTracker, RiskyUrl, and RiskyUrlPages

The backend must include a new background service called `FraudUrlTracker`.

Responsibilities:

- Subscribe to a new domain event `RiskyUrlFound`.
- Persist suspicious URL discoveries into `RiskyUrls`.
- Crawl or analyze the same site/subdomain using a new `RiskyUrlAnalyzer`.
- Persist fetched page content into `RiskyUrlPages`.
- Raise `RiskyUrlPagesAdded` after new pages are saved.

Required data concepts:

- `RiskyUrl`: key, URL, domain, source user key, creation date, deletion state.
- `RiskyUrlPage`: key, URL, domain, text content, HTML content, risky URL key, creation date, deletion state, locale, optional proxy.
- `ASView.RiskyUrlPages`: in-memory list updated from `RiskyUrlPagesAdded`.

### 3.5 RiskyDomain and RiskyDomainPages

When a scam journey identifies a dangerous domain, the system must:

- Add the domain to the user's tracked domains.
- Persist the domain in `RiskyDomains`.
- Crawl or discover additional pages on that domain.
- Store page text and scan metadata in `RiskyDomainPages`.
- Distribute updated `SetTrackedDomains` notifications to all user devices.

### 3.6 ScamInProgress and Scam Journey

The User Layer must model scam journeys as durable state.

Required concepts:

- `ScamInProgress`: key, scam type, creation time, trigger device or analysis key, confidence, and ordered progress items.
- `ScamProgressItem`: key, previous item key, device UID, timestamp, URL/from URL, sequence, risky content category, and journey item type.
- `ScamType`: at minimum `Investment`, `TechSupport`, `Recovery`, `Romance`, and `Unknown`.
- `ScamProgressItemType`: at minimum `Ad`, `PersonalDetailsForm`, `PersonalDetailsFormSubmit`, `IncomingCallFakeNumber`, `RemoteAccessStarted`, `PaymentAttempt`, and `Unknown`.
- `RiskyContentCategory`: bait/form/phishing categories, including investment, tech support, recovery, and phishing forms.

The system must open or update a `ScamInProgress` when:

- A URL analysis result identifies investment-scam bait, tech-support bait, recovery-scam bait, or a risky form.
- A tracked-domain interaction shows movement from a bait page to a lead form or registration flow.
- A suspicious or fake phone number is associated with an active fraud flow.
- Remote access begins during an existing scam journey.

When a scam journey starts or changes, the backend must raise `ScamInProgressAdded` or an equivalent event and update tracked-domain instructions for the user's devices.

### 3.7 Investment Scam Detection Journey

The system must support the following investment-scam journey:

1. User sees an ad linking to a bait article.
2. The article is classified as a bait page.
3. The article links to a lead form.
4. The user clicks from the bait page to the lead form.
5. The lead form page is classified as suspicious.
6. After details are submitted, a conversion agent calls the user.
7. The first call requests a small initial deposit, typically 250-1000 USD.
8. A retention agent later performs a KYC-style call.
9. The scammer pushes the user to allow remote access.
10. During remote access, the user visits banking, trading, crypto, or other financial sites.
11. The system correlates remote access, sensitive sites, risky domains, and journey state to raise user risk and execute protective actions.

The system currently does not detect the first phone call unless phone intelligence or device telemetry is available.

### 3.8 Recovery Scam and Tech-Support Scam Detection

The system must recognize:

- **Recovery scam**: starts with proactive phone contact; the user may already appear on dark-net or fraud lead lists.
- **Tech-support scam**: starts with a browser popup or page claiming a false computer problem; the popup/page must be classified as a tech-support threat category.

### 3.9 User Risk Profile and User Risk Scoring

The User Layer must maintain a `UserRiskProfile` and user-level score. The enhanced user model must include:

- User risk profile.
- Current risk score.
- Immediate danger state.
- Cross-platform lock state.
- Active scams in progress.
- Tracked domains.
- Unified URL history.
- Phone-call history.
- Remote-access history.
- Behavior statistics.
- `IsScammed`, set by user self-report at onboarding or profile update.
- `IsTargeted`, updated by backend intelligence when the user appears on lead/targeting lists.

Risk scoring must consider:

- Historical vulnerability and exposure.
- Risky URLs.
- Suspicious calls.
- Active inbound remote access.
- Active scam journeys and confidence.
- Time decay for older events.

Default settings from the uploaded User Layer document:

- `AggregationPeriodDays`: 30.
- `TimeDecayFactor`: 0.95 daily.
- `NormalizationCap`: 100.

### 3.10 Immediate Danger

The first required immediate-danger scenario is:

1. A user device has an active inbound remote-access session.
2. The same device has an open browser tab or app on a sensitive site, and the user is logged in.

Sensitive scopes include banking, payment, crypto, government, tax, trading, and investment sites/apps.

When immediate danger is detected, the system must:

- Set user risk score to critical.
- Mark the user as in immediate danger.
- Send protective actions to the specific device and, when configured, to other devices and contacts.
- Apply user preferences and system policy.

### 3.11 Protective Actions

Protective actions must be configurable at both system and user level.

Potential actions include:

- Visual warning.
- Audio warning.
- Browser warning banner.
- Modal alert.
- Push notification.
- Page blocking.
- Detailed tracking activation.
- Remote-access disconnect.
- SMS/email/WhatsApp notification to user or emergency contact.
- Cross-platform lock.
- Browser lock.
- Black-screen/redaction mode.
- OTP interception warning or blocking.

Suggested risk bands:

- 0-20: passive monitoring.
- 21-40: browser warning banner.
- 41-60: push/modal warning and detailed tracking.
- 61-80: page block, remote-access disconnect, emergency contact notification.
- 81-100: cross-platform lock, black screen/redaction, browser lock.

### 3.12 OTP Interception and Black Screen

The User Layer design introduces two advanced protective capabilities:

- **OTP interception**: correlate an SMS OTP received on a mobile device with browser input during remote access; block exposure or warn the user.
- **Black screen/redaction**: browser extension hides sensitive DOM elements from the remote party while preserving local user visibility where technically possible.

These are new planned requirements and are not described as built in the current authoritative spec.

### 3.13 PhoneAlert and Phone Intelligence

The Device Layer must support phone events:

- Incoming call checks against blacklisted numbers.
- Fake-number/VOIP/country checks.
- Outgoing call checks against blacklisted numbers.
- Phone events may create or update `ScamInProgress`.

New required entity:

- `BlacklistedPhoneNumber`: timestamp, country code, area code, number, creation/deletion fields, source, deletion state.

### 3.14 Message-Link Monitoring

The system must support malicious-link detection in messages:

- Email inbox scanning at a user-configurable interval.
- SMS scanning on mobile when new SMS messages arrive.
- WhatsApp scanning on mobile when messages arrive, subject to platform feasibility and permissions.
- URL/domain checks should reuse the URL analysis pipeline.

Configuration keys include `EmailScanIntervalMin` and user/system exceptions for domains and phone numbers.

### 3.15 Intelligence Layer

The Intelligence Layer must support:

- Dark-net or external lead-list intelligence indicating that a user's personal details are being sold or targeted.
- Malicious/risky domain and URL intelligence.
- Fake or suspicious phone-number intelligence.
- `UserIsTargetedAlertReceived`, raised once when a previously untargeted user is found in targeting lists, after which `User.IsTargeted` becomes true.

### 3.16 Cloaking Detection

The URL analyzer must support optional cloaking checks:

- New configuration flag: `CheckCloaking`.
- Fetch the same URL through different proxies/geographies.
- Compare content and intent.
- Add cloaking indicators to the URL analysis result.
- Browser identity must be realistic enough for sites that serve different content to bots.

### 3.17 Notification Persistence and Acknowledgement

The backend must track notifications and commands sent to devices:

- Persist or buffer sent notifications per device.
- Require device ACKs.
- Retry unacknowledged notifications when a device reconnects.
- Recover pending notifications after backend restart.
- Use limits such as `MaxForDevice` and `OutdateAge` from appsettings or `SystemConfiguration`.

If the User Layer expected an extended/tracked URL report but only receives a regular URL alert, it must re-send `TrackMode=Click` instructions.

### 3.18 Extension Configuration

The extension must support configuration delivered by backend through the agent:

- `UrlAlertSilenceIntervalMinutes`.
- `HighRiskThreshold`.
- `LowRiskThreshold`.
- `Version`.

Startup behavior:

- Extension requests configuration through the agent.
- Backend may send `SetExtensionConfiguration`.
- Agent stores the extension config.
- Extensions fetch config from the agent.
- If running without an agent, extension uses local defaults.

### 3.19 Agent Changes

The Desktop Agent must:

- Pass `Timezone` to extensions in keep-alive messages.
- Include `Timestamp` in `RemoteAccessAlert`.
- Include `Version` in `DeviceInfo`.
- Receive and forward `SetExtensionConfiguration`.
- Store local extension config and reject lower config versions.
- Forward backend notifications to extensions.
- Handle version update requirements.

### 3.20 Version Control and Auto Update

Every software component must expose a version:

- Backend.
- WebApi.
- Admin UI.
- Desktop Agent.
- Extension.
- URL analyzers.
- Documentation/versioned release notes where relevant.

Auto-update requirement for Windows Desktop Agent:

- `UserDevice` and `DeviceInfo` include `Version`.
- Backend compares device version against `LatestVersion_Agent_Win`.
- If outdated, backend returns `VersionUpdateRequired`.
- Agent stores the triggering message and subsequent messages until upgrade completes.
- Agent downloads update from `DownloadPath`, sends `VersionUpdateRequest`, restarts into the new version, and resends the triggering message.
- Backend validates update requests by `VersionRequestId` and updates `UserDevice.Version`.

### 3.21 Admin Tools and Simulation

Admin must support:

- Clear Cache.
- Initialize View.
- Device Alert Simulator.
- Simulation list with filtering, sorting, edit, delete, run.
- Simulation create/edit with ordered `SimulationStep` records.
- Simulation steps for `UrlAlert`, `RemoteAccessAlert`, and `TrackUrlAlert`.
- Step delay, selected user/device, dynamic form fields per alert type, and drag-and-drop order.
- Steps persisted as JSON.

### 3.22 Archive Service

The backend must include an archive/background service:

- Move expired `UrlAnalysisResult` records to archive tables.
- Move related `DeviceAlerts` to archive tables.
- Use configurable expiration such as `UrlAnalysisResultExpirationDays`.

## 4. Conflicts, Differences, and Open Clarifications

### 4.1 Runtime Version Conflict

The current authoritative specification says the backend is .NET 8 and already built. The uploaded `My_Backend_System` document asks for .NET 10 and a Visual Studio 2022 solution. Treat .NET 8 as the current built baseline and .NET 10 as a future modernization decision that requires explicit approval.

### 4.2 Admin Frontend Conflict

The current system has a built Razor Pages admin portal. The uploaded backend document mentions an Angular frontend and user/admin sections. Treat Angular as a future client requirement, not a replacement of the built Razor Pages admin unless a migration is explicitly approved.

### 4.3 TrackUrlAlert Naming

The uploaded documents use inconsistent names: `TrackUrlAlert`, `TrackUrAlert`, `ExtendedUrlAlert`, and `ExtendedUrlReport`. The recommended canonical name is `TrackUrlAlert` for the device alert and `TrackUrlAnalysisResult` for the result. Legacy aliases should be mapped only if needed.

### 4.4 Risk Threshold Wording

One uploaded section states that `RiskyUrlFound` is raised when score is lower than `RiskyUrlScoreThreshold`; other sections say when the score exceeds or breaches the threshold. The intended behavior appears to be: raise `RiskyUrlFound` when risk score is greater than or equal to the configured threshold. This needs confirmation before implementation.

### 4.5 Existing vs Planned Capabilities

The current spec marks Backend, WebApi, Desktop Agent, Extension, and URL Analyzer as built. The new documents add many planned capabilities that are not necessarily implemented:

- PhoneAlert.
- SMS/WhatsApp scanning.
- OTP interception.
- Black-screen/redaction mode.
- FraudUrlTracker.
- RiskyUrlAnalyzer.
- RiskyDomain crawler.
- Notification persistence with ACK/retry.
- Version auto-update.
- Admin simulation builder.
- Archive service.
- Intelligence Layer lead-list integration.

## 5. Consolidated Functional Requirements

### FR-001 Device Alert Intake

The backend must receive device alerts over the real-time listener, validate device token/session where applicable, set `ReceivedAt`, persist raw alert data, update ASView, and route events to the relevant analysis pipeline.

### FR-002 URL Alert Analysis

The system must analyze URLs for risk, phishing, blacklist status, domain reputation, WHOIS/registration factors, page content, category, scam type, and optionally cloaking.

### FR-003 Track URL Alert Analysis

The system must analyze tracked-domain clicks, navigations, form interactions, source URL, page duration, and scam journey association.

### FR-004 Remote Access Analysis

The system must detect active remote-access applications, direction, connection/session status, associated browser tabs, and sensitive-site overlap.

### FR-005 Phone Alert Analysis

The system must analyze incoming and outgoing phone numbers for blacklist, fake-number, VOIP, and geographic risk signals.

### FR-006 User-Level Correlation

The User Layer must correlate URL, tracked URL, phone, remote access, message-link, and intelligence events across all devices belonging to a user.

### FR-007 Scam Journey Tracking

The User Layer must create, update, and persist scam journeys as `ScamInProgress` with ordered progress items and confidence.

### FR-008 User Risk Scoring

The system must maintain a normalized user risk score from 0 to 100 using configurable weights, time decay, active threats, and user-specific profile factors.

### FR-009 Immediate Danger

The system must detect immediate danger when inbound remote access coincides with logged-in sensitive financial/government/trading/crypto context on the same device.

### FR-010 Protective Actions

The system must compute protective actions from system policy, user preferences, risk score, scam type, device type, and immediate-danger state.

### FR-011 Multi-Device Notifications

The backend must send relevant notifications and commands to the triggering device and, when required, to all other devices of the user.

### FR-012 Notification Reliability

The backend must track notification ACKs, retry unacknowledged commands, and restore pending delivery after restart.

### FR-013 Tracked Domains Distribution

The backend must send `SetTrackedDomains` to all user devices whenever risky/scam domains are added, updated, or cleared.

### FR-014 Risky URL Discovery

The backend must raise `RiskyUrlFound` for high-risk non-cached URL results and process them through `FraudUrlTracker`.

### FR-015 Risky Domain Discovery

The backend must persist risky domains and crawl/analyze additional pages for fraud evidence and journey classification.

### FR-016 Message Link Scanning

The system must support scanning of links from configured email accounts, SMS, and WhatsApp where technically and legally permitted.

### FR-017 Intelligence Targeting Alert

The system must mark users as targeted when external intelligence indicates their details appear in fraud lead lists, raising only the first alert for a previously untargeted user.

### FR-018 Extension Runtime Configuration

The backend/agent/extension chain must support versioned extension configuration and local fallback defaults.

### FR-019 Component Versioning

Every component must expose and report its version. Admin UI must display backend, WebApi, admin client, agent, and extension versions where available.

### FR-020 Desktop Agent Auto Update

The backend and desktop agent must support enforced update flow for outdated agent versions.

### FR-021 Admin Simulation

Admin must allow operators to define, edit, store, and run multi-step simulations of device alerts over time.

### FR-022 Archive and Retention

The backend must archive expired alerts and analysis results according to configurable retention periods.

## 6. Consolidated Data Requirements

The following new or extended data objects are required:

- `DeviceAlert`: add `Timezone`, `ReceivedAt`.
- `UrlAlert`: add `Timestamp`, `TabId`, `Timezone`.
- `TrackUrlAlert`: new alert type with URL, source URL, duration, scam key, user agent, tab ID, timezone.
- `ExtendedUrlAlertEntity` or canonical `TrackUrlAlertEntity`.
- `User`: add `IsScammed`, `IsTargeted`, locale/timezone where absent.
- `UDUser`: add risk profile, risk score, immediate danger state, cross-platform lock, scams in progress, tracked domains, URL/phone/remote history, behavior stats.
- `UserRiskProfile`.
- `ScamInProgress`.
- `ScamProgressItem`.
- `TrackedDomain`.
- `RiskyUrl`.
- `RiskyUrlPage`.
- `RiskyDomain`.
- `RiskyDomainPage`.
- `BlacklistedPhoneNumber`.
- `ExtensionConfiguration`.
- `SystemConfiguration` with JSON configuration and version.
- `Simulation`.
- `SimulationStep`.
- `VersionUpdateRequired`.
- `VersionUpdateRequest`.
- `NotificationPersistence` or equivalent delivery tracking entity.

## 7. Consolidated Event Requirements

The event model must include or extend:

- `DeviceAlertReceived`.
- `AnalysisResultReceived`.
- `RiskyUrlFound`.
- `RiskyUrlPagesAdded`.
- `RiskyDomainFound`.
- `ScamInProgressAdded`.
- `ImmediateDangerDetected`.
- `ImmediateDangerEnded`.
- `OtpInterceptionTriggered`.
- `BlackScreenActivated`.
- `SetTrackedDomains`.
- `UserIsTargetedAlertReceived`.
- `UserDeviceChanged`.

## 8. Consolidated Configuration Requirements

System-level configuration must include:

- `RiskyUrlScoreThreshold`.
- `HighRiskThreshold`.
- `MediumRiskThreshold`.
- `LowRiskThreshold`.
- `UrlAlertSilenceIntervalMinutes`.
- `UrlAnalysisResultExpirationDays`.
- `RiskyDomainPageScrapingExpirationDays`.
- `EmailScanIntervalMin`.
- `CheckCloaking`.
- `AggregationPeriodDays`.
- `TimeDecayFactor`.
- `NormalizationCap`.
- `LatestVersion_Agent_Win`.
- Notification retry/retention limits such as `MaxForDevice` and `OutdateAge`.
- System defaults for protective actions by risk band and scenario.
- Global exceptions for domains and phone numbers.

User-level configuration must include:

- Protective action preferences by risk band and scenario.
- User-specific domain and phone exceptions.
- Locale and timezone.
- Configured email accounts for scanning.
- Emergency contact notification preferences.

## 9. Non-Functional Requirements

### Reliability

- Notification delivery must survive device disconnects and backend restarts.
- User risk state and scam journey state must be reconstructable from persisted data.
- Background crawlers/analyzers must be idempotent for repeated URL/domain discoveries.

### Security

- Device communication over external channels must remain token-validated and encrypted where currently defined.
- Remote update download must validate `VersionRequestId` and prevent unauthorized package retrieval.
- Crawled content and user message scanning must be stored with clear retention controls.
- Sensitive user/account data must be protected from overexposure in admin, logs, and notifications.

### Privacy and Consent

- Email, SMS, WhatsApp, OTP, and message-link scanning require explicit user consent and platform-compliant permissions.
- Emergency contact notifications must follow user configuration.
- Sensitive DOM redaction/black-screen features must be limited to clearly defined immediate-danger conditions.

### Observability

- Admin must expose versions, simulation runs, notification delivery state, and cache/view management tools.
- Critical events such as immediate danger, update required, scam journey started, and targeted-user intelligence must be auditable.

## 10. Recommended Implementation Order

1. Normalize terminology and contracts for `TrackUrlAlert`, `TrackedDomain`, `TrackMode`, and risk thresholds.
2. Add persistence/entity migrations for new alert fields, tracked URL alerts, scam journeys, risky URLs/domains, and blacklisted phone numbers.
3. Implement backend event flow for `RiskyUrlFound`, `ScamInProgressAdded`, and `SetTrackedDomains`.
4. Update extension and agent contracts for tracked domains, timestamps, tab IDs, timezone, and configuration.
5. Implement User Layer risk profile, scam journey state, and immediate-danger detection.
6. Implement notification ACK/retry persistence.
7. Add FraudUrlTracker/RiskyUrlAnalyzer and risky-domain page collection.
8. Add admin simulation tooling.
9. Add version display and desktop agent auto-update.
10. Add optional Intelligence Layer integrations, message scanning, OTP interception, and black-screen/redaction after consent and platform feasibility are finalized.

## 11. Source Documents

Uploaded on 2026-07-15 and stored under `/root/KnowledgeEngine/documents/specifications`:

- `ASPS-System-Specifications---0309d751-df4c-4945-ab0e-f2528b703c34.docx`
- `My_Backend_System---889c96c0-e268-490b-84ce-6739d0d35bb4.docx`
- `שלבים_בזיהוי_הונאות_השקעות---bdc4b79d-0c1d-4e66-bdbb-e51b14d55f47.docx`
- `מסמך_ארכיטקטורה_שכבת_המשתמש_User_Layer_במערכת_ASPS_גרסה_מורח---d2f8bda2-fbce-4885-a447-b4cd2fc6a6af.docx`

Existing baseline:

- `/root/KnowledgeEngine/documents/system-specifications/ASPS_System_Specification.md`
- `/root/KnowledgeEngine/documents/system-specifications/ASPS_System_Overview.md`
- `/root/KnowledgeEngine/documents/system-specifications/My Backend System.md`
- `/root/KnowledgeEngine/documents/system-specifications/מסמך ארכיטקטורה_ שכבת המשתמש (User Layer) במערכת ASPS – גרסה מורחבת.md`
- `/root/KnowledgeEngine/documents/system-specifications/מערכת ההגנה מפני הונאות של ASPS.md`

# ASPS — Anti-Scam Protection System

## Product Specification

> A real-time, on-device protection system that detects and stops online scams **as they happen** — across browsing, SMS, email, and remote-access channels.

**Version:** 0.0.0.4 (March 30, 2026)
**Audience:** product / business / partners. Engineering-level detail lives in [ARCHITECTURE.md](ARCHITECTURE.md) and [docs/ASPS_DATA_FLOW.md](docs/ASPS_DATA_FLOW.md).

---

## Table of Contents

1. [The Problem](#1-the-problem)
2. [Vision & Mission](#2-vision--mission)
3. [Target Users](#3-target-users)
4. [What ASPS Does Today](#4-what-asps-does-today)
5. [What's Coming Next](#5-whats-coming-next)
6. [Differentiators — Why ASPS](#6-differentiators--why-asps)
7. [Business Model & Pricing](#7-business-model--pricing)
8. [Funding & Roadmap Stages](#8-funding--roadmap-stages)
9. [Success Metrics (KPIs)](#9-success-metrics-kpis)
10. [Risks & Mitigations](#10-risks--mitigations)
11. [Compliance & Trust](#11-compliance--trust)
12. [Glossary](#12-glossary)

---

## 1. The Problem

Online scam losses have outpaced almost every other form of consumer fraud. Common patterns:

- **Tech-support scams** — fake "Microsoft / your bank called" pop-ups, ending in remote access + wire transfer.
- **Investment scams** — bogus crypto / forex platforms, often promoted via Facebook / Instagram.
- **Bank impersonation** — phishing sites and SMS that mimic the user's real bank's domain or phone number.
- **Romance / pig-butchering** — long-running social engineering culminating in financial control.

The shared pattern across all of them: **a window of minutes between "first warning signs" and "money is gone"**. Existing protections (browser-built-in safe-browsing, antivirus, bank fraud teams) act *too late* — they react to a click that already happened, or a transfer that's already in flight.

**ASPS is built to catch the moment the user is being scammed — and stop the action before money moves.**

---

## 2. Vision & Mission

**Vision:** A world where being online doesn't mean being one click away from financial ruin.

**Mission:** Give every internet user — and especially the most vulnerable (elderly, immigrants, recent-tech-adopters) — an always-on guardian that:

1. Recognizes scam patterns in real time, across all channels (browser, SMS, email, phone, remote access).
2. Intervenes *before* the user takes a destructive action — not after.
3. Notifies a chosen "protector" (family member, social worker, NPO contact) when intervention is needed.
4. Stays **on the user's device** — content never leaves the device unless the user agrees, and analysis is local-first.

---

## 3. Target Users

### 3.1 Direct end-users (B2C)

| Segment | Why ASPS matters | How they find us |
|---------|------------------|------------------|
| **Older adults (65+)** | #1 victims of tech-support / romance scams; often unfamiliar with phishing patterns | Family member installs for them; NPO partnerships |
| **New immigrants** | Language barriers; unfamiliar with local banks → hard to spot impersonation | Community organizations, embassies, MFA partnerships |
| **Tech-anxious adults** | Aware of scams but unsure which signal to trust | Direct search, recommended by family |
| **Bereaved / recently-divorced** | Statistically a high-risk window for romance scams | NPO referrals, care providers |

### 3.2 Indirect stakeholders ("Protectors")

ASPS is designed to support **two-sided households** — a parent / spouse / NPO contact who installs the agent on a vulnerable family member's device and gets notified on critical events.

### 3.3 B2B partners

| Partner | What we offer | What they offer |
|---------|---------------|-----------------|
| **Banks** | Pre-transaction warnings about scam-in-progress (a customer browsing a known-scam site, or active remote-access during a wire transfer) | Customer enrollment channel, white-label deployment, bank-website whitelist data |
| **Credit / insurance** | Risk signals during application flows | Volume, fraud-loss subsidy |
| **NPOs (e.g., Eshelnet, JDC)** | Free / subsidized seats for at-risk populations | Trust, education channels, government / philanthropic funding |
| **MDM / MAM** | Mobile-device deployment for enterprise / family plans | Distribution at scale |

---

## 4. What ASPS Does Today

### 4.1 URL & website scams (Chrome extension)

- **Real-time URL scoring** on every page navigation (cached, ~50ms overhead on cache hit; ~1-2s on full analysis)
- **Phishing detection** against a 500K+ row database of known phishing URLs and domains
- **ML-based novel-phishing detection** — domain age, content patterns, brand impersonation, urgency language
- **Bank-website whitelist** — confirms a domain really is the user's bank (prevents typosquat phishing)
- **Sensitive-site categorization** — banking / crypto / healthcare flagged separately for extra scrutiny

### 4.2 Tech-support scam detection

- **Remote-access app awareness** — recognizes AnyDesk / TeamViewer / etc. running
- **Combined signal** — "remote access + sensitive site = immediate danger" → **Immediate Danger** alert with high-priority protective action

### 4.3 Long-duration tracking

- **Tracked URLs** — flags users spending unusual time on suspicious pages
- **Scam-in-progress detection** — known sequences (e.g., "Microsoft warning page → call number → AnyDesk install") fire dedicated alerts

### 4.4 Per-user risk profile

- **UserRiskProfile** — real-time, time-decayed risk score per user (introduced in v0.0.0.3, JIRA: ASPS-258)
- Aggregates URL, behavior, device, and history signals

### 4.5 Protective actions (10 distinct types)

| Effect | When | Channel |
|--------|------|---------|
| Display banner / overlay | Medium-risk URL | Browser tab |
| Modal warning | High-risk URL | Browser tab |
| Block page | Critical-risk URL | Browser tab (replaces page) |
| Sound alert | High urgency, user not looking | OS notification |
| Email notification | High severity, user has linked protector | SMTP |
| Block remote-access | Immediate-danger | Desktop terminates session |
| Quarantine device | Repeated risk events | Backend marks device for admin review |
| Track URL | Flagged but not yet conclusive | Backend extends monitoring |

### 4.6 Admin tooling (WebApi)

- **Live dashboard** with SignalR — counts of users, devices, alerts, recent activity
- **CRUD admin pages** for: users, devices, alerts, analysis results, phishing DB, **bank websites**, **blacklisted phone numbers**, **website categories**, tracked domains, simulations, system configuration
- **Roadmap editor** — multi-project planning tool, exports a self-contained HTML "Viewer" that anyone can open offline

### 4.7 Languages

Hebrew + US English fully supported. Russian, UK English, French, German planned for the **Angel** stage; Spanish, Arabic, and others for **VC** stage.

---

## 5. What's Coming Next

### 5.1 Mobile agents (target: Angel stage)

- **Android agent** — full feature parity with Desktop where the OS allows: URL detection (via Accessibility), SMS scanning (BroadcastReceiver), call screening (CallScreeningService), remote-access app detection. Sprint plan in [ARCHITECTURE.md §16.6](ARCHITECTURE.md#16-mobile-agents--specification-to-be-built).
- **iOS agent** — degraded but useful: URL filtering via Network Extension, call directory sync, message-filter extension. Battery-friendly. Limitations called out in [§16.7](ARCHITECTURE.md#167-open-questions).

### 5.2 New alert channels

- **SMS scanning** (Android — iOS is OS-restricted)
- **Email scanning** (via OAuth into Gmail / Outlook — both platforms)
- **Phone-call scanning** — incoming-number lookup against `BlacklistedPhoneNumbers` (already in DB; mobile agent will consume it)
- **App-install monitoring** — flags installation of unknown remote-access apps on Android

### 5.3 Voice-clone / synthetic-voice detection (VC stage)

The next horizon: scam phone-calls now use cloned voices ("It's me, your son, I need money"). VC-stage R&D investigates either a model trained in-house or licensed API.

### 5.4 Network-effect features

- **Cross-customer dangerous-domain propagation** — when one customer's domain is flagged as scam, all other customers gain protection within minutes
- **Lead lists** — early-warning database shared with banks and partners
- **Community Services** — opt-in feedback loop where protectors can quickly mark "this was a real scam" / "false alarm" to improve detection

### 5.5 Trust and education

- **Education Center** — articles, blog, video, TV reach (VC stage)
- **PR / press partnerships** — currently being scoped
- **Customer support** — hybrid AI + human model for the Angel stage; full multi-channel at VC

---

## 6. Differentiators — Why ASPS

| Competitor type | What they do | What ASPS does differently |
|-----------------|--------------|---------------------------|
| Browser safe-browsing (Google, Microsoft) | Block known-malicious URLs from a centralized list | **On-device ML** detects novel phishing; **cross-channel** (URL + remote-access + SMS); **immediate-danger** combination signals |
| Antivirus suites | File-level malware detection | Behavior + scam-context — not file scanning. Protects users who never download malware but still get scammed |
| Bank fraud teams | Detect anomalies post-transfer | ASPS warns *before* the transfer is initiated |
| Identity-theft monitors (LifeLock, etc.) | Notify after credentials are leaked | ASPS prevents the credential-handover from happening in the first place |
| Family-control software | Time limits, content blocking | Active scam detection; designed for adults, not children |

**The strategic moat:** Combined-signal analysis. No competitor we've found integrates URL + remote-access + sensitive-site context in a single immediate-danger event. The more customers we have, the better that signal becomes (network effect on the dangerous-domain propagation).

---

## 7. Business Model & Pricing

### 7.1 Revenue streams

| Stream | Status | Notes |
|--------|--------|-------|
| **B2C subscriptions** | TBD pricing — not yet live | Per-user, per-month; freemium consideration for under-18 / over-80 |
| **B2B partnerships (banks, insurance)** | Pre-Angel | Per-seat or per-prevented-loss revenue share |
| **B2NPO** (subsidized seats) | Pre-Angel | Cost-coverage model + grants |
| **API / data licensing** | VC stage | Licensing the dangerous-domain feed and risk signals to other security products |

### 7.2 Pricing decisions still open

- Free tier vs. paid-only?
- Family-plan structure (2-5 seats vs. unlimited)?
- B2B pricing — flat per seat vs. value-share on prevented losses?
- Pre-Angel: target a tiny pilot at zero/discount price to gather data; monetize at Angel.

### 7.3 Customer acquisition

| Stage | Channel | Notes |
|-------|---------|-------|
| Pre-Angel | NPO partnerships, family referrals | Trust > marketing budget |
| Angel | First paid acquisition (Google / Facebook / community press) | Tied to PR launch |
| VC | Mass-market, partner-sponsored, white-label | Bank-channel deployment |

---

## 8. Funding & Roadmap Stages

ASPS planning runs on three stages — **Now**, **Angel**, **VC** — captured in the live admin tool at `/Roadmaps`. Highlights:

### 8.1 Pre-Angel (Now)

What's required before raising:

- Working MVP — already done (Backend + Desktop + Extension + 500K phishing DB)
- Onboarded 5+ pilot users / NPO partner
- Revenue model + first signed pilot agreement
- Pitch deck + financials

**Open in Now:** customer-billing infrastructure (the only Now-stage feature that hasn't been classified), legal / business entity, IP / privacy basics.

### 8.2 Angel (target raise)

What the Angel money is for — four buckets:

1. **Product completion** — Mobile agents (Android + iOS basic), additional languages, WhatsApp scanning, upgraded escalation
2. **Information security** — ISO-27001 (9-12 months), SOC 2 Type I (6-9 months), CCPA, GDPR, Pentests
3. **Production infrastructure** — Pipeline, QA, Cloud, Backup, Recovery, Scalability, Automation
4. **Legal & insurance** — Terms-of-service, privacy policy, regulatory positioning, professional liability insurance

Estimated runway: **9 months** (subject to refinement during deck prep).

### 8.3 VC (target raise)

Scale-up:

- iOS feature parity, Browser Extension everywhere (Edge, Firefox, Safari)
- Synthetic-voice detection
- Educational center (articles, video)
- Community services
- Load testing for hundreds of thousands of users
- Spanish / Arabic / additional locales
- Hiring scale-up — engineering, ML, BD, NPO partnership manager

---

## 9. Success Metrics (KPIs)

To be tracked from day one:

| Metric | Definition | Target (Angel close) | Target (VC close) |
|--------|------------|----------------------|-------------------|
| **Active users** | Devices reporting in last 7d | 1,000 | 100,000 |
| **Prevented incidents** | Critical-action events fired (banner / block / quarantine) | 50/week | 5,000/week |
| **False-positive rate** | User-reported wrong alerts / total alerts | < 5% | < 2% |
| **Time-to-detection** | Median time between first scam signal and protective action fired | < 5s | < 2s |
| **Coverage of bank-impersonation domains** | Bank-domain whitelist size | 100 banks (IL/US) | 500 banks (global) |
| **Phishing-DB hit rate** | % of UrlAlerts matched to known DB | 30%+ | 50%+ |
| **NPS** | Among end users | > 30 | > 50 |
| **Customer retention** | Monthly retention | > 90% | > 95% |

---

## 10. Risks & Mitigations

| Risk | Why it matters | Mitigation |
|------|----------------|------------|
| **iOS limitations** | iOS blocks the OS hooks that make Android effective (SMS, remote-access detection). | Build Android-first; iOS as "URL + call + email" only. Document limitations to user honestly. |
| **Battery drain on mobile** | Always-on monitoring kills user trust faster than scam losses. | Use FCM / APNs as wakeup; switch ZMQ SUB to short-lived poll on mobile. Measured budget required. |
| **App-store review** | URL filters often rejected by Apple. | Distribute via Network Extension + user-installed VPN profile, OR via MDM partners. |
| **Privacy concerns** | "Spyware-like" perception risks user pushback. | All analysis local-first. Open privacy policy. Customer can audit what leaves the device. Consider periodic transparency reports. |
| **False positives** | Even one bad alert in front of an at-risk user erodes trust. | Multi-stage scoring; user feedback loop. Cap "scary" UIs (Block, ForceClose) to high-confidence cases only. |
| **Regulatory** | EU AI Act + GDPR + IL Privacy Law all touch on user-monitoring software. | Legal counsel in Angel stage; build the consent / disclosure UX upfront. |
| **B2B sales cycle** | Bank deals can take 12-18 months. | Pursue NPO + B2C in parallel; bank deals are upside, not Angel-stage requirement. |
| **Voice-clone arms race** | Detection accuracy may lag generators. | VC-stage R&D; consider acquisition / partnership instead of in-house. |
| **Operational scale** | Hundreds of thousands of devices = serious infrastructure. | Load tests + Production pipeline (Angel-stage line items #3). |

---

## 11. Compliance & Trust

### 11.1 Standards target (Angel)

- **ISO-27001** — information-security management; baseline for B2B sales
- **SOC 2 Type I** — operational controls; baseline for US bank partnerships
- **CCPA** — California consumer privacy; required for any US footprint
- **GDPR** — EU; required if serving EU users (and to give users the option)

### 11.2 Encryption

- **End-to-end** between Desktop / Mobile and Backend over CurveZMQ (NaCl-based public-key crypto)
- **TLS** for all admin and WebApi traffic
- **At-rest encryption** in DB for sensitive fields (target Angel)

### 11.3 Open / auditable

- Privacy policy will explicitly list what data leaves the device
- Customers (especially B2B) can audit the local cache + outbound traffic
- Public bug-bounty program (target VC)

---

## 12. Glossary

| Term | Definition |
|------|------------|
| **Alert** | A signal sent from an Agent (Desktop / Mobile / Extension) to the Backend describing something the user just did |
| **Analyzer** | A backend module that examines an alert and assigns a risk score |
| **Protective Action** | An instruction the Backend sends back to the Agent telling it what UI / OS-level effect to fire |
| **Immediate Danger** | A combined-signal event (e.g. remote access + sensitive site) that triggers the highest-priority protective action |
| **Protector** | A trusted contact (family member, NPO worker) the user has linked to receive notifications on critical events |
| **Tracked URL** | A URL that the Backend continues monitoring after the initial alert, looking for sustained suspicious behavior |
| **Phishing DB** | The 500K-row database of known-bad URLs |
| **Sensitive Site** | A site whose category (banking / crypto / healthcare) makes any combined risk signal more severe |
| **Network Effect** | The model where one customer flagging a scam protects all other customers within minutes |

---

*Last updated: 2026-04-29 — initial product spec extracted from internal roadmap, completion reports, and code audit.*

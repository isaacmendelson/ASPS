# SCRUM-904 — User Risk Score (URS) — Design

**Goal:** a principled, deep, and actionable definition of *User Risk Score* —
what it is, what feeds it, how it's computed, how the weights are set and
later corrected. Starting point: the user's initial thoughts in JIRA SCRUM-904
comments (Hebrew + English) and the existing
[UserRiskProfile.cs](../ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UserRiskProfile.cs)
implementation (ASPS-366 / ASPS-367).

---

## 1. What is the User Risk Score?

URS is **not just a number** — it is a **structured assessment** of which a
single 0–100 scalar is the public face. The decomposition matters as much as
the scalar:

```
UserRiskScore
├── score:            int, 0–100              ← the headline number
├── level:            "Low"|"Elevated"|"High"|"Critical"   ← band on the score
├── confidence:       0.0–1.0                 ← how much data backed this
├── computed_at:      datetime
├── valid_until:      datetime (refresh deadline)
│
├── axes
│   ├── vulnerability_score   0–100   ← slow-moving "how prone is this user"
│   └── exposure_score        0–100   ← fast-moving "what is hitting them now"
│
├── dimensions (per-dimension subscores 0–100)
│   ├── inbound_attack_vector       ← messages, calls, leak lists
│   ├── observed_behavior            ← what they did with what they saw
│   ├── live_threat_indicator        ← things on right now
│   └── cross_modal_correlation      ← suspicious overlap of signals
│
├── contributing_signals[]   ← the individual events that drove this number
│   each has: signal_type, weight, value, timestamp, decayed_contribution
│
├── explanation:      string   ← human-readable top-3 reasons
├── recommended_actions[]      ← protective actions implied by this score
└── data_sources_active[]      ← {source, consent_level, last_observed_at};
                                  drives confidence and the renormalization
                                  of weights across permitted sources
```

`confidence` is **consent-aware**: it reflects not just data freshness but
the *breadth* of permitted sources. A user who opts out of message
monitoring gets a URS computed only from what they allowed, and the
confidence honestly reports the reduction — the system never fabricates a
signal from data it isn't allowed to read.

**Why a decomposed structure and not just a scalar:**

1. **Action selection.** A URS of 75 caused by *recent inbound spoofed calls*
   triggers a different protective action than the same 75 caused by *active
   remote-access session on a banking site*. The system already makes
   action decisions; without decomposition those decisions can't be informed.
2. **Explainability.** Vulnerable users and their family/guardians must be
   able to understand *why* the system raised the alarm. A bare number is
   not explainable.
3. **Auto-correction.** When an outcome is observed (real scam confirmed /
   false alarm), the correction must attribute the error to specific
   signals. A scalar makes attribution impossible.
4. **Operational truth.** The mission is protection. "Score = 72" is not an
   operational truth; "two spoofed calls + a click on a phishing link in
   the last 48h while a banking session was open" is.

**Operational interpretation of the headline scalar:**

| Band | Range | Operational meaning |
|---|---|---|
| Low | 0–30 | Baseline. No special action. |
| Elevated | 31–60 | Increase monitoring frequency. Soft warnings on risky pages. Alert family if configured. |
| High | 61–85 | Active intervention. Hard warnings. Confirmation gates on form submits. Family notification. |
| Critical | 86–100 | Maximum-friction mode. Block sensitive-site form submits until guardian confirms. Toast escalation. ImmediateDanger flow eligible. |

The exact thresholds are tunable; they are a **product-policy decision**, not
a math fact.

---

## 2. The conceptual model

Two orthogonal axes, mediated by a third "live" stream and a fourth
correlation term. The existing `UserRiskProfile` already has the first two;
this design keeps them and expands the inputs.

```
Vulnerability ──┐
                ├──► Risk = σ( a·Vuln + b·Exp + c·Live + d·Corr − θ )
   Exposure ────┤
                │
   Live ────────┤   σ = logistic (saturates gracefully near 100)
                │   θ = bias / threshold
   Corr ────────┘
```

| Axis | What it captures | Time character | Examples |
|---|---|---|---|
| **Vulnerability** | how prone is this user to *being* scammed? | slow — months | age cohort, declared vulnerability, past click-through rate on malicious links, behavioral baseline, account value at stake |
| **Exposure** | what attack surface is hitting them *recently*? | days–weeks | inbound malicious messages, calls from spoofed numbers, presence in darknet lead lists, visits to risky domains |
| **Live** | is something dangerous happening *right now*? | seconds–hours | active remote-access + open banking, ImmediateDanger flag, recent form submit on a flagged domain |
| **Corr** | is a suspicious *combination* of signals overlapping in time? | minutes–days | spoofed-call ⟶ click within 30 min; inbound malicious link ⟶ click ⟶ form submit |

The **logistic function** for combination is the recommendation in §6.

---

## 3. Parameter catalog (the signals that feed URS)

Organized by the dimensions of §1. For each: what it is, why it matters,
**what data source supplies it** (✅ already collected, ⚠️ partly collected,
❌ not yet collected), and which axis it primarily feeds.

### 3.A — Inbound attack vector (Exposure axis)

| # | Signal | Source | Status | Notes |
|---|---|---|---|---|
| A.1 | Contact info found in darknet lead lists | external feed (need 3rd-party partner: HaveIBeenPwned + a paid lead-list monitor) | ❌ | step-change Exposure boost when first found; decays slowly |
| A.2 | Inbound malicious messages count (last 30d) | needs **message-stream ingestion** (SMS via mobile agent, email via gmail integration?) | ❌ | also need a per-message scam-likelihood score |
| A.3 | Spoofed-number calls received (last 30d) | needs **call-log ingestion** (mobile agent permission) + spoofed-number detection | ❌ | high signal for elderly-targeting fraud |
| A.4 | Inbound vector velocity (trend slope) | derived from A.2 + A.3 over rolling window | ❌ | a sharp rise = attacker focus on this user |

### 3.B — Observed behavior (Vulnerability axis)

| # | Signal | Source | Status | Notes |
|---|---|---|---|---|
| B.1 | Count of risky URL visits (last 30d, weighted by per-URL risk_score) | existing `UrlAlert` → `AnalysisResult.risk_score` | ✅ | already feeds UserRiskProfile |
| B.2 | Per-domain depth on risky domains: visits, dwell time, did a form get submitted? | extension's `TrackedDomain` + tracking data | ⚠️ | visits are tracked; **dwell time** and **form-submit detection** need extension work |
| B.3 | Click-through rate from inbound malicious messages | join A.2 (messages) with URL-visit timestamps within e.g. 6h | ❌ | the single most diagnostic vulnerability signal — depends on A.2 |
| B.4 | Sensitive-site activity (banking, trading): visits, sessions, total time | existing `SensitiveSite` table + `WebsiteCategory` | ✅ | this is "stake" rather than risk — raises *consequence* not *probability* |
| B.5 | Remote-access sessions: count, duration, concurrent sensitive-site flag | `RemoteAccessAlert` + ImmediateDanger correlation | ✅ | already in UserRiskProfile via `RemoteAccessWeight` |
| B.6 | Anomaly vs the user's own baseline (sudden spike in any of the above) | derived; requires storing a per-user rolling baseline | ❌ | "this user is acting differently than usual" |

### 3.C — Live threat indicators (Live axis)

| # | Signal | Source | Status | Notes |
|---|---|---|---|---|
| C.1 | Active ImmediateDanger session (remote-access + sensitive site) | existing `ImmediateDanger` table + flag on alerts | ✅ | already gets the heaviest weight |
| C.2 | Open remote-access session right now (without sensitive site overlap) | live `RemoteAccessAlert` state | ✅ | medium weight; raised by Live |
| C.3 | Form submit on a domain flagged in the last 10 minutes | extension form-submit signal + recent risk classification | ⚠️ | "credentials may have just been handed over" |
| C.4 | Active scam-in-progress key associated with this user | existing `ScamInProgressKey` propagation | ✅ | top-of-stack signal |

### 3.D — Cross-modal correlation (Corr axis)

| # | Signal | Source | Status | Notes |
|---|---|---|---|---|
| D.1 | Spoofed call → risky URL visit within a short window | requires A.3 + B.1 with timestamps | ❌ | classic social-engineering chain |
| D.2 | Inbound malicious link → click within window → form-submit chain | A.2 + B.3 + C.3 | ❌ | the "lure conversion" funnel; very high weight |
| D.3 | Multi-modal anomaly: simultaneous behavior change in two or more channels (calls + browsing + messages) | derived; needs B.6 across modalities | ❌ | "attacker has multiple touchpoints active" |
| D.4 | Time overlap between RemoteAccess and sensitive-site session | already in C.1 logic | ✅ | this is what ImmediateDanger is |

### 3.E — User-context modifiers (Vulnerability multipliers)

These don't move much; they modulate other dimensions. Usually set once per
user, edited by an admin/guardian.

| # | Modifier | Source | Status | Effect |
|---|---|---|---|---|
| E.1 | Age cohort (or declared "elderly") | user profile / guardian-set | ⚠️ | multiplies Vulnerability axis |
| E.2 | Declared technical anxiety / first-time digital user | user profile | ⚠️ | multiplies Vulnerability |
| E.3 | High-value account configured (banking, trading) | user profile + observed sensitive-site activity | ✅ | raises the *consequence*, indirectly amplifies Live actions |
| E.4 | Region / language (regional fraud-trend overlay) | user profile + an externally maintained fraud-trend feed | ❌ | regional weight on certain signals |

---

## 3.5. User consent and configurable privacy depth

Every data source above is **independently configurable** by the user. The
system **works with whatever is permitted** — accuracy and confidence drop
when data is missing, but URS never silently degrades to using data the
user did not allow.

### The consent ladder

Consent is **not binary**. Each data source has a level that the user picks:

| Level | What is collected | Privacy cost | Signal value |
|---|---|---|---|
| `NONE` | nothing | zero | zero |
| `PRESENCE` | count + timestamps only ("5 SMSes today; 1 flagged risky by carrier") | very low | medium |
| `METADATA` | sender / recipient / category — no content | low | high |
| `CONTENT_LOCAL` | content read but processed *only on device*; only the verdict (not the content) is sent to backend | medium | very high |
| `CONTENT_SHARED` | content uploaded to backend for richer analysis | high | maximum |

Different sources support different sets of levels — e.g. a darknet-leak
check only has `NONE`/`PRESENCE`; a URL-risk check only has `NONE`/`METADATA`
(the URL itself is the data). The level catalog is per-source.

### Default proposal at first install (the user can change anything)

A balanced "Recommended" baseline — never the maximum, never the minimum:

| Source | Default | Rationale |
|---|---|---|
| URL browsing analysis | `METADATA` | core to the product; URL + risk score, no page-body |
| Remote-access monitoring | `METADATA` | core; which app + when, no screen contents |
| ImmediateDanger / sensitive-site correlation | `METADATA` | required for the existing protection logic |
| Sensitive-site classification | `METADATA` | classifies the site, not the user's actions on it |
| Inbound SMS reading | `NONE` | sensitive; user must opt in |
| Inbound email reading | `NONE` | sensitive; user must opt in |
| Call log + spoofed-number detection | `NONE` | sensitive; user must opt in |
| Darknet leak monitoring (HIBP-style) | `NONE` (user enters email manually) | requires sharing the user's contact info with a 3rd party |
| Per-domain dwell / form-submit on risky domains | `METADATA` | active protection feature; no form-content captured |
| Browsing-pattern baseline (rolling, on-device) | `PRESENCE` | derived from already-collected URL data; no extra capture |

The first-run UX walks the user through each source with a one-line
trade-off ("Enable SMS reading to detect phishing texts; we'll only process
them on your device unless you also enable cloud analysis"). Each toggle
shows the current consent level + what enabling raises it to + the *signal
value* gained.

### Guardian override (opt-in, for protected users)

When a user account is *configured* as vulnerable (the `declared
vulnerability` modifier E.2 is set) AND a linked guardian exists, certain
"critical" sources become **guardian-approval-required to disable**:

- ImmediateDanger / sensitive-site correlation
- Remote-access monitoring
- URL browsing analysis

These are the sources whose disabling under social-engineering pressure
("turn off the warnings so you can complete the transfer") is a known scam
pattern. The guardian-override mechanism:

- Is *opt-in at account configuration time* — most users never see it.
- Does not prevent disabling; it requires the guardian to **approve** the
  disable within a short window (e.g. 24h). The user can always escalate.
- Has clear semantics: the protected user sees "this change needs your
  guardian's confirmation" — never silent denial.

This satisfies the protected-user mission (Charter priority 1) without
making the system paternalistic for normal users.

### Audit trail

Every consent change — by user or by guardian — is logged:
`UserConsentAuditLog { user_key, source, old_level, new_level, changed_by,
changed_at, reason? }`. Needed for legal defensibility, trust restoration
("I never enabled this" → here's the record), and to recognise patterns
like consent-revocation-under-attack.

### Computation under partial consent

When a dimension's source is at `NONE`, the aggregator for that dimension
returns *no value, not zero*. Two policies for the risk function:

- **Renormalize active weights** — distribute the missing source's weight
  proportionally across the active ones. Slight risk inflation in some
  cases, but URS stays well-calibrated for the user's actual exposure.
- **Accept "data void"** — never imply a number we don't have; lower
  confidence proportionally.

We use **renormalize + clearly-lowered confidence** as the default. The UI
reports honestly: *"Your protection is reduced because messages monitoring
is off — confidence 0.4. Enable it to improve."* No signal is fabricated.

### Per-user weight learning respects consent

The auto-correction mechanisms in §7 only update the per-user weights for
*permitted* sources. If a user has `INBOUND_MESSAGES = NONE`, no
personalized message-weight is learned for them; they get the global
default if they later opt in. No drift on data we cannot see.

---

## 4. Data-source landscape

Summary of what the system collects today vs what URS needs:

| Source | Today | Needed for full URS |
|---|---|---|
| URL visits, risk scoring | ✅ in production | reuse |
| Track-URL, dwell, form-submit | ⚠️ partial (visits) | **add form-submit + dwell to extension** |
| Remote-access sessions | ✅ in production | reuse |
| ImmediateDanger correlation | ✅ in production | reuse |
| Sensitive-site classification | ✅ in production | reuse |
| Inbound messages (SMS / email / WhatsApp) | ❌ | **major new ingestion** — needs mobile-agent SMS read permission, gmail OAuth scope expansion, etc. Privacy-heavy. |
| Call logs (incl. spoofed-number detection) | ❌ | **major new ingestion** — needs mobile-agent call-log permission. Privacy-heavy. |
| Darknet / breach-leak feed | ❌ | **3rd-party feed** — e.g. HIBP (free, limited) + paid leads-monitor. |
| User profile (age, declared vulnerability) | ⚠️ partial | small admin-UI extension to expose these. |
| Per-user behavioral baseline (rolling) | ❌ | **new aggregation service** — derive from existing events, no new ingestion needed. |

**Implication:** an MVP URS can use only ✅/⚠️ sources and is still far better
than what exists today; the ❌ items unlock the higher-value detection but
each requires real product/legal work.

---

## 5. The formula — layered

```
   per-event RiskAssessment     ←  L1  (already exists, e.g. URL analyzer)
        ↓ aggregation
   per-dimension subscores       ←  L2  (count + weight + time decay)
        ↓ axis composition
   Vulnerability  +  Exposure   ←  L3  (slow vs fast axes, each 0–100)
        ↓ risk function
   UserRiskScore.score (0–100)   ←  L4  (logistic — §6)
```

### L1 — Per-event risk
Already exists. Examples:
- `UrlAlert` → `RiskAssessment(risk_score, risk_level, is_scam, confidence)`
- `RemoteAccessAlert` + sensitive-site overlap → ImmediateDanger trigger
- (Future) `IncomingMessage` → per-message scam likelihood

### L2 — Per-dimension subscore

For each dimension *d* with a set of events *e*:
```
subscore(d) = clamp(0, 100,
                   Σ_e   weight(type(e))
                       × magnitude(e)
                       × time_decay(now − e.timestamp)
                       × confidence(e)
                  )
```
- `weight(type(e))` — the per-signal weight (see §7).
- `magnitude(e)` — for graded signals: `e.risk_score`, dwell-time bucketed, etc. For binary: 1.
- `time_decay(Δt)` — `λ^days`, with `λ` per-dimension (Live decays fast, Vulnerability slow).
- `confidence(e)` — the assessment's confidence (0–1), so low-confidence events contribute less.

### L3 — Axis composition

```
Vulnerability_score  = clamp(0, 100,
                              context_modifier(E.1, E.2, E.4)
                              × ( w_B  · subscore(B)
                                + w_D' · subscore(D, slow-portion) )
                            )

Exposure_score       = clamp(0, 100,
                              w_A  · subscore(A)
                            + w_D'' · subscore(D, fast-portion)
                            )

Live_score           = clamp(0, 100,  w_C · subscore(C))

Corr_score           = clamp(0, 100,  w_D · subscore(D))
```
Note: D (cross-modal correlation) participates in both Vulnerability and
Exposure depending on which side of the time horizon the correlation lives —
but mainly Live for short-window correlations.

### L4 — The risk function

```
URS.score = round( 100 · σ( a·Vuln + b·Exp + c·Live + d·Corr − θ ) )

         where σ(x) = 1 / (1 + e^(−k·x))    ← logistic with steepness k
```
- `a`, `b`, `c`, `d` — axis weights (a starting set is in §7).
- `θ` — bias; the value of the linear input for which URS hits 50.
- `k` — steepness; controls how sharply the curve transitions.

URS is `round()` for the integer scalar; the contributing-signals breakdown
keeps full precision.

---

## 6. Why the logistic function — and what was rejected

| Candidate | Rejected because |
|---|---|
| **Weighted sum + `min(100, …)` (current code)** | unbounded behavior of the sum; calibrating "how many events = 100" is brittle; ceiling is a cliff, not a curve |
| **Max-of-event-scores** | ignores aggregation; one bad URL = same as ten |
| **Multiplicative (e.g. `Π (1 + risk_i)`)** | too volatile; one outlier dominates |
| **Pure ML classifier (gradient-boosted, NN)** | not explainable; needs labeled outcomes we don't have at scale |

**Recommended: logistic.** Reasons:
1. **Bounded by construction** — output is always (0, 100); no clamp cliff.
2. **Saturates gracefully** — many small risk events still sum to a high URS,
   but each additional event matters less as URS approaches 100 (matches
   reality: a user with 50 risky visits is not "twice as risky" as one with
   25).
3. **Calibratable** — `θ` shifts the threshold, `k` shifts the steepness; both
   are interpretable and easy to tune from labeled data when it appears.
4. **Drop-in for logistic regression later** — when we have outcome labels,
   we can move directly from expert-set weights to learned weights with the
   **same functional form**. No re-architecture.

This recommendation is conventional in risk-scoring literature for exactly
these reasons (credit scoring, fraud detection, medical risk indices).

---

## 7. Weights — initial values and auto-correction

### Initial weights (expert-set, sketch — to be tuned)

| Signal | Weight | Rationale |
|---|---|---|
| C.1 ImmediateDanger active | 35 | one is potentially catastrophic |
| C.4 ScamInProgress key | 30 | system-confirmed scam touching this user |
| D.2 Lure-conversion chain (msg → click → form) | 25 | strongest predictor of actual loss |
| D.1 Spoofed call → click within 30 min | 20 | classic social-engineering pattern |
| C.3 Form submit on flagged domain | 18 | credentials may have just been exfiltrated |
| C.2 Open RemoteAccess (no sensitive overlap) | 8 | suspicious but ambiguous |
| B.5 RemoteAccess + sensitive-site total time | 6 | pattern indicator |
| B.3 Per-month message-click-through rate | 10 | strong vulnerability proxy |
| B.2 Per-domain dwell + form-submit | 6 | engagement depth |
| B.1 Risky URL visits (per visit, weighted by risk_score) | 4 | most-frequent low signal |
| A.1 Darknet leak presence (per record) | 6 | step-change exposure |
| A.2 Inbound malicious messages (per message, by severity) | 4 | base inbound rate |
| A.3 Spoofed calls received (per call) | 3 | base inbound rate |
| B.6 Anomaly vs baseline | 5 (multiplicative ×1.2) | meta-signal — amplifies others |
| E.1–E.4 context modifiers | multipliers (×1.0–×1.5) | not additive |

Axis-level weights for L4 (the logistic argument):
- `a` (Vulnerability) = 0.7
- `b` (Exposure)      = 1.0
- `c` (Live)          = 1.5
- `d` (Corr)          = 1.3
- `θ` ≈ 50; `k` ≈ 0.06 (gives URS ≈ 50 at linear-input ≈ 50, URS ≈ 90 at ≈ 90)

These are *starting* values. The first month is observational — collect
inputs + outputs, do not let URS drive irreversible automated actions yet.

### Auto-correction — three plausible mechanisms, increasing in sophistication

1. **Threshold-band calibration** (Phase 3, low data).
   For each band (Low/Elevated/High/Critical), track the rate of *confirmed
   outcomes* (true scam reports, family-confirmed harm, false-alarm rates
   from user-feedback dismissals). When confirmed-scam rate in the Elevated
   band exceeds the High band — the thresholds drift; retune `θ`. Needs only
   modest ground truth.

2. **Bayesian per-signal updating** (Phase 4).
   For each signal-type, maintain a Beta posterior over "given this signal
   fired, the probability of a real scam outcome". Use the posterior to
   nudge that signal's weight up or down. Robust to small-sample noise.
   Needs per-event ground truth labels.

3. **Logistic-regression refit** (Phase 5+, when we have ≥ 1000 labeled
   outcomes).
   Since the chosen risk function IS logistic, we can fit it directly on
   feature vectors → outcome labels. Same architecture, learned weights.
   This is the long-term endpoint.

**Ground-truth sources** for any of these:
- User / guardian "this was a scam" or "this was a false alarm" reports.
- Bank-fraud confirmation (if we ever integrate with a bank).
- Family-member-reported financial loss.
- The user actually following the protective action (engagement-as-label).

Without ground truth, *do not auto-correct*. A miscalibrated auto-corrector
is worse than well-set expert weights.

---

## 8. Architectural placement (in this codebase)

Reuse what's already in [UserRiskProfile.cs](../ASPSBackend14_J/Business/RealtimeAnalysis/UserDomain/UserRiskProfile.cs)
and the UDUser analyzer.

```
Common/Models/
   UserRiskScore.cs                  ← new — the structured object of §1
   RiskAssessment.cs                 ← existing, per-event (keep)

Common/Enums/
   DataConsentLevel.cs               ← new — NONE / PRESENCE / METADATA / CONTENT_LOCAL / CONTENT_SHARED
   DataSourceKind.cs                 ← new — enum naming every consent-configurable source

Common/Entities/
   UserRiskScoreHistory.cs           ← new — persisted snapshot per user per recompute
   UserConsentPreferences.cs         ← new — per user, per data source → DataConsentLevel
   UserConsentAuditLog.cs            ← new — every consent change (who/when/old/new/reason)
   GuardianLink.cs                   ← new — optional; protected-user ↔ guardian binding

Business/RealtimeAnalysis/UserDomain/
   UserRiskProfile.cs                ← existing — clarified intent: per-user weight set, learned from user behavior
   UserRiskScoreCalculator.cs        ← new — the L1→L4 pipeline, consent-aware (renormalize over permitted sources)
   UserRiskScoreService.cs           ← new — orchestration: on-event recompute + periodic batch
   ConsentService.cs                 ← new — read/write consent + guardian-approval workflow
   SignalAggregators/
      InboundVectorAggregator.cs     ← A-dimension — short-circuits if source consent < required
      BehaviorAggregator.cs          ← B-dimension
      LiveThreatAggregator.cs        ← C-dimension
      CorrelationAggregator.cs       ← D-dimension — needs ≥ 2 active sources to produce a value

WebApi/Pages/Users/
   RiskDetail.cshtml                 ← new — explainable breakdown UI (visible to the user; richer view for guardian)
   ConsentSettings.cshtml            ← new — user-facing consent toggles + per-source trade-off explanations
   GuardianApprovals.cshtml          ← new — pending approvals for vulnerable-user consent changes
```

**Recompute triggers** — the URS must be fresh enough to drive action:
- On every alert that materially changes a dimension (URL alert, RemoteAccess
  start, ImmediateDanger fire, message arrives, call arrives).
- Periodic full recompute (e.g. nightly) for time-decay updates.
- On user-profile edits (modifier change → axis recompute).

**Persistence:** store each recompute as a `UserRiskScoreHistory` row. This
enables trend graphs, the "your risk has been rising" guardian alert, and
the calibration loop in §7.

---

## 9. Practical phasing (what to build first)

The full design is a year-scale project. A useful URS is one quarter away.

**Phase 1 — MVP (use only ✅ data):**
- Add `UserRiskScore` structured object.
- Aggregators for B.1, B.4, B.5, C.1, C.2, C.4 (everything already collected).
- Logistic combination with expert weights.
- Persistence + per-user history.
- Explainable admin UI.
- *No auto-correction yet — collect data first.*

**Phase 2 — fill in the ⚠️ partial sources:**
- Extension form-submit + dwell-time signals (B.2, C.3).
- User-profile modifiers exposed (E.1–E.4).
- Per-user baseline + anomaly (B.6).

**Phase 3 — add ❌ new ingestion (privacy-heavy; needs product+legal):**
- Darknet leak feed (A.1).
- Inbound message ingestion (A.2) + B.3 + D.2.
- Call-log ingestion + spoofed-number detection (A.3) + D.1 + D.3.

**Phase 4 — auto-correction:**
- Threshold-band calibration once ≥ a few weeks of labeled outcomes exist.
- Bayesian per-signal updating.

**Phase 5+ — full learned model:**
- Logistic-regression refit on labeled corpus.
- Possibly per-cohort weight sets.

---

## 10. Decisions — resolved (2026-05-23)

| # | Decision | Resolution |
|---|---|---|
| 1 | URS scope | **Per user.** Per-device / per-modality views may be debug-only. |
| 2 | Latency | **Realtime when ImmediateDanger or URS-very-high is firing; batch otherwise.** Live and Corr signals trigger immediate recompute; the rest run on a periodic schedule. |
| 3 | Action coupling | **Yes — URS bands auto-trigger protective actions.** Two-tier policy: system config is the default; per-user config overrides. (Future tier: guardian override for protected-user scenarios — out of scope for this milestone.) |
| 4 | Privacy / consent for messages + calls | **Built in as a first-class concept** (see §3.5). Every data source is independently consent-configurable with a 5-level depth ladder (`NONE` / `PRESENCE` / `METADATA` / `CONTENT_LOCAL` / `CONTENT_SHARED`). System works with whatever is permitted; accuracy and confidence are reported honestly. |
| 5 | Ground-truth sourcing | Baseline: a one-click **"this was a false alarm"** dismiss on every warning + a guardian-portal **"report a confirmed scam incident"** flow. Implicit signals (did the user heed the action) also collected. No periodic prompts that could erode trust. Auto-correction only when meaningful ground truth accumulates. |
| 6 | Transparency | **URS is visible to the user.** No guardian-only hiding. Some raw signals may be presented in summary form rather than dumped, but the user always sees their own score, the contributing reasons, and the active data sources. |
| A | Guardian override on consent disabling | **Opt-in dual-consent for vulnerable users.** When the account is configured as vulnerable + a guardian is linked, certain critical sources require guardian approval to disable — to counter the social-engineering pattern of "turn off the warnings so you can complete the transfer". Normal users never see this. Detailed in §3.5. |
| B | Default consent levels at install | **Recommended (balanced) baseline.** URL analysis + remote-access + sensitive-site correlation at `METADATA`; messages / calls / darknet at `NONE` (user must opt in). First-run UX walks through each source with a one-line trade-off. Detailed in §3.5. |

### `UserRiskProfile` clarification

Per user input (2026-05-23): `UserRiskProfile` is **the per-user weight set
itself**, not the output. The naming in this design now reflects that:

- `RiskAssessment` — *per event* (URL, call, message). Exists.
- `UserRiskProfile` — *per user, weights only* — personalized to the user's
  observed behavior. Learning (auto-correction in §7) is the process that
  drifts these weights from the global defaults toward what each user's
  history says actually predicts harm for *them*.
- `UserRiskScore` — *per user, the structured output*. Computed each cycle
  as `URS = risk_function(events × UserRiskProfile.weights)`.

---

## 11. Summary

URS is best modelled as a **structured assessment** producing a scalar via a
**logistic risk function** over four time-shaped dimensions
(Vulnerability/Exposure/Live/Correlation), each fed by aggregated per-event
signals. The existing `UserRiskProfile` is a credible Phase-1 scaffold;
extending it (rather than replacing it) is the right path. Auto-correction
must wait for ground truth; until then, calibrated expert weights.

The highest-leverage signals — **inbound messages**, **call logs**,
**darknet exposure**, and the **lure-conversion chain** — are not yet
ingested. Phase 1 ships a useful URS without them; Phase 3 unlocks the
exponentially better one.

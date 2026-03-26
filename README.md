# ASPS - Anti-Scam Protection System

**Version:** 0.0.0.3  
**Release Date:** March 26, 2026

---

## Overview

ASPS is a comprehensive anti-scam protection system that monitors user activity and protects against online fraud, phishing, and scam attempts.

### Components
- **Backend** - .NET 8 server with real-time analysis
- **WebApi** - Admin dashboard and API endpoints
- **Agent** - Windows desktop application
- **Extension** - Chrome browser extension

---

## Version 0.0.0.3 - Release Notes

### 🆕 New Features

#### User Layer (ASPS-258)
- **UserRiskProfile** - Real-time risk scoring per user
- **Risk Score Calculation** - Weighted formula with time decay
- **OTP Interception Detection** - Detects when scammers intercept 2FA codes
- **Black Screen** - Hides sensitive info when remote access detected on bank sites
- **UserIsTargetedAlert** - Alerts when user appears on darknet scam lists
- **SetTrackedDomains Event** - Cross-platform domain synchronization
- **Protective Actions Matrix** - Risk-based response system (warn/block/lock)
- **Sensitive Sites Configuration** - Configurable list of sensitive domains

#### Device Layer (ASPS-374)
- **TrackUrlAnalyzer** enhancements (Bait Detection, Form Detection, Urgency Indicators)
- **Site Category Detection** - Classifies websites by type (Banking, Crypto, etc.)
- **Phishing Detection Logic** - Domain similarity, SSL checks, content analysis

#### Extension
- **FormMonitorService** - Detects and tracks form submissions
- **Warning Banner** - Non-blocking warning for suspicious sites
- **Block Page** - Full-page block for high-risk sites
- **TrackUrlAlert** - Complete implementation

#### Admin Dashboard
- **Simulations** - Device alert simulator with autocomplete
- **BlacklistedPhoneNumbers** - Admin page for managing blocked numbers
- **BankWebsites** - Admin page for sensitive bank domains
- **KnownPhishingWebsites** - Admin page improvements

#### Agent
- **Windows Toast Notifications** - Risk-based alerts with actions
- **Installer** - Inno Setup installer ready for distribution

### 🐛 Bug Fixes
- Fixed CQRSGateway routing for Simulation queries
- Fixed version sync between components
- Fixed ASPS-74 TrackUrlAlert completion

### 🧪 Testing
- 220+ new unit tests for User Layer
- 52 new tests for Agent
- 15 new tests for Simulations API
- FormMonitorService tests

### 📊 Statistics
- **Tasks Completed:** 25+
- **New Files:** 50+
- **New Tests:** 300+

---

## Version 0.0.0.2 - Previous Release

### Features
- Version control across all components
- Remote access monitoring (TeamViewer, VNC, Chrome Remote Desktop, AnyDesk)
- TrackUrlAlert infrastructure
- Admin Simulations (basic)
- GeoIP lookup for remote IPs
- File transfer detection

---

## Installation

### Backend
```bash
cd ASPSBackend14_J
dotnet restore
dotnet build
dotnet run --project ASPSBackend
```

### WebApi
```bash
cd ASPSBackend14_J
dotnet run --project WebApi
```

### Agent (Windows)
1. Download `AntiScamDesktop-Setup-0.0.0.3.exe`
2. Run installer
3. Follow setup wizard

### Extension (Chrome)
1. Open `chrome://extensions`
2. Enable Developer Mode
3. Load unpacked from `apps/extension/chrome/`

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      User Devices                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐  │
│  │   Agent     │  │  Extension  │  │   Mobile App    │  │
│  │  (Windows)  │  │  (Chrome)   │  │   (Future)      │  │
│  └──────┬──────┘  └──────┬──────┘  └────────┬────────┘  │
└─────────┼────────────────┼──────────────────┼───────────┘
          │                │                  │
          ▼                ▼                  ▼
┌─────────────────────────────────────────────────────────┐
│                    ASPS Backend                          │
│  ┌─────────────────────────────────────────────────┐    │
│  │              Real-time Analysis                  │    │
│  │  ┌───────────┐ ┌───────────┐ ┌───────────────┐  │    │
│  │  │  Device   │ │   User    │ │  Intelligence │  │    │
│  │  │  Layer    │ │   Layer   │ │    Layer      │  │    │
│  │  └───────────┘ └───────────┘ └───────────────┘  │    │
│  └─────────────────────────────────────────────────┘    │
│                          │                               │
│  ┌───────────────────────┴───────────────────────┐      │
│  │              Protective Actions                │      │
│  │  (Warn → Block → Lock → Emergency Contact)     │      │
│  └────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────┘
```

---

## Risk Score Calculation

```
FinalRiskScore = min(100, (BaseRisk + Σ WeightedFactors) × TimeDecay + ActiveThreats)

Where:
- BaseRisk = 0.3 × VulnerabilityScore + 0.4 × ExposureScore
- WeightedFactors: RiskyUrl(1.0), SuspiciousCall(1.5), RemoteAccess(2.0), ScamInProgress(3.0)
- TimeDecay = 0.95^days
```

### Protective Actions by Risk Score

| Risk Score | Actions |
|------------|---------|
| 0-20 | Monitor only |
| 21-40 | Warning banner |
| 41-60 | Modal warning + Push notification |
| 61-80 | Block page + SMS alert |
| 81-100 | Full lock + Emergency contact |

---

## License

Proprietary - All rights reserved.

---

## Contact

- **GitHub:** https://github.com/yehudaz136/asps
- **JIRA:** https://zappa.asps.io

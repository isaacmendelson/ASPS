# ASPS — Anti-Scam Protection System

## Vision

A real-time, multi-layer protection platform that shields users from online scams, phishing attacks, and remote access fraud. ASPS runs silently in the background on the user's Windows machine, monitors browsing behavior and remote access tools, and alerts in real-time when threats are detected.

## Problem

Scam victims are growing — phone/browser-based scams, fake tech support, phishing. Existing tools (antiviruses, browser warnings) are reactive and generic. ASPS is proactive and context-aware: it knows the user's browsing history, device profile, and risk pattern, and can act before damage occurs.

## Core Components

| Component | Tech | Role |
|-----------|------|------|
| **ASPSBackend** | C# .NET 8 | Business logic, analysis orchestration, DB, ZMQ alert listener |
| **WebApi** | C# .NET 8 / ASP.NET | Admin dashboard, REST API, SignalR notifications |
| **Chrome Extension** | JavaScript | Detects URLs + trackers in browser, sends to desktop app |
| **Desktop App** | Python / Windows | Monitors browser + remote access tools, sends ZMQ alerts, receives notifications |
| **URL Analyzer** | Python / ML | Analyzes URLs with Playwright, WHOIS, ML models, optional LLM |

## Communication Architecture

```
Browser Extension ←WebSocket→ Desktop App ←ZMQ REQ→ ASPSBackend ←ZMQ→ WebApi
                                    ↑ ZMQ SUB ←notifications─────────────┘
```

## Target Users

- End users: Windows PC owners susceptible to scam/phishing
- Admins: Security operators managing devices and reviewing alerts via dashboard

## Success Criteria

- URL analyzed and result delivered to extension < 30s
- Remote access threat detected and flagged in real-time
- Admin dashboard shows live alerts and analysis results
- Zero false positives on legitimate banking/commerce sites
- System runs silently with < 2% CPU overhead

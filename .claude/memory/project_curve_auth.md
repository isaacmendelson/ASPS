---
name: CURVE Key Management & Auth Flow
description: How CURVE encryption keys are managed and the device authentication flow
type: project
---

## CURVE Key Management (implemented)
- Keys stored in `appsettings.json` under `Security` section (not file-based)
- Config keys: `ServerPublicKey` (base64), `ServerSecretKey` (base64), `ServerPublicKeyZ85`
- Client receives serverPublicKey (Z85) in token response, stores in `auth.json`
- First RequestToken may be unencrypted; subsequent calls use CURVE

## Authentication Flow (implemented)
- Device sends RequestToken -> backend checks ASView for device
- If DeviceNotRecognized -> client opens WebApi login page
- If known -> returns token + serverPublicKey (Z85)
- Alerts validated via TokenStore before processing
- Token refresh supported within MaxExpiration window

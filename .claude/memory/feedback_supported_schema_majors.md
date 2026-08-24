---
name: supported-schema-majors-required
description: All auth messages (RegisterDevice, RequestToken, RefreshToken) must include SupportedSchemaMajors=[1] — Azure rejects v0
metadata:
  type: feedback
---

Every `RegisterDevice`, `RequestToken`, and `RefreshToken` message must include `SupportedSchemaMajors = [1]`.

**Why:** Backend's `AlertProcessor.HandleRegisterDevice` defaults a missing `SupportedSchemaMajors` field to `[0]`. Azure runs with `AcceptLegacyV0=false`, so v0-only messages get rejected with "No mutually supported messaging schema major". This affects both WebApi (DeviceLogin page) and the desktop agent.

**How to apply:** When adding new auth message payloads or modifying existing ones in any client (WebApi, desktop agent, mobile), always include `SupportedSchemaMajors`. Check `alert_builders.py` and `DeviceLogin.cshtml.cs` as reference implementations.

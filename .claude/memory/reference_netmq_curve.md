---
name: NetMQ CURVE API Reference
description: Correct NetMQ 4.0.1.13 CURVE API usage - which methods exist and which don't
type: reference
---

## NetMQ CURVE API (v4.0.1.13)
- `socket.Options.CurveServer = true` — works
- `socket.Options.CurveCertificate = new NetMQCertificate(secretKey, publicKey)` — correct way to set keys
- **DO NOT USE** `socket.Options.CurveSecretKey` — does not exist in this version
- `NetMQCertificate` constructors: `()`, `(byte[] secret, byte[] public)`, `(string secretZ85, string publicZ85)`
- Static: `NetMQCertificate.CreateFromSecretKey(byte[])`, `.FromSecretKey(byte[])`
- Properties: `SecretKey`, `PublicKey`, `SecretKeyZ85`, `PublicKeyZ85`

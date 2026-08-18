"""
AntiScam Desktop App — Environment override: 'azure' (ASPS-721 / ADR-004)

Applied by:
    python build_release.py --env azure

The build script copies this file to `config_override.py`, which is imported
at the end of `config.py`. Names defined here REPLACE the corresponding
values in config.py — only override what differs from the defaults.

Selects the WebSocket transport (docs/architecture/WS-AGENT-PROTOCOL.md)
instead of direct ZMQ, so the agent can reach the Azure-hosted Backend
through the WebApi's WebSocket gateway at /ws/agent. CURVE is not used on
this path — TLS (wss://) is the transport security boundary (see
ADR-004-ASPS-718-WEBSOCKET-GATEWAY.md, "Security analysis").

Keep this file minimal: only environment-specific overrides go here.
"""

TRANSPORT_MODE = "ws"
WS_URL = "wss://ca-webapi-dev.purplesand-dfb51ae4.northeurope.azurecontainerapps.io/ws/agent"

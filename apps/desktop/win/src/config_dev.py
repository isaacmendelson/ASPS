"""
AntiScam Desktop App — Environment override: 'dev' (AWS Production)

Applied by:
    python build_release.py --env dev

The build script copies this file to `config_override.py`, which is imported
at the end of `config.py`. Names defined here REPLACE the corresponding
values in config.py — only override what differs from the defaults.

Keep this file minimal: only environment-specific overrides go here.
"""

BACKEND_HOST = "127.0.0.1"  # Local testing

# WebApi (Dashboard / View Details) — AWS Production
WEBAPI_URL = "https://localhost:5002"

"""
AntiScam Desktop App — Environment override: 'prod' (AWS Production)

Applied by:
    python build_release.py --env prod

The build script copies this file to `config_override.py`, which is imported
at the end of `config.py`. Names defined here REPLACE the corresponding
values in config.py — only override what differs from the defaults.

Keep this file minimal: only environment-specific overrides go here.
"""

BACKEND_HOST = "app.asps.io"

# WebApi (Dashboard / View Details) — AWS Production
WEBAPI_URL = "https://admin.asps.io"

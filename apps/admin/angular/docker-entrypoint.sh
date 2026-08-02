#!/bin/sh
# Writes /assets/runtime-config.json from environment variables at container startup.
# This allows the same Docker image to run in dev, staging, and production
# without rebuilding — only environment variables change between environments.

set -e

RUNTIME_CONFIG="/usr/share/nginx/html/assets/runtime-config.json"

# Ensure assets directory exists
mkdir -p "$(dirname "$RUNTIME_CONFIG")"

cat > "$RUNTIME_CONFIG" <<EOF
{
  "apiUrl": "${API_URL:-http://localhost:5001}",
  "keycloakUrl": "${KEYCLOAK_URL:-http://localhost:8081}",
  "keycloakRealm": "${KEYCLOAK_REALM:-asps}",
  "keycloakClientId": "${KEYCLOAK_CLIENT_ID:-asps-angular-admin}"
}
EOF

echo "runtime-config.json written:"
cat "$RUNTIME_CONFIG"

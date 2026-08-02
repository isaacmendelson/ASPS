// Production values are overridden at runtime via /assets/runtime-config.json
// injected by docker-entrypoint.sh. These empty strings act as safe fallbacks.
export const environment = {
  production: true,
  apiUrl: '',
  keycloakUrl: '',
};

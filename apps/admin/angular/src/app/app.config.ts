import {
  APP_INITIALIZER,
  ApplicationConfig,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import {
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { KeycloakService } from 'keycloak-angular';

import { routes } from './app.routes';
import { RuntimeConfigService } from './core/services/runtime-config.service';
import { authInterceptor } from './core/auth/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

function initializeKeycloak(
  keycloak: KeycloakService,
  config: RuntimeConfigService
): () => Promise<boolean> {
  return () =>
    config.load().then(() =>
      keycloak.init({
        config: {
          url: config.keycloakUrl,
          realm: config.keycloakRealm,
          clientId: config.keycloakClientId,
        },
        initOptions: {
          onLoad: 'check-sso',
          silentCheckSsoFallback: false,
          checkLoginIframe: false,
          pkceMethod: 'S256',
        },
        // We provide our own authInterceptor — disable the built-in one
        // to keep full control over which requests receive the token.
        enableBearerInterceptor: false,
      })
    );
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAnimations(),
    KeycloakService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeKeycloak,
      multi: true,
      deps: [KeycloakService, RuntimeConfigService],
    },
  ],
};

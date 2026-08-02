import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

interface RuntimeConfig {
  apiUrl: string;
  keycloakUrl: string;
  keycloakRealm: string;
  keycloakClientId: string;
}

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private config: RuntimeConfig | null = null;

  get apiUrl(): string {
    return this.config?.apiUrl ?? environment.apiUrl;
  }

  get keycloakUrl(): string {
    return this.config?.keycloakUrl ?? environment.keycloakUrl;
  }

  get keycloakRealm(): string {
    return this.config?.keycloakRealm ?? 'asps';
  }

  get keycloakClientId(): string {
    return this.config?.keycloakClientId ?? 'asps-angular-admin';
  }

  /**
   * Loads runtime-config.json from /assets/ before app bootstrap.
   * Falls back silently to environment.ts defaults if the file is not present
   * (e.g. during `ng serve` without Docker).
   */
  load(): Promise<void> {
    return fetch('/assets/runtime-config.json')
      .then(response => {
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        return response.json() as Promise<RuntimeConfig>;
      })
      .then(config => {
        this.config = config;
      })
      .catch(() => {
        console.warn('runtime-config.json not found, using environment defaults.');
      });
  }
}

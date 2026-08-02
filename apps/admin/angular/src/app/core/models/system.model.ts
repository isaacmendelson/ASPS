export interface SystemVersion {
  backendVersion: string;
  webApiVersion: string;
  databaseVersion: string;
  agentVersion?: string;
}

export interface HealthCheck {
  name: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description?: string;
  checkedAt: string;
}

export interface SystemSettings {
  keycloakEnabled: boolean;
  notificationsEnabled: boolean;
  version: SystemVersion;
  health: HealthCheck[];
}

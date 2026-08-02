import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { KeycloakService } from 'keycloak-angular';

/**
 * Functional auth guard.
 * 1. Redirects unauthenticated users to Keycloak login.
 * 2. Checks for Admin role (realm roles OR groups claim).
 * 3. Redirects non-admin authenticated users to /access-denied.
 */
export const authGuard = async (): Promise<boolean> => {
  const keycloak = inject(KeycloakService);
  const router = inject(Router);

  const isAuthenticated = keycloak.isLoggedIn();

  if (!isAuthenticated) {
    await keycloak.login({
      redirectUri: window.location.origin + '/dashboard',
    });
    return false;
  }

  // Check realm roles
  const roles = keycloak.getUserRoles(true);
  const hasAdminRole =
    roles.includes('admin') || roles.includes('Admin');

  // Also check groups claim (Keycloak group membership)
  const token = keycloak.getKeycloakInstance().tokenParsed;
  const groups: string[] = (token?.['groups'] as string[]) ?? [];
  const isInAdminGroup = groups.some(
    g => g === '/Administrators' || g === 'Administrators'
  );

  if (!hasAdminRole && !isInAdminGroup) {
    router.navigate(['/access-denied']);
    return false;
  }

  return true;
};

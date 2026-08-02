import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { KeycloakService } from 'keycloak-angular';
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  let keycloakSpy: jasmine.SpyObj<KeycloakService>;
  let routerSpy: jasmine.SpyObj<Router>;

  function runGuard(): Promise<boolean> {
    return TestBed.runInInjectionContext(() => authGuard());
  }

  beforeEach(() => {
    keycloakSpy = jasmine.createSpyObj('KeycloakService', [
      'isLoggedIn',
      'login',
      'getUserRoles',
      'getKeycloakInstance',
    ]);
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        { provide: KeycloakService, useValue: keycloakSpy },
        { provide: Router, useValue: routerSpy },
      ],
    });
  });

  it('should redirect to Keycloak login when not authenticated', async () => {
    keycloakSpy.isLoggedIn.and.returnValue(false);
    keycloakSpy.login.and.returnValue(Promise.resolve());

    const result = await runGuard();

    expect(result).toBeFalse();
    expect(keycloakSpy.login).toHaveBeenCalledWith(
      jasmine.objectContaining({ redirectUri: jasmine.any(String) })
    );
  });

  it('should allow access when user has "admin" realm role', async () => {
    keycloakSpy.isLoggedIn.and.returnValue(true);
    keycloakSpy.getUserRoles.and.returnValue(['admin']);
    keycloakSpy.getKeycloakInstance.and.returnValue({
      tokenParsed: { groups: [], preferred_username: 'someuser' },
    } as any);

    const result = await runGuard();

    expect(result).toBeTrue();
    expect(routerSpy.navigate).not.toHaveBeenCalled();
  });

  it('should allow access when user is in Administrators group', async () => {
    keycloakSpy.isLoggedIn.and.returnValue(true);
    keycloakSpy.getUserRoles.and.returnValue([]);
    keycloakSpy.getKeycloakInstance.and.returnValue({
      tokenParsed: {
        groups: ['/Administrators'],
        preferred_username: 'someuser',
      },
    } as any);

    const result = await runGuard();

    expect(result).toBeTrue();
  });

  it('should allow access for hardcoded admin username "isaac"', async () => {
    keycloakSpy.isLoggedIn.and.returnValue(true);
    keycloakSpy.getUserRoles.and.returnValue([]);
    keycloakSpy.getKeycloakInstance.and.returnValue({
      tokenParsed: { groups: [], preferred_username: 'isaac' },
    } as any);

    const result = await runGuard();

    expect(result).toBeTrue();
  });

  it('should redirect to /access-denied for authenticated non-admin user', async () => {
    keycloakSpy.isLoggedIn.and.returnValue(true);
    keycloakSpy.getUserRoles.and.returnValue(['user']);
    keycloakSpy.getKeycloakInstance.and.returnValue({
      tokenParsed: { groups: [], preferred_username: 'regularuser' },
    } as any);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    const result = await runGuard();

    expect(result).toBeFalse();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/access-denied']);
  });
});

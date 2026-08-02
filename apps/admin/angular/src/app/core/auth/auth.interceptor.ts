import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { KeycloakService } from 'keycloak-angular';
import { from, switchMap } from 'rxjs';

/**
 * Attaches Authorization: Bearer <token> to requests targeting
 * the backend API (/api/*) or the SignalR hub.
 * Skips external URLs.
 */
export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const keycloak = inject(KeycloakService);

  const isApiRequest =
    req.url.startsWith('/api') ||
    req.url.includes('/notificationshub');

  if (!isApiRequest) {
    return next(req);
  }

  return from(keycloak.getToken()).pipe(
    switchMap(token => {
      if (token) {
        const authReq = req.clone({
          setHeaders: { Authorization: `Bearer ${token}` },
        });
        return next(authReq);
      }
      return next(req);
    })
  );
};

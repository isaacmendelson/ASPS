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

  // Match absolute URLs (e.g. http://localhost:5001/api/...) as well as
  // relative ones. Parse the URL to inspect the pathname only, falling back
  // to string-contains for relative paths that URL() cannot parse.
  let pathname: string;
  try {
    pathname = new URL(req.url).pathname;
  } catch {
    pathname = req.url;
  }
  const isApiRequest =
    pathname.includes('/api/') ||
    pathname.includes('/api?') ||
    pathname.endsWith('/api') ||
    pathname.includes('/notificationshub');

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

import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { KeycloakService } from 'keycloak-angular';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../services/notification.service';

/**
 * Global HTTP error handler.
 * Runs after authInterceptor in the chain.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(NotificationService);
  const keycloak = inject(KeycloakService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          // Token expired or invalid — redirect to Keycloak login
          keycloak.login({ redirectUri: window.location.href });
          break;

        case 403:
          router.navigate(['/access-denied']);
          break;

        case 0:
          // Network error (backend unreachable)
          notification.error(
            'Connection Error',
            'Unable to reach the server. Check your network connection.'
          );
          break;

        default:
          if (error.status >= 500) {
            const message =
              (error.error as { message?: string; Message?: string })
                ?.message ??
              (error.error as { message?: string; Message?: string })
                ?.Message ??
              'An unexpected server error occurred.';
            notification.error('Server Error', message);
          } else if (error.status >= 400) {
            const errBody = error.error as {
              message?: string;
              Message?: string;
            };
            const message =
              errBody?.message ??
              errBody?.Message ??
              (typeof error.error === 'string' ? error.error : null) ??
              'Invalid request.';
            notification.warn('Request Failed', message);
          }
      }

      return throwError(() => error);
    })
  );
};

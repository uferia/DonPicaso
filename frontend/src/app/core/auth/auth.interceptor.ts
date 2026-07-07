import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const accessToken = authService.getAccessToken();
  const authorizedReq = accessToken
    ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
    : req;

  return next(authorizedReq).pipe(
    catchError((error: unknown) => {
      // Auth endpoints failing with 401 (bad credentials/expired refresh
      // token) must not trigger another refresh attempt - that would loop.
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || req.url.includes('/api/v1/auth/')) {
        return throwError(() => error);
      }

      return from(authService.refresh()).pipe(
        switchMap((refreshed) => {
          if (!refreshed) {
            void router.navigateByUrl('/login');
            return throwError(() => error);
          }

          const retriedToken = authService.getAccessToken();
          const retriedReq = req.clone({ setHeaders: { Authorization: `Bearer ${retriedToken}` } });
          return next(retriedReq);
        }),
      );
    }),
  );
};

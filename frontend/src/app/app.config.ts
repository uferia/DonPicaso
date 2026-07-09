import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { routes } from './app.routes';
import { DonPicasoPreset } from './app-theme';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    providePrimeNG({
      theme: {
        preset: DonPicasoPreset,
        // The POS runs on tablets in bright rooms; no dark mode this phase.
        options: { darkModeSelector: false },
      },
    }),
    // Exchanges a stored refresh token for a fresh access token before the
    // app finishes bootstrapping, so a page reload on /admin or /pos
    // doesn't force a re-login when a valid refresh token already exists.
    // Safe to call unconditionally - AuthService.refresh() no-ops when
    // there's no stored refresh token.
    provideAppInitializer(() => {
      const authService = inject(AuthService);
      return authService.refresh();
    }),
  ],
};

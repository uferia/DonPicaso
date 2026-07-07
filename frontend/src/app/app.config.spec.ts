import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { appConfig } from './app.config';
import { AuthService } from './core/auth/auth.service';

describe('appConfig', () => {
  it('runs AuthService.refresh() as a blocking app initializer, so a stored session survives a reload', async () => {
    const refresh = vi.fn().mockResolvedValue(false);

    // Mirrors the real providers (routing + http) so provideAppInitializer's
    // callback can inject AuthService the same way it does at real app
    // boot, but swaps in HttpClientTesting (no real HTTP call goes out) and
    // an explicit AuthService stub so the spy and the instance the
    // initializer resolves are guaranteed to be the exact same object.
    TestBed.configureTestingModule({
      providers: [
        ...appConfig.providers,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { refresh } },
      ],
    });

    // TestBed doesn't run APP_INITIALIZERs as part of component creation the
    // way a real `bootstrapApplication()` does - internally, bootstrap calls
    // `ApplicationInitStatus.runInitializers()` (see @angular/core's
    // `internalCreateApplication`) before resolving `donePromise`. This
    // drives the exact same machinery to prove provideAppInitializer's
    // callback is actually wired up to call AuthService.refresh().
    // `runInitializers` isn't part of the public .d.ts surface (elided from
    // typings though present at runtime), so it's invoked via a cast.
    const initStatus = TestBed.inject(ApplicationInitStatus);
    (initStatus as unknown as { runInitializers(): void }).runInitializers();
    await initStatus.donePromise;

    expect(refresh).toHaveBeenCalledTimes(1);
  });
});

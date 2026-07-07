import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('attaches the bearer token when one is present', async () => {
    vi.spyOn(authService, 'getAccessToken').mockReturnValue('access-token-value');

    const responsePromise = firstValueFrom(http.get('/api/v1/some-resource'));
    const req = httpMock.expectOne('/api/v1/some-resource');

    expect(req.request.headers.get('Authorization')).toBe('Bearer access-token-value');
    req.flush({});
    await responsePromise;
  });

  it('retries the request with a new token after a silent refresh succeeds on a 401', async () => {
    vi.spyOn(authService, 'getAccessToken')
      .mockReturnValueOnce('expired-token')
      .mockReturnValueOnce('fresh-token');
    vi.spyOn(authService, 'refresh').mockResolvedValue(true);

    const responsePromise = firstValueFrom(http.get('/api/v1/some-resource'));

    const firstAttempt = httpMock.expectOne('/api/v1/some-resource');
    firstAttempt.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    // The retry-after-refresh path resolves over a couple of microtasks
    // (the refresh() promise, then the switchMap); yield the event loop so
    // the retried request has been dispatched before we look for it.
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();

    const retryAttempt = httpMock.expectOne('/api/v1/some-resource');
    expect(retryAttempt.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retryAttempt.flush({ ok: true });

    await responsePromise;
  });

  it('redirects to /login and gives up when the silent refresh also fails', async () => {
    vi.spyOn(authService, 'getAccessToken').mockReturnValue('expired-token');
    vi.spyOn(authService, 'refresh').mockResolvedValue(false);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    const responsePromise = firstValueFrom(http.get('/api/v1/some-resource'));
    const req = httpMock.expectOne('/api/v1/some-resource');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    await expect(responsePromise).rejects.toBeTruthy();
    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});

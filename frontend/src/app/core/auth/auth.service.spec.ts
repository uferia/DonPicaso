import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AuthService } from './auth.service';

function buildFakeAccessToken(claims: Record<string, string>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify(claims));
  return `${header}.${payload}.fake-signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('decodes the access token into currentUser after a successful login', async () => {
    const loginPromise = service.login({ email: 'corporate@donpicaso.dev', password: 'Password123!' });

    const req = httpMock.expectOne('/api/v1/auth/login');
    req.flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await loginPromise;

    expect(service.currentUser()).toEqual({
      userId: 'user-1',
      role: 'Corporate',
      brandId: null,
      branchId: null,
    });
    expect(localStorage.getItem('donpicaso.refreshToken')).toBe('refresh-token-value');
  });

  it('clears the session when logout is called', async () => {
    const loginPromise = service.login({ email: 'corporate@donpicaso.dev', password: 'Password123!' });
    httpMock.expectOne('/api/v1/auth/login').flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await loginPromise;

    const logoutPromise = service.logout();
    httpMock.expectOne('/api/v1/auth/logout').flush({});
    await logoutPromise;

    expect(service.currentUser()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
    expect(localStorage.getItem('donpicaso.refreshToken')).toBeNull();
  });

  it('clears the session even when the backend logout call fails', async () => {
    const loginPromise = service.login({ email: 'corporate@donpicaso.dev', password: 'Password123!' });
    httpMock.expectOne('/api/v1/auth/login').flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await loginPromise;

    const logoutPromise = service.logout();
    httpMock
      .expectOne('/api/v1/auth/logout')
      .flush({ message: 'Server Error' }, { status: 500, statusText: 'Internal Server Error' });
    await logoutPromise;

    expect(service.currentUser()).toBeNull();
    expect(service.getAccessToken()).toBeNull();
    expect(localStorage.getItem('donpicaso.refreshToken')).toBeNull();
  });

  it('clears the session when refresh fails', async () => {
    localStorage.setItem('donpicaso.refreshToken', 'stale-token');

    const refreshPromise = service.refresh();
    httpMock
      .expectOne('/api/v1/auth/refresh')
      .flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    const result = await refreshPromise;

    expect(result).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(localStorage.getItem('donpicaso.refreshToken')).toBeNull();
  });

  it('returns false immediately from refresh when there is no stored refresh token', async () => {
    const result = await service.refresh();

    expect(result).toBe(false);
  });
});

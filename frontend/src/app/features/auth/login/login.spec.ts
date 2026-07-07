import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { Login } from './login';

function buildFakeAccessToken(claims: Record<string, string>): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify(claims));
  return `${header}.${payload}.fake-signature`;
}

describe('Login', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  it('navigates to /admin after a successful login', async () => {
    const fixture = TestBed.createComponent(Login);
    const component = fixture.componentInstance;
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component['email'] = 'corporate@donpicaso.dev';
    component['password'] = 'Password123!';
    const submitPromise = component.submit();

    const req = httpMock.expectOne('/api/v1/auth/login');
    req.flush({
      accessToken: buildFakeAccessToken({ sub: 'user-1', role: 'Corporate' }),
      accessTokenExpiresAtUtc: new Date().toISOString(),
      refreshToken: 'refresh-token-value',
      refreshTokenExpiresAtUtc: new Date().toISOString(),
    });
    await submitPromise;

    expect(navigateSpy).toHaveBeenCalledWith('/admin');
  });

  it('shows an error message when the credentials are rejected', async () => {
    const fixture = TestBed.createComponent(Login);
    const component = fixture.componentInstance;

    const submitPromise = component.submit();
    const req = httpMock.expectOne('/api/v1/auth/login');
    req.flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    await submitPromise;

    expect(component['errorMessage']()).toBe('Invalid email or password.');
  });
});

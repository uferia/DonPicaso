import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { DEVICE_BRANCH_ID_STORAGE_KEY } from '../device-setup/device-setup';
import { StaffLogin } from './staff-login';

describe('StaffLogin', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [StaffLogin],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('redirects to /device-setup when no branch id is configured', async () => {
    const fixture = TestBed.createComponent(StaffLogin);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    await fixture.componentInstance.ngOnInit();

    expect(navigateSpy).toHaveBeenCalledWith('/device-setup');
  });

  it('loads the staff roster for the configured branch', async () => {
    localStorage.setItem(DEVICE_BRANCH_ID_STORAGE_KEY, 'branch-123');

    const fixture = TestBed.createComponent(StaffLogin);
    const initPromise = fixture.componentInstance.ngOnInit();

    const req = httpMock.expectOne('/api/v1/auth/staff/branch-123/users');
    req.flush([{ userId: 'user-1', displayName: 'Ana' }]);
    await initPromise;

    expect(fixture.componentInstance['roster']()).toEqual([{ userId: 'user-1', displayName: 'Ana' }]);
  });

  it('logs in and navigates to /pos after a correct 4-digit pin', async () => {
    localStorage.setItem(DEVICE_BRANCH_ID_STORAGE_KEY, 'branch-123');
    const fixture = TestBed.createComponent(StaffLogin);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const authService = TestBed.inject(AuthService);
    vi.spyOn(authService, 'staffLogin').mockResolvedValue(undefined);

    const initPromise = component.ngOnInit();
    httpMock.expectOne('/api/v1/auth/staff/branch-123/users').flush([{ userId: 'user-1', displayName: 'Ana' }]);
    await initPromise;

    component.selectMember({ userId: 'user-1', displayName: 'Ana' });
    '1234'.split('').forEach((digit) => component.pressDigit(digit));
    await component.submitPin();

    expect(authService.staffLogin).toHaveBeenCalledWith({ branchId: 'branch-123', userId: 'user-1', pin: '1234' });
    expect(navigateSpy).toHaveBeenCalledWith('/pos');
  });
});

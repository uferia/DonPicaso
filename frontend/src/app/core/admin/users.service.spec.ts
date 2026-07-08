import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { Role } from '../auth/auth.models';
import { UsersService } from './users.service';

describe('UsersService', () => {
  let service: UsersService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(UsersService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists users scoped by branchId', async () => {
    const listPromise = service.list({ branchId: 'br1' });

    const req = httpMock.expectOne((r) => r.url === '/api/v1/users' && r.params.get('branchId') === 'br1');
    req.flush([]);

    expect(await listPromise).toEqual([]);
  });

  it('creates a staff user', async () => {
    const createPromise = service.create({
      email: null,
      displayName: 'Staff Member',
      role: Role.Staff,
      brandId: 'b1',
      branchId: 'br1',
      password: null,
      pin: '1234',
    });

    const req = httpMock.expectOne('/api/v1/users');
    expect(req.request.method).toBe('POST');
    req.flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });

    expect((await createPromise).id).toBe('u1');
  });

  it('resets a staff members PIN', async () => {
    const resetPromise = service.resetCredential('u1', { newPassword: null, newPin: '5678' });

    const req = httpMock.expectOne('/api/v1/users/u1/reset-credential');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ newPassword: null, newPin: '5678' });
    req.flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });

    expect((await resetPromise).id).toBe('u1');
  });
});

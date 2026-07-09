import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { Role } from './auth.models';
import { branchSessionGuard, roleGuard } from './auth.guard';

describe('roleGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('allows access when the current user outranks the minimum role', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Corporate, brandId: null, branchId: null });

    const result = TestBed.runInInjectionContext(() => roleGuard(Role.BranchManager)({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('redirects to /login when there is no current user', () => {
    const router = TestBed.inject(Router);

    const result = TestBed.runInInjectionContext(() => roleGuard(Role.Staff)({} as never, {} as never));

    expect(result).toEqual(router.parseUrl('/login'));
  });

  it('redirects to /login when the current user is below the minimum role', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Staff, brandId: null, branchId: null });
    const router = TestBed.inject(Router);

    const result = TestBed.runInInjectionContext(() => roleGuard(Role.BranchManager)({} as never, {} as never));

    expect(result).toEqual(router.parseUrl('/login'));
  });
});

describe('branchSessionGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
  });

  it('allows a branch-scoped session', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Staff, brandId: 'brand-1', branchId: 'branch-1' });

    const result = TestBed.runInInjectionContext(() => branchSessionGuard({} as never, {} as never));

    expect(result).toBe(true);
  });

  it('redirects to /staff-login when the session has no branch (e.g. a brand owner)', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.BrandOwner, brandId: 'brand-1', branchId: null });
    const router = TestBed.inject(Router);

    const result = TestBed.runInInjectionContext(() => branchSessionGuard({} as never, {} as never));

    expect(result).toEqual(router.parseUrl('/staff-login'));
  });

  it('redirects to /staff-login when there is no current user', () => {
    const router = TestBed.inject(Router);

    const result = TestBed.runInInjectionContext(() => branchSessionGuard({} as never, {} as never));

    expect(result).toEqual(router.parseUrl('/staff-login'));
  });
});

import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { AdminRedirect } from './admin-redirect';

describe('AdminRedirect', () => {
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminRedirect],
      providers: [provideRouter([])],
    }).compileComponents();

    router = TestBed.inject(Router);
  });

  it('sends Corporate to the Brands list', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Corporate, brandId: null, branchId: null });
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    TestBed.createComponent(AdminRedirect).componentInstance.ngOnInit();

    expect(navigateSpy).toHaveBeenCalledWith('/admin/brands');
  });

  it('sends BrandOwner to their own Brand Branches list', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.BrandOwner, brandId: 'brand-1', branchId: null });
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    TestBed.createComponent(AdminRedirect).componentInstance.ngOnInit();

    expect(navigateSpy).toHaveBeenCalledWith('/admin/brands/brand-1/branches');
  });

  it('sends BranchManager to their own Branch Users list', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.BranchManager, brandId: 'brand-1', branchId: 'branch-1' });
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    TestBed.createComponent(AdminRedirect).componentInstance.ngOnInit();

    expect(navigateSpy).toHaveBeenCalledWith('/admin/branches/branch-1/users');
  });

  it('sends an unauthenticated visitor to /login', () => {
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    TestBed.createComponent(AdminRedirect).componentInstance.ngOnInit();

    expect(navigateSpy).toHaveBeenCalledWith('/login');
  });
});

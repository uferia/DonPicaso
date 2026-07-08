import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { AdminShell } from './admin-shell';

describe('AdminShell', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminShell],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('shows the Brands nav link for Corporate', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Corporate, brandId: null, branchId: null });

    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Brands');
  });

  it('hides the Brands nav link for BranchManager', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.BranchManager, brandId: 'brand-1', branchId: 'branch-1' });

    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('Brands');
    expect(fixture.nativeElement.textContent).toContain('Users');
  });
});

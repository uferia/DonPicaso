import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { DEVICE_BRANCH_ID_STORAGE_KEY, DeviceSetup } from './device-setup';

describe('DeviceSetup', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      imports: [DeviceSetup],
      providers: [provideRouter([])],
    });
  });

  it('stores the branch id and navigates to /staff-login on save', () => {
    const fixture = TestBed.createComponent(DeviceSetup);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component['branchId'] = 'branch-123';
    component.save();

    expect(localStorage.getItem(DEVICE_BRANCH_ID_STORAGE_KEY)).toBe('branch-123');
    expect(navigateSpy).toHaveBeenCalledWith('/staff-login');
  });

  it('shows an error and does not navigate when branch id is blank', () => {
    const fixture = TestBed.createComponent(DeviceSetup);
    const component = fixture.componentInstance;
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component.save();

    expect(component['errorMessage']()).toBe('Branch ID is required.');
    expect(navigateSpy).not.toHaveBeenCalled();
  });
});

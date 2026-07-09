import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';

import { MenuResponse } from '../../../core/menu/menu.models';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { PosShell } from './pos-shell';

const sampleMenu: MenuResponse = {
  categories: [
    {
      id: 'cat-1',
      name: 'Coffee',
      products: [{ id: 'p-1', name: 'Espresso', price: 2.5, imageUrl: null }],
    },
  ],
  taxRatePercent: 1.5,
};

describe('PosShell', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [PosShell],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        // Stub the root OrderSyncService: its constructor touches IndexedDB,
        // which jsdom lacks — same pattern as payment-dialog.spec.ts.
        { provide: OrderSyncService, useValue: { isOnline: signal(true), pendingCount: signal(0) } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  it('loads the menu on init and renders catalog + cart', async () => {
    const fixture = TestBed.createComponent(PosShell);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/menu').flush(sampleMenu);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-product-catalog')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-cart-panel')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Espresso');
  });

  it('shows the retry state when the menu is unavailable', async () => {
    const fixture = TestBed.createComponent(PosShell);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/menu').error(new ProgressEvent('offline'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.menu-unavailable')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('app-product-catalog')).toBeFalsy();
  });

  it('logs out to the staff PIN screen when the cart is empty', async () => {
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    const fixture = TestBed.createComponent(PosShell);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/menu').flush(sampleMenu);
    await fixture.whenStable();

    await fixture.componentInstance['doLogout']();

    // AuthService.logout() only calls the API when a refresh token exists;
    // with clean localStorage it just clears the session locally.
    expect(navigateSpy).toHaveBeenCalledWith('/staff-login');
  });
});

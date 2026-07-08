import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { BrandsList } from './brands-list';

describe('BrandsList', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BrandsList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists brands with their active status', async () => {
    const fixture = TestBed.createComponent(BrandsList);
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/brands').flush([
      { id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Don Picaso');
    expect(fixture.nativeElement.textContent).toContain('Active');
  });

  it('deactivates a brand and updates its row in place', async () => {
    const fixture = TestBed.createComponent(BrandsList);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/brands').flush([
      { id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    const clickPromise = fixture.componentInstance.toggleActive(fixture.componentInstance['brands']()[0]);
    httpMock.expectOne('/api/v1/brands/b1/deactivate').flush({
      id: 'b1', name: 'Don Picaso', isActive: false, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await clickPromise;
    fixture.detectChanges();

    expect(button.textContent).toContain('Reactivate');
  });
});

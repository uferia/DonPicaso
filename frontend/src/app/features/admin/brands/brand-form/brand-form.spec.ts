import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';

import { BrandForm } from './brand-form';

describe('BrandForm', () => {
  let httpMock: HttpTestingController;

  async function setUp(brandId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [BrandForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(brandId ? { brandId } : {}) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('creates a new brand and navigates back to the list', async () => {
    await setUp(null);
    const fixture = TestBed.createComponent(BrandForm);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    fixture.detectChanges();

    fixture.componentInstance['name'] = 'Don Picaso';
    const submitPromise = fixture.componentInstance.submit();
    httpMock.expectOne('/api/v1/brands').flush({
      id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await submitPromise;

    expect(navigateSpy).toHaveBeenCalledWith('/admin/brands');
  });

  it('loads an existing brand for editing and updates it on submit', async () => {
    await setUp('b1');
    const fixture = TestBed.createComponent(BrandForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/brands/b1').flush({
      id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();

    fixture.componentInstance['name'] = 'Don Picaso Renamed';
    const submitPromise = fixture.componentInstance.submit();
    const req = httpMock.expectOne('/api/v1/brands/b1');
    expect(req.request.method).toBe('PUT');
    req.flush({ id: 'b1', name: 'Don Picaso Renamed', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' });
    await submitPromise;
  });
});

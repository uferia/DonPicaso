import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';

import { BranchForm } from './branch-form';

describe('BranchForm', () => {
  let httpMock: HttpTestingController;

  async function setUp(branchId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [BranchForm],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(branchId ? { brandId: 'b1', branchId } : { brandId: 'b1' }) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('creates a new branch under the brand and navigates back to its branches list', async () => {
    await setUp(null);
    const fixture = TestBed.createComponent(BranchForm);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    fixture.detectChanges();

    fixture.componentInstance['name'] = 'Downtown';
    const submitPromise = fixture.componentInstance.submit();
    const req = httpMock.expectOne('/api/v1/brands/b1/branches');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' });
    await submitPromise;

    expect(navigateSpy).toHaveBeenCalledWith('/admin/brands/b1/branches');
  });

  it('loads an existing branch for editing and updates it on submit', async () => {
    await setUp('br1');
    const fixture = TestBed.createComponent(BranchForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/brands/b1/branches/br1').flush({
      id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();

    fixture.componentInstance['name'] = 'Uptown';
    const submitPromise = fixture.componentInstance.submit();
    const req = httpMock.expectOne('/api/v1/brands/b1/branches/br1');
    expect(req.request.method).toBe('PUT');
    req.flush({ id: 'br1', brandId: 'b1', name: 'Uptown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' });
    await submitPromise;
  });

  it('toasts on successful save', async () => {
    await setUp(null);
    const fixture = TestBed.createComponent(BranchForm);
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');
    fixture.detectChanges();

    fixture.componentInstance['name'] = 'Downtown';
    const submitPromise = fixture.componentInstance.submit();
    httpMock.expectOne('/api/v1/brands/b1/branches').flush({
      id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await submitPromise;

    expect(messageAddSpy).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success', summary: 'Branch created' }),
    );
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { BrandsService } from './brands.service';

describe('BrandsService', () => {
  let service: BrandsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(BrandsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists brands', async () => {
    const listPromise = service.list();

    httpMock.expectOne('/api/v1/brands').flush([{ id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' }]);

    expect(await listPromise).toHaveLength(1);
  });

  it('creates a brand', async () => {
    const createPromise = service.create('Don Picaso');

    const req = httpMock.expectOne('/api/v1/brands');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Don Picaso' });
    req.flush({ id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' });

    expect((await createPromise).id).toBe('b1');
  });

  it('deactivates a brand', async () => {
    const deactivatePromise = service.deactivate('b1');

    const req = httpMock.expectOne('/api/v1/brands/b1/deactivate');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'b1', name: 'Don Picaso', isActive: false, createdAtUtc: '2026-07-08T00:00:00Z' });

    expect((await deactivatePromise).isActive).toBe(false);
  });
});

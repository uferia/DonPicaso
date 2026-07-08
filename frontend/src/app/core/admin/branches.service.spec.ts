import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { BranchesService } from './branches.service';

describe('BranchesService', () => {
  let service: BranchesService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(BranchesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists branches under a brand', async () => {
    const listPromise = service.list('b1');

    httpMock
      .expectOne('/api/v1/brands/b1/branches')
      .flush([{ id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' }]);

    expect(await listPromise).toHaveLength(1);
  });

  it('creates a branch under a brand', async () => {
    const createPromise = service.create('b1', 'Downtown');

    const req = httpMock.expectOne('/api/v1/brands/b1/branches');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Downtown' });
    req.flush({ id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' });

    expect((await createPromise).id).toBe('br1');
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { BranchesList } from './branches-list';

describe('BranchesList', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BranchesList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ brandId: 'b1' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists branches under the brand from the route', async () => {
    const fixture = TestBed.createComponent(BranchesList);
    fixture.detectChanges();

    httpMock.expectOne('/api/v1/brands/b1/branches').flush([
      { id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Downtown');
  });
});

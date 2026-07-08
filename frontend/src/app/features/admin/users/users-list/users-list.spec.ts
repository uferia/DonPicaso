import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';

import { Role } from '../../../../core/auth/auth.models';
import { UsersList } from './users-list';

describe('UsersList', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UsersList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ branchId: 'br1' }),
              queryParamMap: convertToParamMap({ brandId: 'b1' }),
            },
          },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('lists users scoped to the branch from the route', async () => {
    const fixture = TestBed.createComponent(UsersList);
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/v1/users' && r.params.get('branchId') === 'br1');
    req.flush([
      {
        id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
        brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
      },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Staff Member');
  });
});

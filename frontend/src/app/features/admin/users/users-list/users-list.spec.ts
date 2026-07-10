import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';

import { Role } from '../../../../core/auth/auth.models';
import { UsersList } from './users-list';

const users = [
  {
    id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
    brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
  },
  {
    id: 'u2', email: 'manager@donpicaso.dev', displayName: 'Branch Manager', role: Role.BranchManager,
    brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
  },
];

describe('UsersList', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UsersList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService,
        ConfirmationService,
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

  async function renderWithUsers(): Promise<ComponentFixture<UsersList>> {
    const fixture = TestBed.createComponent(UsersList);
    fixture.detectChanges();
    const req = httpMock.expectOne((r) => r.url === '/api/v1/users' && r.params.get('branchId') === 'br1');
    req.flush(users);
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('lists users scoped to the branch from the route with status tags', async () => {
    const fixture = await renderWithUsers();

    expect(fixture.nativeElement.textContent).toContain('Staff Member');
    expect(fixture.nativeElement.textContent).toContain('Branch Manager');
    expect(fixture.nativeElement.querySelectorAll('p-tag').length).toBe(2);
  });

  it('filters users across name, role, and email', async () => {
    const fixture = await renderWithUsers();

    fixture.componentInstance['search'].set('manager@donpicaso.dev');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Branch Manager');
    expect(fixture.nativeElement.textContent).not.toContain('Staff Member');
  });

  it('asks for confirmation, then deactivates and toasts', async () => {
    const fixture = await renderWithUsers();
    const confirmationService = TestBed.inject(ConfirmationService);
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');

    let captured: Confirmation | undefined;
    vi.spyOn(confirmationService, 'confirm').mockImplementation((confirmation: Confirmation) => {
      captured = confirmation;
      return confirmationService;
    });

    fixture.componentInstance.confirmToggle(fixture.componentInstance['users']()[0]);
    expect(captured!.message).toContain('Staff Member');

    captured!.accept!();
    httpMock.expectOne('/api/v1/users/u1/deactivate').flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: false, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance['users']()[0].isActive).toBe(false);
    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }));
  });
});

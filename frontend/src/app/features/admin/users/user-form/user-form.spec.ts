import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';

import { Role } from '../../../../core/auth/auth.models';
import { UserForm } from './user-form';

describe('UserForm', () => {
  let httpMock: HttpTestingController;

  async function setUp(userId: string | null): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [UserForm],
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
              paramMap: convertToParamMap(userId ? { branchId: 'br1', userId } : { branchId: 'br1' }),
              queryParamMap: convertToParamMap({ brandId: 'b1' }),
            },
          },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => httpMock.verify());

  it('creates a new Staff user and navigates back to the branch Users list', async () => {
    await setUp(null);
    const fixture = TestBed.createComponent(UserForm);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');
    fixture.detectChanges();

    fixture.componentInstance['displayName'] = 'Staff Member';
    fixture.componentInstance['role'] = Role.Staff;
    fixture.componentInstance['pin'] = '1234';
    const submitPromise = fixture.componentInstance.submit();

    const req = httpMock.expectOne('/api/v1/users');
    expect(req.request.body).toMatchObject({ displayName: 'Staff Member', role: Role.Staff, pin: '1234' });
    req.flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await submitPromise;

    expect(navigateSpy).toHaveBeenCalledWith('/admin/branches/br1/users');
  });

  it('loads an existing user for editing and updates it on submit', async () => {
    await setUp('u1');
    const fixture = TestBed.createComponent(UserForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/users/u1').flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();

    fixture.componentInstance['displayName'] = 'Renamed Staff';
    const submitPromise = fixture.componentInstance.submit();
    const req = httpMock.expectOne('/api/v1/users/u1');
    expect(req.request.method).toBe('PUT');
    req.flush({
      id: 'u1', email: null, displayName: 'Renamed Staff', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await submitPromise;
  });

  it('resets a staff members PIN independently of the main save', async () => {
    await setUp('u1');
    const fixture = TestBed.createComponent(UserForm);
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/users/u1').flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();

    fixture.componentInstance['newPin'] = '5678';
    const resetPromise = fixture.componentInstance.resetCredential();
    const req = httpMock.expectOne('/api/v1/users/u1/reset-credential');
    expect(req.request.body).toEqual({ newPassword: null, newPin: '5678' });
    req.flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await resetPromise;

    expect(messageAddSpy).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success', summary: 'Credential updated' }),
    );
  });

  it('confirms before resetting a credential', async () => {
    await setUp('u1');
    const fixture = TestBed.createComponent(UserForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/users/u1').flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();

    const confirmationService = TestBed.inject(ConfirmationService);
    let captured: Confirmation | undefined;
    vi.spyOn(confirmationService, 'confirm').mockImplementation((confirmation: Confirmation) => {
      captured = confirmation;
      return confirmationService;
    });

    fixture.componentInstance['newPin'] = '5678';
    fixture.componentInstance['confirmResetCredential']();
    expect(captured).toBeDefined();

    captured!.accept!();
    httpMock.expectOne('/api/v1/users/u1/reset-credential').flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();
  });

  it('keeps Cancel pointing at the origin branch even after the form fields are edited', async () => {
    await setUp('u1');
    const fixture = TestBed.createComponent(UserForm);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/users/u1').flush({
      id: 'u1', email: null, displayName: 'Staff Member', role: Role.Staff,
      brandId: 'b1', branchId: 'br1', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();

    fixture.componentInstance['branchId'] = 'someone-elses-branch';
    fixture.componentInstance['brandId'] = 'someone-elses-brand';
    fixture.detectChanges();

    const cancel = fixture.nativeElement.querySelector('.admin-cancel-link') as HTMLAnchorElement;
    expect(cancel.getAttribute('href')).toContain('/admin/branches/br1/users');
    expect(cancel.getAttribute('href')).toContain('brandId=b1');
  });
});

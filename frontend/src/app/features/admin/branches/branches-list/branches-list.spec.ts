import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';

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
        MessageService,
        ConfirmationService,
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ brandId: 'b1' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function renderWithBranch(): Promise<ComponentFixture<BranchesList>> {
    const fixture = TestBed.createComponent(BranchesList);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/brands/b1/branches').flush([
      { id: 'br1', brandId: 'b1', name: 'Downtown', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' },
    ]);
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('lists branches under the brand from the route with a status tag', async () => {
    const fixture = await renderWithBranch();

    expect(fixture.nativeElement.textContent).toContain('Downtown');
    expect(fixture.nativeElement.querySelector('p-tag')).toBeTruthy();
  });

  it('asks for confirmation, then deactivates and toasts', async () => {
    const fixture = await renderWithBranch();
    const confirmationService = TestBed.inject(ConfirmationService);
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');

    let captured: Confirmation | undefined;
    vi.spyOn(confirmationService, 'confirm').mockImplementation((confirmation: Confirmation) => {
      captured = confirmation;
      return confirmationService;
    });

    fixture.componentInstance.confirmToggle(fixture.componentInstance['branches']()[0]);
    expect(captured!.message).toContain('Downtown');

    captured!.accept!();
    httpMock.expectOne('/api/v1/brands/b1/branches/br1/deactivate').flush({
      id: 'br1', brandId: 'b1', name: 'Downtown', isActive: false, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance['branches']()[0].isActive).toBe(false);
    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }));
  });
});

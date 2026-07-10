import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';

import { BrandsList } from './brands-list';

const brands = [
  { id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z' },
  { id: 'b2', name: 'Espresso Corner', isActive: false, createdAtUtc: '2026-07-08T00:00:00Z' },
];

describe('BrandsList', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BrandsList],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService,
        ConfirmationService,
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  async function renderWithBrands(): Promise<ComponentFixture<BrandsList>> {
    const fixture = TestBed.createComponent(BrandsList);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/brands').flush(brands);
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('lists brands with status tags', async () => {
    const fixture = await renderWithBrands();

    expect(fixture.nativeElement.textContent).toContain('Don Picaso');
    expect(fixture.nativeElement.textContent).toContain('Espresso Corner');
    const tags = fixture.nativeElement.querySelectorAll('p-tag');
    expect(tags.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Active');
    expect(fixture.nativeElement.textContent).toContain('Inactive');
  });

  it('filters rows by the search box', async () => {
    const fixture = await renderWithBrands();

    fixture.componentInstance['search'].set('espresso');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Espresso Corner');
    expect(fixture.nativeElement.textContent).not.toContain('Don Picaso');
  });

  it('shows a filter-miss message when the search matches nothing', async () => {
    const fixture = await renderWithBrands();

    fixture.componentInstance['search'].set('zzz');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No brands match your search.');
  });

  it('asks for confirmation, then deactivates and toasts', async () => {
    const fixture = await renderWithBrands();
    const confirmationService = TestBed.inject(ConfirmationService);
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');

    let captured: Confirmation | undefined;
    vi.spyOn(confirmationService, 'confirm').mockImplementation((confirmation: Confirmation) => {
      captured = confirmation;
      return confirmationService;
    });

    fixture.componentInstance.confirmToggle(fixture.componentInstance['brands']()[0]);
    expect(captured).toBeDefined();
    expect(captured!.message).toContain('Don Picaso');

    captured!.accept!();
    httpMock.expectOne('/api/v1/brands/b1/deactivate').flush({
      id: 'b1', name: 'Don Picaso', isActive: false, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance['brands']()[0].isActive).toBe(false);
    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }));
  });

  it('shows a retry state when the list fails to load', async () => {
    const fixture = TestBed.createComponent(BrandsList);
    fixture.detectChanges();
    httpMock.expectOne('/api/v1/brands').error(new ProgressEvent('offline'));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain("Couldn't load brands.");
    expect(fixture.nativeElement.querySelector('.admin-retry-link')).toBeTruthy();
  });
});

# Admin Restyle + Responsive Design Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the `/admin` area (shell + three list pages + three form pages) up to the POS's PrimeNG visual standard with toasts/confirm dialogs/loading/empty states, and make the whole app responsive (POS ≥768px, admin/auth ≥360px).

**Architecture:** Frontend-only. AdminShell gains the one-time toast/confirm wiring (mirroring PosShell) and a neutral white topbar. One canonical list treatment (BrandsList) and one canonical form treatment (BrandForm) are built first with full test coverage, then transcribed to the other four pages. Shared layout CSS lives in a global `_admin.scss` partial so all six pages stay consistent. Responsive behavior is CSS-only media queries, verified with Playwright screenshots.

**Tech Stack:** Angular 21 standalone + signals, PrimeNG 21.1.9 (emerald Aura preset already global), SCSS, Vitest/jsdom.

**Spec:** `docs/superpowers/specs/2026-07-10-admin-restyle-design.md`

## Global Constraints

- Zero backend, API, route, or service changes. Templates, styles, component classes, and specs only.
- PrimeNG components for all admin UI chrome; the emerald Aura preset is already configured globally — no theme changes.
- Admin topbar is neutral white with emerald accents (NOT the solid emerald POS bar).
- Responsive floors: POS usable at 768px width and up; admin and auth usable at 360px and up. No horizontal page scroll at any supported width — wide content scrolls inside its own container.
- Copy is sentence case ("New brand", not "New Brand"). Exact strings are given in each task.
- Test environment: specs run under `provideRouter([])`; Angular 21.2 REJECTS unmatched-route navigations, so never `await` a `navigateByUrl` in new code paths that tests exercise unless the existing code already does (the admin forms' awaited navigation inside try/catch is existing behavior — leave it). The single pre-existing NG04002 'pos' unhandled-rejection line from staff-login.spec.ts is accepted baseline noise; any OTHER warning is a defect.
- jsdom lacks IndexedDB — irrelevant here (no POS service changes), noted for completeness.
- Full suite baseline before this plan: `cd frontend && npm test` → 26 files / 80 tests passing; `npm run build` clean.
- Commits are authored solely by the repo owner — NO `Co-Authored-By` trailer of any kind.
- Components inject `MessageService`/`ConfirmationService` — provided by AdminShell at runtime (Task 1); isolated component specs must provide both in their own TestBed providers.

## File Structure

```
frontend/src/
  styles.scss                              (modify: @use the new admin partial)
  styles/_admin.scss                       (create: shared admin layout classes)
  app/features/admin/
    admin-shell/admin-shell.{ts,html,scss,spec.ts}      (modify: Task 1)
    brands/brands-list/brands-list.{ts,html,scss,spec.ts}   (modify: Task 2 — canonical list)
    brands/brand-form/brand-form.{ts,html,scss,spec.ts}     (modify: Task 3 — canonical form)
    branches/branches-list/branches-list.{ts,html,scss,spec.ts} (modify: Task 4)
    branches/branch-form/branch-form.{ts,html,scss,spec.ts}     (modify: Task 5)
    users/users-list/users-list.{ts,html,scss,spec.ts}          (modify: Task 6)
    users/user-form/user-form.{ts,html,scss,spec.ts}            (modify: Task 7)
  app/features/pos/pos-shell/pos-shell.scss             (modify: Task 8)
  app/features/pos/product-catalog/product-catalog.scss (modify: Task 8)
  app/features/auth/login/login.scss                    (modify: Task 9)
  app/features/auth/staff-login/staff-login.scss        (modify: Task 9)
  app/features/auth/device-setup/device-setup.scss      (modify: Task 9)
```

The shared classes in `_admin.scss` are global (Angular's emulated encapsulation doesn't scope `styles.scss`), so the six admin components' own `.scss` files stay nearly empty. Do NOT duplicate the shared classes into component stylesheets.

---

### Task 1: Shared admin stylesheet + AdminShell restyle

**Files:**
- Create: `frontend/src/styles/_admin.scss`
- Modify: `frontend/src/styles.scss`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.ts`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.html`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.scss`
- Modify: `frontend/src/app/features/admin/admin-shell/admin-shell.spec.ts`

**Interfaces:**
- Consumes: `AuthService.currentUser` signal, `Role` enum (existing, unchanged).
- Produces: `MessageService` + `ConfirmationService` provided at AdminShell and visible to all routed child pages (Tasks 2–7 inject them without providing). Global CSS classes consumed by Tasks 2–7: `.admin-page`, `.admin-page-header`, `.admin-card`, `.admin-toolbar`, `.admin-table-scroll`, `.admin-empty-state`, `.admin-row-actions`, `.admin-form-card`, `.admin-form-header`, `.admin-cancel-link`, `.admin-retry-link`.

- [ ] **Step 1: Update the spec for the new logout selector and add a role-tag test**

Replace the logout test's selector and append one test in `frontend/src/app/features/admin/admin-shell/admin-shell.spec.ts`. The logout test's `querySelector('.logout-button')` line becomes (the `.logout-button` class moves to the `p-button` host, so the real `<button>` is inside it):

```ts
    (fixture.nativeElement.querySelector('.logout-button button') as HTMLButtonElement).click();
```

Append inside the `describe` block:

```ts
  it('shows the current role as a tag', () => {
    const authService = TestBed.inject(AuthService);
    authService.currentUser.set({ userId: 'u1', role: Role.Corporate, brandId: null, branchId: null });

    const fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('p-tag')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('p-toast')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('p-confirmdialog')).toBeTruthy();
  });
```

- [ ] **Step 2: Run the spec to verify the new assertions fail**

Run: `cd frontend && npx ng test --watch=false --include='**/admin-shell.spec.ts'`
Expected: FAIL — no `p-tag` element yet, and the logout test fails on the changed selector (`.logout-button button` doesn't exist while `.logout-button` is still the plain button).

- [ ] **Step 3: Create the shared admin stylesheet**

`frontend/src/styles/_admin.scss`:

```scss
// Shared layout classes for the /admin area. Global on purpose: Angular's
// emulated encapsulation would force six copies if these lived in
// component stylesheets.

.admin-page {
  max-width: 1080px;
  margin: 0 auto;
  padding: 1.5rem;

  @media (max-width: 640px) {
    padding: 1rem 0.75rem;
  }
}

.admin-page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 1rem;

  h1 {
    margin: 0;
    font-size: 1.4rem;
  }
}

.admin-card {
  background: #fff;
  border: 1px solid var(--p-surface-200);
  border-radius: 10px;
  padding: 1rem;

  @media (max-width: 480px) {
    padding: 0.75rem;
  }
}

.admin-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;

  i {
    color: var(--p-surface-500);
  }

  input {
    flex: 1;
    max-width: 320px;
  }
}

.admin-table-scroll {
  overflow-x: auto;

  table {
    min-width: 480px;
  }
}

.admin-empty-state {
  text-align: center;
  color: var(--p-surface-500);
  padding: 2rem 1rem;
}

.admin-row-actions {
  text-align: right;
  white-space: nowrap;
}

.admin-form-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-width: 560px;
  margin: 0 auto;

  label {
    font-weight: 600;
    font-size: 0.9rem;
  }
}

.admin-form-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;

  h1 {
    margin: 0;
    font-size: 1.4rem;
  }
}

.admin-cancel-link {
  color: var(--p-surface-500);
  text-decoration: none;

  &:hover {
    color: var(--p-surface-700);
    text-decoration: underline;
  }
}

.admin-retry-link {
  background: none;
  border: none;
  padding: 0;
  margin-left: 0.5rem;
  color: var(--p-primary-600);
  font: inherit;
  text-decoration: underline;
  cursor: pointer;
}
```

- [ ] **Step 4: Register the partial in `frontend/src/styles.scss`**

Append to the end of the file (keep the existing `body` rule exactly as-is):

```scss
@use './styles/admin';
```

NOTE: sass requires `@use` before other rules — place the `@use` line at the TOP of `styles.scss`, above the comment and `body` rule. The final file is:

```scss
@use './styles/admin';

/* You can add global styles to this file, and also import other style files */

body {
  margin: 0;
  font-family: 'Inter Variable', system-ui, 'Segoe UI', sans-serif;

  // A register reads numbers all day: tabular figures keep prices and
  // totals from shifting width as digits change.
  font-variant-numeric: tabular-nums;
}
```

- [ ] **Step 5: Rewrite AdminShell**

`frontend/src/app/features/admin/admin-shell/admin-shell.ts` — full replacement:

```ts
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';

import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-admin-shell',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ButtonModule,
    ConfirmDialogModule,
    TagModule,
    ToastModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './admin-shell.html',
  styleUrl: './admin-shell.scss',
})
export class AdminShell {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly currentUser = this.authService.currentUser;
  protected readonly Role = Role;

  protected async logout(): Promise<void> {
    await this.authService.logout();
    // Not awaited: in tests (and any router config missing '/login')
    // navigateByUrl's promise rejects on no-match, which would otherwise
    // propagate out of this method. Matches the existing pattern in
    // staff-login.ts and pos-shell.ts for the same reason.
    void this.router.navigateByUrl('/login');
  }
}
```

`frontend/src/app/features/admin/admin-shell/admin-shell.html` — full replacement:

```html
<p-toast position="top-right" />
<p-confirmdialog />

<div class="admin-layout">
  <header class="admin-header">
    <div class="admin-brand">
      <span class="brand-mark"><i class="pi pi-shop"></i></span>
      <span class="brand-name">Don Picaso Admin</span>
    </div>

    <nav class="admin-nav">
      @if (currentUser()?.role === Role.Corporate) {
        <a routerLink="/admin/brands" routerLinkActive="active">Brands</a>
      }
      @if (currentUser()?.role === Role.BrandOwner && currentUser()?.brandId) {
        <a [routerLink]="['/admin/brands', currentUser()!.brandId!, 'branches']" routerLinkActive="active">Branches</a>
      }
      @if (currentUser()?.role === Role.BranchManager && currentUser()?.branchId) {
        <a [routerLink]="['/admin/branches', currentUser()!.branchId!, 'users']" routerLinkActive="active">Users</a>
      }
    </nav>

    <div class="session-controls">
      @if (currentUser(); as user) {
        <p-tag [value]="user.role" severity="secondary" />
      }
      <p-button
        class="logout-button"
        label="Log out"
        icon="pi pi-sign-out"
        severity="secondary"
        text
        (onClick)="logout()"
      />
    </div>
  </header>

  <main class="admin-content">
    <router-outlet />
  </main>
</div>
```

`frontend/src/app/features/admin/admin-shell/admin-shell.scss` — full replacement:

```scss
.admin-layout {
  min-height: 100vh;
  background: var(--p-surface-50);
}

.admin-header {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  gap: 1.25rem;
  flex-wrap: wrap;
  padding: 0.5rem 1.25rem;
  background: #fff;
  border-bottom: 1px solid var(--p-surface-200);
}

.admin-brand {
  display: flex;
  align-items: center;
  gap: 0.5rem;

  .brand-mark {
    display: grid;
    place-items: center;
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background: var(--p-primary-100);

    i {
      color: var(--p-primary-600);
    }
  }

  .brand-name {
    font-weight: 700;
  }
}

.admin-nav {
  display: flex;
  gap: 0.25rem;
  flex: 1;

  a {
    padding: 0.4rem 0.75rem;
    border-radius: 6px;
    color: var(--p-surface-600);
    text-decoration: none;
    font-weight: 600;

    &:hover {
      background: var(--p-surface-100);
    }

    &.active {
      color: var(--p-primary-700);
      background: var(--p-primary-50);
    }
  }
}

.session-controls {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

@media (max-width: 640px) {
  .admin-header {
    padding: 0.5rem 0.75rem;
    gap: 0.5rem;
  }

  .admin-brand .brand-name {
    display: none;
  }
}
```

- [ ] **Step 6: Run the shell spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/admin-shell.spec.ts'`
Expected: PASS (4/4).
Run: `cd frontend && npm test`
Expected: all files pass; only the baseline NG04002 'pos' noise line.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/styles.scss frontend/src/styles frontend/src/app/features/admin/admin-shell
git commit -m "Restyle admin shell with PrimeNG topbar and shared toast/confirm hosting"
```

---

### Task 2: BrandsList — canonical list page

**Files:**
- Modify: `frontend/src/app/features/admin/brands/brands-list/brands-list.ts`
- Modify: `frontend/src/app/features/admin/brands/brands-list/brands-list.html`
- Modify: `frontend/src/app/features/admin/brands/brands-list/brands-list.scss`
- Modify: `frontend/src/app/features/admin/brands/brands-list/brands-list.spec.ts`

**Interfaces:**
- Consumes: `BrandsService.list(): Promise<Brand[]>`, `.deactivate(id): Promise<Brand>`, `.reactivate(id): Promise<Brand>` (existing, unchanged); `MessageService`/`ConfirmationService` (provided by AdminShell at runtime, by the TestBed in specs); global `.admin-*` classes from Task 1.
- Produces: the canonical list-page pattern that Tasks 4 and 6 transcribe: signals `isLoading`, `loadError`, `search`, computed `filtered<Entities>`, methods `load()`, `confirmToggle(entity)`, `toggleActive(entity)` (public, unchanged signature).

- [ ] **Step 1: Rewrite the spec with the canonical coverage**

`frontend/src/app/features/admin/brands/brands-list/brands-list.spec.ts` — full replacement:

```ts
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
```

- [ ] **Step 2: Run the spec to verify the new tests fail**

Run: `cd frontend && npx ng test --watch=false --include='**/brands-list.spec.ts'`
Expected: FAIL — no `search` signal, no `confirmToggle`, no `p-tag` in the template.

- [ ] **Step 3: Implement the component**

`frontend/src/app/features/admin/brands/brands-list/brands-list.ts` — full replacement:

```ts
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { Brand } from '../../../../core/admin/admin.models';
import { BrandsService } from '../../../../core/admin/brands.service';

@Component({
  selector: 'app-brands-list',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, TableModule, TagModule],
  templateUrl: './brands-list.html',
  styleUrl: './brands-list.scss',
})
export class BrandsList implements OnInit {
  private readonly brandsService = inject(BrandsService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly brands = signal<Brand[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly search = signal('');

  protected readonly filteredBrands = computed(() => {
    const term = this.search().trim().toLowerCase();
    const brands = this.brands();
    return term ? brands.filter((brand) => brand.name.toLowerCase().includes(term)) : brands;
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set(false);
    try {
      this.brands.set(await this.brandsService.list());
    } catch {
      this.loadError.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  confirmToggle(brand: Brand): void {
    const confirmation: Confirmation = {
      header: brand.isActive ? 'Deactivate brand' : 'Reactivate brand',
      message: brand.isActive
        ? `Deactivate ${brand.name}? Its branches and staff won't be able to sign in.`
        : `Reactivate ${brand.name}?`,
      acceptButtonProps: {
        label: brand.isActive ? 'Deactivate' : 'Reactivate',
        severity: brand.isActive ? 'danger' : 'primary',
      },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.toggleActive(brand),
    };
    this.confirmationService.confirm(confirmation);
  }

  async toggleActive(brand: Brand): Promise<void> {
    try {
      const updated = brand.isActive
        ? await this.brandsService.deactivate(brand.id)
        : await this.brandsService.reactivate(brand.id);

      this.brands.update((brands) => brands.map((b) => (b.id === updated.id ? updated : b)));
      this.messageService.add({
        severity: 'success',
        summary: updated.isActive ? 'Brand reactivated' : 'Brand deactivated',
      });
    } catch {
      this.messageService.add({ severity: 'error', summary: "Couldn't update the brand" });
    }
  }
}
```

`frontend/src/app/features/admin/brands/brands-list/brands-list.html` — full replacement:

```html
<div class="admin-page">
  <div class="admin-page-header">
    <h1>Brands</h1>
    <p-button label="New brand" icon="pi pi-plus" routerLink="/admin/brands/new" />
  </div>

  @if (loadError()) {
    <p-message severity="error">
      Couldn't load brands.
      <button type="button" class="admin-retry-link" (click)="load()">Retry</button>
    </p-message>
  } @else {
    <div class="admin-card">
      <div class="admin-toolbar">
        <i class="pi pi-search"></i>
        <input
          pInputText
          type="text"
          name="search"
          placeholder="Search brands"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
        />
      </div>

      <div class="admin-table-scroll">
        <p-table [value]="filteredBrands()" [loading]="isLoading()" dataKey="id">
          <ng-template #header>
            <tr>
              <th pSortableColumn="name">Name <p-sortIcon field="name" /></th>
              <th>Status</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template #body let-brand>
            <tr>
              <td><a [routerLink]="['/admin/brands', brand.id]">{{ brand.name }}</a></td>
              <td>
                <p-tag
                  [value]="brand.isActive ? 'Active' : 'Inactive'"
                  [severity]="brand.isActive ? 'success' : 'secondary'"
                />
              </td>
              <td class="admin-row-actions">
                <p-button
                  [label]="brand.isActive ? 'Deactivate' : 'Reactivate'"
                  [severity]="brand.isActive ? 'danger' : 'secondary'"
                  text
                  size="small"
                  (onClick)="confirmToggle(brand)"
                />
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="3" class="admin-empty-state">
                {{ search() ? 'No brands match your search.' : 'No brands yet — create the first one.' }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  }
</div>
```

`frontend/src/app/features/admin/brands/brands-list/brands-list.scss` — full replacement (shared classes are global; nothing page-specific is needed):

```scss
// Layout comes from the global .admin-* classes (src/styles/_admin.scss).
```

- [ ] **Step 4: Run the spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/brands-list.spec.ts'`
Expected: PASS (5/5).
Run: `cd frontend && npm test`
Expected: all pass; only baseline noise.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/brands/brands-list
git commit -m "Restyle brands list with PrimeNG table, search, and confirm-deactivate"
```

---

### Task 3: BrandForm — canonical form page

**Files:**
- Modify: `frontend/src/app/features/admin/brands/brand-form/brand-form.ts`
- Modify: `frontend/src/app/features/admin/brands/brand-form/brand-form.html`
- Modify: `frontend/src/app/features/admin/brands/brand-form/brand-form.scss`
- Modify: `frontend/src/app/features/admin/brands/brand-form/brand-form.spec.ts`

**Interfaces:**
- Consumes: `BrandsService.get/create/update` (existing, unchanged); `MessageService` (AdminShell at runtime, TestBed in specs); global `.admin-*` classes.
- Produces: the canonical form-page pattern Tasks 5 and 7 transcribe: success toast before the existing awaited navigation, inline `p-message` for `errorMessage()`, `p-button [loading]="isSubmitting()"` submit.

- [ ] **Step 1: Add the new spec coverage**

In `frontend/src/app/features/admin/brands/brand-form/brand-form.spec.ts`: add `MessageService` to the imports and TestBed providers, and append two tests. The import line and providers list become:

```ts
import { MessageService } from 'primeng/api';
```

```ts
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(brandId ? { brandId } : {}) } },
        },
      ],
```

Append inside the `describe` block:

```ts
  it('toasts on successful save', async () => {
    await setUp(null);
    const fixture = TestBed.createComponent(BrandForm);
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');
    fixture.detectChanges();

    fixture.componentInstance['name'] = 'Don Picaso';
    const submitPromise = fixture.componentInstance.submit();
    httpMock.expectOne('/api/v1/brands').flush({
      id: 'b1', name: 'Don Picaso', isActive: true, createdAtUtc: '2026-07-08T00:00:00Z',
    });
    await submitPromise;

    expect(messageAddSpy).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success', summary: 'Brand created' }),
    );
  });

  it('shows the error inline when the save fails', async () => {
    await setUp(null);
    const fixture = TestBed.createComponent(BrandForm);
    fixture.detectChanges();

    fixture.componentInstance['name'] = 'Don Picaso';
    const submitPromise = fixture.componentInstance.submit();
    httpMock.expectOne('/api/v1/brands').flush('boom', { status: 500, statusText: 'Server Error' });
    await submitPromise;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('p-message')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Could not save this brand.');
  });
```

- [ ] **Step 2: Run the spec to verify the new tests fail**

Run: `cd frontend && npx ng test --watch=false --include='**/brand-form.spec.ts'`
Expected: FAIL — MessageService is never called; the error renders in a plain `<p>`, not `p-message`.

- [ ] **Step 3: Implement**

`frontend/src/app/features/admin/brands/brand-form/brand-form.ts` — full replacement:

```ts
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';

import { BrandsService } from '../../../../core/admin/brands.service';

@Component({
  selector: 'app-brand-form',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule],
  templateUrl: './brand-form.html',
  styleUrl: './brand-form.scss',
})
export class BrandForm implements OnInit {
  private readonly brandsService = inject(BrandsService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly brandId = signal<string | null>(null);
  protected name = '';
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  async ngOnInit(): Promise<void> {
    const brandId = this.route.snapshot.paramMap.get('brandId');
    if (!brandId) {
      return;
    }

    this.brandId.set(brandId);
    const brand = await this.brandsService.get(brandId);
    this.name = brand.name;
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      const brandId = this.brandId();
      if (brandId) {
        await this.brandsService.update(brandId, this.name);
      } else {
        await this.brandsService.create(this.name);
      }
      this.messageService.add({
        severity: 'success',
        summary: brandId ? 'Brand updated' : 'Brand created',
      });
      await this.router.navigateByUrl('/admin/brands');
    } catch {
      this.errorMessage.set('Could not save this brand.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
```

NOTE: the awaited `navigateByUrl` inside try/catch is pre-existing behavior — under `provideRouter([])` its rejection lands in the catch, which the existing tests tolerate. Do not change it, and add the toast BEFORE the navigation so the success test passes regardless.

`frontend/src/app/features/admin/brands/brand-form/brand-form.html` — full replacement:

```html
<div class="admin-page">
  <form class="admin-card admin-form-card" (ngSubmit)="submit()">
    <div class="admin-form-header">
      <h1>{{ brandId() ? 'Edit brand' : 'New brand' }}</h1>
      <a routerLink="/admin/brands" class="admin-cancel-link">Cancel</a>
    </div>

    <label for="name">Name</label>
    <input pInputText id="name" type="text" name="name" [(ngModel)]="name" required />

    @if (errorMessage()) {
      <p-message severity="error">{{ errorMessage() }}</p-message>
    }

    <p-button type="submit" label="Save" [loading]="isSubmitting()" [fluid]="true" />
  </form>
</div>
```

`frontend/src/app/features/admin/brands/brand-form/brand-form.scss` — full replacement:

```scss
// Layout comes from the global .admin-* classes (src/styles/_admin.scss).
```

- [ ] **Step 4: Run the spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/brand-form.spec.ts'`
Expected: PASS (4/4).
Run: `cd frontend && npm test`
Expected: all pass; only baseline noise.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/brands/brand-form
git commit -m "Restyle brand form with PrimeNG card, inline errors, and save toast"
```

---

### Task 4: BranchesList — transcribe the list pattern

**Files:**
- Modify: `frontend/src/app/features/admin/branches/branches-list/branches-list.ts`
- Modify: `frontend/src/app/features/admin/branches/branches-list/branches-list.html`
- Modify: `frontend/src/app/features/admin/branches/branches-list/branches-list.scss`
- Modify: `frontend/src/app/features/admin/branches/branches-list/branches-list.spec.ts`

**Interfaces:**
- Consumes: `BranchesService.list(brandId)`, `.deactivate(brandId, branchId)`, `.reactivate(brandId, branchId)` (existing); the Task 2 pattern; global `.admin-*` classes.
- Produces: nothing new.

- [ ] **Step 1: Update the spec**

`frontend/src/app/features/admin/branches/branches-list/branches-list.spec.ts` — full replacement (adds the providers plus one confirm-flow test; keeps the existing listing test):

```ts
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
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `cd frontend && npx ng test --watch=false --include='**/branches-list.spec.ts'`
Expected: FAIL — no `confirmToggle`, no `p-tag`.

- [ ] **Step 3: Implement**

`frontend/src/app/features/admin/branches/branches-list/branches-list.ts` — full replacement:

```ts
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { Branch } from '../../../../core/admin/admin.models';
import { BranchesService } from '../../../../core/admin/branches.service';

@Component({
  selector: 'app-branches-list',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, TableModule, TagModule],
  templateUrl: './branches-list.html',
  styleUrl: './branches-list.scss',
})
export class BranchesList implements OnInit {
  private readonly branchesService = inject(BranchesService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  protected readonly brandId = this.route.snapshot.paramMap.get('brandId')!;
  protected readonly branches = signal<Branch[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly search = signal('');

  protected readonly filteredBranches = computed(() => {
    const term = this.search().trim().toLowerCase();
    const branches = this.branches();
    return term ? branches.filter((branch) => branch.name.toLowerCase().includes(term)) : branches;
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set(false);
    try {
      this.branches.set(await this.branchesService.list(this.brandId));
    } catch {
      this.loadError.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  confirmToggle(branch: Branch): void {
    const confirmation: Confirmation = {
      header: branch.isActive ? 'Deactivate branch' : 'Reactivate branch',
      message: branch.isActive
        ? `Deactivate ${branch.name}? Staff there won't be able to sign in.`
        : `Reactivate ${branch.name}?`,
      acceptButtonProps: {
        label: branch.isActive ? 'Deactivate' : 'Reactivate',
        severity: branch.isActive ? 'danger' : 'primary',
      },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.toggleActive(branch),
    };
    this.confirmationService.confirm(confirmation);
  }

  async toggleActive(branch: Branch): Promise<void> {
    try {
      const updated = branch.isActive
        ? await this.branchesService.deactivate(this.brandId, branch.id)
        : await this.branchesService.reactivate(this.brandId, branch.id);

      this.branches.update((branches) => branches.map((b) => (b.id === updated.id ? updated : b)));
      this.messageService.add({
        severity: 'success',
        summary: updated.isActive ? 'Branch reactivated' : 'Branch deactivated',
      });
    } catch {
      this.messageService.add({ severity: 'error', summary: "Couldn't update the branch" });
    }
  }
}
```

`frontend/src/app/features/admin/branches/branches-list/branches-list.html` — full replacement:

```html
<div class="admin-page">
  <div class="admin-page-header">
    <h1>Branches</h1>
    <p-button label="New branch" icon="pi pi-plus" [routerLink]="['/admin/brands', brandId, 'branches', 'new']" />
  </div>

  @if (loadError()) {
    <p-message severity="error">
      Couldn't load branches.
      <button type="button" class="admin-retry-link" (click)="load()">Retry</button>
    </p-message>
  } @else {
    <div class="admin-card">
      <div class="admin-toolbar">
        <i class="pi pi-search"></i>
        <input
          pInputText
          type="text"
          name="search"
          placeholder="Search branches"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
        />
      </div>

      <div class="admin-table-scroll">
        <p-table [value]="filteredBranches()" [loading]="isLoading()" dataKey="id">
          <ng-template #header>
            <tr>
              <th pSortableColumn="name">Name <p-sortIcon field="name" /></th>
              <th>Status</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template #body let-branch>
            <tr>
              <td>
                <a [routerLink]="['/admin/brands', brandId, 'branches', branch.id]">{{ branch.name }}</a>
              </td>
              <td>
                <p-tag
                  [value]="branch.isActive ? 'Active' : 'Inactive'"
                  [severity]="branch.isActive ? 'success' : 'secondary'"
                />
              </td>
              <td class="admin-row-actions">
                <p-button
                  label="Users"
                  icon="pi pi-users"
                  severity="secondary"
                  text
                  size="small"
                  [routerLink]="['/admin/branches', branch.id, 'users']"
                  [queryParams]="{ brandId: branch.brandId }"
                />
                <p-button
                  [label]="branch.isActive ? 'Deactivate' : 'Reactivate'"
                  [severity]="branch.isActive ? 'danger' : 'secondary'"
                  text
                  size="small"
                  (onClick)="confirmToggle(branch)"
                />
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="3" class="admin-empty-state">
                {{ search() ? 'No branches match your search.' : 'No branches yet — create the first one.' }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  }
</div>
```

`frontend/src/app/features/admin/branches/branches-list/branches-list.scss` — full replacement:

```scss
// Layout comes from the global .admin-* classes (src/styles/_admin.scss).
```

- [ ] **Step 4: Run the spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/branches-list.spec.ts'`
Expected: PASS (2/2).
Run: `cd frontend && npm test`
Expected: all pass; only baseline noise.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/branches/branches-list
git commit -m "Restyle branches list with PrimeNG table, search, and confirm-deactivate"
```

---

### Task 5: BranchForm — transcribe the form pattern

**Files:**
- Modify: `frontend/src/app/features/admin/branches/branch-form/branch-form.ts`
- Modify: `frontend/src/app/features/admin/branches/branch-form/branch-form.html`
- Modify: `frontend/src/app/features/admin/branches/branch-form/branch-form.scss`
- Modify: `frontend/src/app/features/admin/branches/branch-form/branch-form.spec.ts`

**Interfaces:**
- Consumes: `BranchesService.get/create/update` (existing); the Task 3 pattern; global `.admin-*` classes.
- Produces: nothing new.

- [ ] **Step 1: Update the spec**

In `frontend/src/app/features/admin/branches/branch-form/branch-form.spec.ts`: add the import `import { MessageService } from 'primeng/api';`, add `MessageService,` to the TestBed providers array (after `provideRouter([]),`), and append one test inside the `describe`:

```ts
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
```

- [ ] **Step 2: Run the spec to verify the new test fails**

Run: `cd frontend && npx ng test --watch=false --include='**/branch-form.spec.ts'`
Expected: FAIL — MessageService never called.

- [ ] **Step 3: Implement**

`frontend/src/app/features/admin/branches/branch-form/branch-form.ts` — full replacement:

```ts
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';

import { BranchesService } from '../../../../core/admin/branches.service';

@Component({
  selector: 'app-branch-form',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule],
  templateUrl: './branch-form.html',
  styleUrl: './branch-form.scss',
})
export class BranchForm implements OnInit {
  private readonly branchesService = inject(BranchesService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly brandId = this.route.snapshot.paramMap.get('brandId')!;
  protected readonly branchId = signal<string | null>(null);
  protected name = '';
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  async ngOnInit(): Promise<void> {
    const branchId = this.route.snapshot.paramMap.get('branchId');
    if (!branchId) {
      return;
    }

    this.branchId.set(branchId);
    const branch = await this.branchesService.get(this.brandId, branchId);
    this.name = branch.name;
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      const branchId = this.branchId();
      if (branchId) {
        await this.branchesService.update(this.brandId, branchId, this.name);
      } else {
        await this.branchesService.create(this.brandId, this.name);
      }
      this.messageService.add({
        severity: 'success',
        summary: branchId ? 'Branch updated' : 'Branch created',
      });
      await this.router.navigateByUrl(`/admin/brands/${this.brandId}/branches`);
    } catch {
      this.errorMessage.set('Could not save this branch.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
```

`frontend/src/app/features/admin/branches/branch-form/branch-form.html` — full replacement:

```html
<div class="admin-page">
  <form class="admin-card admin-form-card" (ngSubmit)="submit()">
    <div class="admin-form-header">
      <h1>{{ branchId() ? 'Edit branch' : 'New branch' }}</h1>
      <a [routerLink]="['/admin/brands', brandId, 'branches']" class="admin-cancel-link">Cancel</a>
    </div>

    <label for="name">Name</label>
    <input pInputText id="name" type="text" name="name" [(ngModel)]="name" required />

    @if (errorMessage()) {
      <p-message severity="error">{{ errorMessage() }}</p-message>
    }

    <p-button type="submit" label="Save" [loading]="isSubmitting()" [fluid]="true" />
  </form>
</div>
```

`frontend/src/app/features/admin/branches/branch-form/branch-form.scss` — full replacement:

```scss
// Layout comes from the global .admin-* classes (src/styles/_admin.scss).
```

- [ ] **Step 4: Run the spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/branch-form.spec.ts'`
Expected: PASS (3/3).
Run: `cd frontend && npm test`
Expected: all pass; only baseline noise.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/branches/branch-form
git commit -m "Restyle branch form with PrimeNG card, inline errors, and save toast"
```

---

### Task 6: UsersList — transcribe the list pattern

**Files:**
- Modify: `frontend/src/app/features/admin/users/users-list/users-list.ts`
- Modify: `frontend/src/app/features/admin/users/users-list/users-list.html`
- Modify: `frontend/src/app/features/admin/users/users-list/users-list.scss`
- Modify: `frontend/src/app/features/admin/users/users-list/users-list.spec.ts`

**Interfaces:**
- Consumes: `UsersService.list({ branchId })`, `.deactivate(id)`, `.reactivate(id)` (existing); the Task 2 pattern; global `.admin-*` classes.
- Produces: nothing new. Search filters across displayName, role, and email.

- [ ] **Step 1: Update the spec**

`frontend/src/app/features/admin/users/users-list/users-list.spec.ts` — full replacement:

```ts
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
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `cd frontend && npx ng test --watch=false --include='**/users-list.spec.ts'`
Expected: FAIL — no `search` signal, no `confirmToggle`, no `p-tag`.

- [ ] **Step 3: Implement**

`frontend/src/app/features/admin/users/users-list/users-list.ts` — full replacement:

```ts
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { AdminUser } from '../../../../core/admin/admin.models';
import { UsersService } from '../../../../core/admin/users.service';

@Component({
  selector: 'app-users-list',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, TableModule, TagModule],
  templateUrl: './users-list.html',
  styleUrl: './users-list.scss',
})
export class UsersList implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  protected readonly branchId = this.route.snapshot.paramMap.get('branchId')!;
  protected readonly brandIdQueryParam = this.route.snapshot.queryParamMap.get('brandId');
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly search = signal('');

  protected readonly filteredUsers = computed(() => {
    const term = this.search().trim().toLowerCase();
    const users = this.users();
    if (!term) {
      return users;
    }
    return users.filter(
      (user) =>
        user.displayName.toLowerCase().includes(term) ||
        user.role.toLowerCase().includes(term) ||
        (user.email ?? '').toLowerCase().includes(term),
    );
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set(false);
    try {
      this.users.set(await this.usersService.list({ branchId: this.branchId }));
    } catch {
      this.loadError.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  confirmToggle(user: AdminUser): void {
    const confirmation: Confirmation = {
      header: user.isActive ? 'Deactivate user' : 'Reactivate user',
      message: user.isActive
        ? `Deactivate ${user.displayName}? They won't be able to sign in.`
        : `Reactivate ${user.displayName}?`,
      acceptButtonProps: {
        label: user.isActive ? 'Deactivate' : 'Reactivate',
        severity: user.isActive ? 'danger' : 'primary',
      },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.toggleActive(user),
    };
    this.confirmationService.confirm(confirmation);
  }

  async toggleActive(user: AdminUser): Promise<void> {
    try {
      const updated = user.isActive
        ? await this.usersService.deactivate(user.id)
        : await this.usersService.reactivate(user.id);

      this.users.update((users) => users.map((u) => (u.id === updated.id ? updated : u)));
      this.messageService.add({
        severity: 'success',
        summary: updated.isActive ? 'User reactivated' : 'User deactivated',
      });
    } catch {
      this.messageService.add({ severity: 'error', summary: "Couldn't update the user" });
    }
  }
}
```

`frontend/src/app/features/admin/users/users-list/users-list.html` — full replacement:

```html
<div class="admin-page">
  <div class="admin-page-header">
    <h1>Users</h1>
    <p-button
      label="New user"
      icon="pi pi-plus"
      [routerLink]="['/admin/branches', branchId, 'users', 'new']"
      [queryParams]="{ brandId: brandIdQueryParam }"
    />
  </div>

  @if (loadError()) {
    <p-message severity="error">
      Couldn't load users.
      <button type="button" class="admin-retry-link" (click)="load()">Retry</button>
    </p-message>
  } @else {
    <div class="admin-card">
      <div class="admin-toolbar">
        <i class="pi pi-search"></i>
        <input
          pInputText
          type="text"
          name="search"
          placeholder="Search users"
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
        />
      </div>

      <div class="admin-table-scroll">
        <p-table [value]="filteredUsers()" [loading]="isLoading()" dataKey="id">
          <ng-template #header>
            <tr>
              <th pSortableColumn="displayName">Name <p-sortIcon field="displayName" /></th>
              <th pSortableColumn="role">Role <p-sortIcon field="role" /></th>
              <th>Status</th>
              <th></th>
            </tr>
          </ng-template>
          <ng-template #body let-user>
            <tr>
              <td>
                <a
                  [routerLink]="['/admin/branches', branchId, 'users', user.id]"
                  [queryParams]="{ brandId: brandIdQueryParam }"
                >
                  {{ user.displayName }}
                </a>
              </td>
              <td>{{ user.role }}</td>
              <td>
                <p-tag
                  [value]="user.isActive ? 'Active' : 'Inactive'"
                  [severity]="user.isActive ? 'success' : 'secondary'"
                />
              </td>
              <td class="admin-row-actions">
                <p-button
                  [label]="user.isActive ? 'Deactivate' : 'Reactivate'"
                  [severity]="user.isActive ? 'danger' : 'secondary'"
                  text
                  size="small"
                  (onClick)="confirmToggle(user)"
                />
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="4" class="admin-empty-state">
                {{ search() ? 'No users match your search.' : 'No users yet — create the first one.' }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  }
</div>
```

`frontend/src/app/features/admin/users/users-list/users-list.scss` — full replacement:

```scss
// Layout comes from the global .admin-* classes (src/styles/_admin.scss).
```

- [ ] **Step 4: Run the spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/users-list.spec.ts'`
Expected: PASS (3/3).
Run: `cd frontend && npm test`
Expected: all pass; only baseline noise.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/users/users-list
git commit -m "Restyle users list with PrimeNG table, search, and confirm-deactivate"
```

---

### Task 7: UserForm — form pattern + role select + reset-credential confirm

**Files:**
- Modify: `frontend/src/app/features/admin/users/user-form/user-form.ts`
- Modify: `frontend/src/app/features/admin/users/user-form/user-form.html`
- Modify: `frontend/src/app/features/admin/users/user-form/user-form.scss`
- Modify: `frontend/src/app/features/admin/users/user-form/user-form.spec.ts`

**Interfaces:**
- Consumes: `UsersService.get/create/update/resetCredential` (existing, unchanged); the Task 3 pattern; global `.admin-*` classes.
- Produces: nothing new. `submit()` and `resetCredential()` keep their public signatures — the confirm dialog wraps `resetCredential` in a new `confirmResetCredential()`.

Behavior changes in this task (all UI-level):
- Role `<select>` becomes `p-select` fed by a `roleOptions` array.
- Reset credential success feedback moves from the inline `credentialResetMessage` to a success toast; `credentialResetMessage` remains for the error case only.
- The template's Reset button calls the new `confirmResetCredential()`; specs keep calling `resetCredential()` directly.

- [ ] **Step 1: Update the spec**

In `frontend/src/app/features/admin/users/user-form/user-form.spec.ts`:

1. Add the import: `import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';` (replace nothing — it's a new line).
2. Add `MessageService,` and `ConfirmationService,` to the TestBed providers (after `provideRouter([]),`).
3. In the test `resets a staff members PIN independently of the main save`, replace the final assertion line

```ts
    expect(fixture.componentInstance['credentialResetMessage']()).toBe('Credential updated.');
```

with:

```ts
    expect(messageAddSpy).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'success', summary: 'Credential updated' }),
    );
```

and add this line right after the fixture is created in that test:

```ts
    const messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');
```

4. Append one new test inside the `describe`:

```ts
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
```

- [ ] **Step 2: Run the spec to verify the changed/new tests fail**

Run: `cd frontend && npx ng test --watch=false --include='**/user-form.spec.ts'`
Expected: FAIL — no toast on reset, no `confirmResetCredential`.

- [ ] **Step 3: Implement**

`frontend/src/app/features/admin/users/user-form/user-form.ts` — full replacement:

```ts
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';

import { Role } from '../../../../core/auth/auth.models';
import { UsersService } from '../../../../core/admin/users.service';

@Component({
  selector: 'app-user-form',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, PasswordModule, SelectModule],
  templateUrl: './user-form.html',
  styleUrl: './user-form.scss',
})
export class UserForm implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly returnBranchId = this.route.snapshot.paramMap.get('branchId')!;

  protected readonly Role = Role;
  protected readonly userId = signal<string | null>(null);

  protected readonly roleOptions = [
    { label: 'Corporate', value: Role.Corporate },
    { label: 'Brand owner', value: Role.BrandOwner },
    { label: 'Branch manager', value: Role.BranchManager },
    { label: 'Staff', value: Role.Staff },
  ];

  protected displayName = '';
  protected role: Role = Role.Staff;
  protected email = '';
  protected brandId = this.route.snapshot.queryParamMap.get('brandId') ?? '';
  protected branchId = this.returnBranchId;
  protected password = '';
  protected pin = '';

  protected newPassword = '';
  protected newPin = '';

  protected readonly errorMessage = signal<string | null>(null);
  protected readonly credentialResetMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  protected readonly isStaff = computed(() => this.role === Role.Staff);

  async ngOnInit(): Promise<void> {
    const userId = this.route.snapshot.paramMap.get('userId');
    if (!userId) {
      return;
    }

    this.userId.set(userId);
    const user = await this.usersService.get(userId);
    this.displayName = user.displayName;
    this.role = user.role;
    this.email = user.email ?? '';
    this.brandId = user.brandId ?? '';
    this.branchId = user.branchId ?? '';
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const scopesToABranch = this.role === Role.BranchManager || this.role === Role.Staff;

    try {
      const userId = this.userId();
      if (userId) {
        await this.usersService.update(userId, {
          displayName: this.displayName,
          role: this.role,
          brandId: this.role === Role.Corporate ? null : this.brandId || null,
          branchId: scopesToABranch ? this.branchId || null : null,
          email: this.isStaff() ? null : this.email || null,
          newPassword: this.newPassword || null,
          newPin: this.newPin || null,
        });
      } else {
        await this.usersService.create({
          email: this.isStaff() ? null : this.email || null,
          displayName: this.displayName,
          role: this.role,
          brandId: this.role === Role.Corporate ? null : this.brandId || null,
          branchId: scopesToABranch ? this.branchId || null : null,
          password: this.isStaff() ? null : this.password || null,
          pin: this.isStaff() ? this.pin || null : null,
        });
      }

      this.messageService.add({
        severity: 'success',
        summary: this.userId() ? 'User updated' : 'User created',
      });
      await this.router.navigateByUrl(`/admin/branches/${this.returnBranchId}/users`);
    } catch {
      this.errorMessage.set('Could not save this user.');
    } finally {
      this.isSubmitting.set(false);
    }
  }

  protected confirmResetCredential(): void {
    const confirmation: Confirmation = {
      header: 'Reset credential',
      message: `Reset the ${this.isStaff() ? 'PIN' : 'password'} for ${this.displayName}?`,
      acceptButtonProps: { label: 'Reset', severity: 'danger' },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.resetCredential(),
    };
    this.confirmationService.confirm(confirmation);
  }

  async resetCredential(): Promise<void> {
    const userId = this.userId();
    if (!userId) {
      return;
    }

    this.credentialResetMessage.set(null);

    try {
      await this.usersService.resetCredential(userId, {
        newPassword: this.isStaff() ? null : this.newPassword || null,
        newPin: this.isStaff() ? this.newPin || null : null,
      });
      this.messageService.add({ severity: 'success', summary: 'Credential updated' });
      this.newPassword = '';
      this.newPin = '';
    } catch {
      this.credentialResetMessage.set('Could not reset the credential.');
    }
  }
}
```

`frontend/src/app/features/admin/users/user-form/user-form.html` — full replacement:

```html
<div class="admin-page">
  <form class="admin-card admin-form-card" (ngSubmit)="submit()">
    <div class="admin-form-header">
      <h1>{{ userId() ? 'Edit user' : 'New user' }}</h1>
      <a
        [routerLink]="['/admin/branches', branchId, 'users']"
        [queryParams]="{ brandId: brandId || null }"
        class="admin-cancel-link"
      >
        Cancel
      </a>
    </div>

    <label for="displayName">Display name</label>
    <input pInputText id="displayName" type="text" name="displayName" [(ngModel)]="displayName" required />

    <label for="role">Role</label>
    <p-select
      inputId="role"
      name="role"
      [(ngModel)]="role"
      [options]="roleOptions"
      optionLabel="label"
      optionValue="value"
    />

    @if (role !== Role.Corporate) {
      <label for="brandId">Brand id</label>
      <input pInputText id="brandId" type="text" name="brandId" [(ngModel)]="brandId" required />
    }

    @if (role === Role.BranchManager || role === Role.Staff) {
      <label for="branchId">Branch id</label>
      <input pInputText id="branchId" type="text" name="branchId" [(ngModel)]="branchId" required />
    }

    @if (!isStaff()) {
      <label for="email">Email</label>
      <input pInputText id="email" type="email" name="email" [(ngModel)]="email" required />
    }

    @if (!userId()) {
      @if (isStaff()) {
        <label for="pin">PIN</label>
        <input
          pInputText
          id="pin"
          type="password"
          inputmode="numeric"
          name="pin"
          [(ngModel)]="pin"
          required
        />
      } @else {
        <label for="password">Password</label>
        <p-password
          inputId="password"
          name="password"
          [(ngModel)]="password"
          [feedback]="false"
          [toggleMask]="true"
          [fluid]="true"
          required
        />
      }
    }

    @if (errorMessage()) {
      <p-message severity="error">{{ errorMessage() }}</p-message>
    }

    <p-button type="submit" label="Save" [loading]="isSubmitting()" [fluid]="true" />
  </form>

  @if (userId()) {
    <section class="admin-card admin-form-card credential-reset">
      <h2>Reset credential</h2>

      @if (isStaff()) {
        <label for="newPin">New PIN</label>
        <input
          pInputText
          id="newPin"
          type="password"
          inputmode="numeric"
          name="newPin"
          [(ngModel)]="newPin"
        />
      } @else {
        <label for="newPassword">New password</label>
        <p-password
          inputId="newPassword"
          name="newPassword"
          [(ngModel)]="newPassword"
          [feedback]="false"
          [toggleMask]="true"
          [fluid]="true"
        />
      }

      @if (credentialResetMessage()) {
        <p-message severity="error">{{ credentialResetMessage() }}</p-message>
      }

      <p-button
        type="button"
        label="Reset credential"
        severity="secondary"
        [outlined]="true"
        (onClick)="confirmResetCredential()"
      />
    </section>
  }
</div>
```

`frontend/src/app/features/admin/users/user-form/user-form.scss` — full replacement:

```scss
.credential-reset {
  margin-top: 1rem;

  h2 {
    margin: 0;
    font-size: 1.1rem;
  }
}
```

- [ ] **Step 4: Run the spec, then the full suite**

Run: `cd frontend && npx ng test --watch=false --include='**/user-form.spec.ts'`
Expected: PASS (4/4).
Run: `cd frontend && npm test`
Expected: all pass; only baseline noise.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/features/admin/users/user-form
git commit -m "Restyle user form with PrimeNG inputs, role select, and reset-credential confirm"
```

---

### Task 8: POS responsive CSS

**Files:**
- Modify: `frontend/src/app/features/pos/pos-shell/pos-shell.scss`
- Modify: `frontend/src/app/features/pos/product-catalog/product-catalog.scss`

**Interfaces:**
- Consumes: existing class names only. NO template or TypeScript changes.
- Produces: POS usable at 768px width and up.

- [ ] **Step 1: Add the media queries**

In `frontend/src/app/features/pos/pos-shell/pos-shell.scss`, append at the end of the file:

```scss
@media (max-width: 1023px) {
  .pos-main {
    grid-template-columns: 1fr 280px;
    gap: 0.75rem;
    padding: 0.75rem;
  }

  .pos-topbar {
    padding: 0.5rem 0.75rem;
  }
}
```

In `frontend/src/app/features/pos/product-catalog/product-catalog.scss`, append at the end of the file:

```scss
@media (max-width: 1023px) {
  .product-grid {
    grid-template-columns: repeat(auto-fill, minmax(128px, 1fr));
    gap: 0.75rem;
  }

  .category-tabs .category-tab {
    min-width: 84px;
    padding: 0.5rem 0.75rem;
  }
}
```

(The category tab row already has `overflow-x: auto`, and the cart panel already stretches to its grid column — no other POS file changes.)

- [ ] **Step 2: Verify build and tests**

Run: `cd frontend && npm test && npm run build`
Expected: all tests pass (CSS-only change), build clean.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/features/pos/pos-shell/pos-shell.scss frontend/src/app/features/pos/product-catalog/product-catalog.scss
git commit -m "Make POS layout fluid down to tablet-portrait width"
```

---

### Task 9: Auth responsive CSS

**Files:**
- Modify: `frontend/src/app/features/auth/login/login.scss`
- Modify: `frontend/src/app/features/auth/staff-login/staff-login.scss`
- Modify: `frontend/src/app/features/auth/device-setup/device-setup.scss`

**Interfaces:**
- Consumes: existing class names only. NO template or TypeScript changes.
- Produces: auth pages usable at 360px width and up.

- [ ] **Step 1: Add the media queries**

In `frontend/src/app/features/auth/login/login.scss`, append at the end:

```scss
@media (max-width: 400px) {
  .login-card {
    padding: 1.75rem 1.25rem;
  }
}
```

In `frontend/src/app/features/auth/staff-login/staff-login.scss`, append at the end:

```scss
@media (max-width: 400px) {
  .panel {
    padding: 1.5rem 1rem;
  }

  .digits {
    gap: 0.5rem;
  }
}
```

In `frontend/src/app/features/auth/device-setup/device-setup.scss`, change the `.device-setup-form` rule's `margin` line from `margin: 4rem auto;` to:

```scss
  margin: 4rem auto;
  padding: 0 1rem;
```

- [ ] **Step 2: Verify build and tests**

Run: `cd frontend && npm test && npm run build`
Expected: all tests pass, build clean.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/features/auth/login/login.scss frontend/src/app/features/auth/staff-login/staff-login.scss frontend/src/app/features/auth/device-setup/device-setup.scss
git commit -m "Tighten auth page spacing at phone widths"
```

---

### Task 10: Browser verification at all supported widths

**Files:**
- No committed files. A throwaway Playwright script runs from the session scratchpad (Playwright + Chromium are already installed there from earlier sessions; if missing, `npm i playwright && npx playwright install chromium` in the scratchpad directory). Fixes, if any are needed, are committed to the files they touch.

**Interfaces:**
- Consumes: running dev stack — Postgres (`docker compose up -d`), API (`dotnet run --project src/RestaurantEmpire.Api`), frontend (`cd frontend && npm start`). Dev seed credentials: `corporate@donpicaso.dev` / `Password123!`; staff PIN `1234`; the seeded branch id can be read from the JWT of `manager@donpicaso.dev` or the admin UI.
- Produces: screenshots reviewed against the spec's responsive rules.

- [ ] **Step 1: Capture screenshots**

With the dev stack running, drive Chromium (Playwright) through: `/login`, `/staff-login` (device bound via `localStorage['donpicaso.deviceBranchId']`), `/pos` (staff PIN session), `/admin/brands` + one branches list + one users list + one user form (corporate session) — at viewport widths **1366, 1024, 768** (all pages) and **390** (admin + auth pages only; POS floor is 768). Screenshot each.

- [ ] **Step 2: Review every screenshot**

Check against the spec: no horizontal page scroll anywhere (assert `document.documentElement.scrollWidth <= window.innerWidth` on each page/width); admin tables scroll inside `.admin-table-scroll`, not the page; POS at 768 shows catalog + 280px cart side by side; topbar wraps without overflow at 390; login/PIN/device-setup fit 390 (and 360 if any doubt — the floor is 360).

- [ ] **Step 3: Fix regressions found, or record clean result**

Trivial CSS fixes (a padding, an overflow) are made, re-verified, and committed to the touched files with message `"Fix responsive overflow found in browser verification"`. Anything non-trivial is reported to the controller instead of being improvised. If everything is clean, record that in the task report — this task then produces no commit.

---

## Spec Coverage Map

| Spec section | Tasks |
|---|---|
| Section 1 — shell, toast/confirm wiring, neutral topbar | 1 |
| Section 2 — list pages (search, sort, tags, confirm, states) | 2 (canonical), 4, 6 |
| Section 3 — form pages (card, inputs, p-select, toasts, reset confirm) | 3 (canonical), 5, 7 |
| Section 4 — responsive: admin | 1 (topbar/page), 2–7 (table scroll/cards via shared classes) |
| Section 4 — responsive: POS | 8 |
| Section 4 — responsive: auth | 9 |
| Error handling (list retry, save toasts/inline) | 2–7 |
| Testing (canonical DOM coverage + screenshot verification) | 2, 3, 10 |

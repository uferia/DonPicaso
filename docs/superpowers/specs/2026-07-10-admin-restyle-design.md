# Admin Area Restyle (PrimeNG) — Design

**Date:** 2026-07-10
**Status:** Approved

## Problem

The `/admin` area (Brands, Branches, Users — three list pages and three form
pages inside AdminShell) is plain unstyled HTML. The previous phase scoped
PrimeNG to the POS, login, and staff-PIN screens only, deliberately leaving
the admin back office untouched. That boundary is now retired: this phase
brings the admin area up to the same visual standard.

## Scope decisions (user-approved)

- **Depth:** reskin + UX niceties — PrimeNG components in place of plain
  HTML, plus toasts on save, confirm dialog before deactivate/reactivate,
  loading states, and empty states. Navigation structure stays as-is
  (topbar links, separate form pages — no sidebar, no dialogs-as-forms).
- **Tables:** sortable columns and a client-side search box per list.
  No pagination (lists are small).
- **Header look:** neutral white topbar with emerald accents — deliberately
  distinct from the solid-emerald POS register bar.
- **Approach:** shared building blocks (Approach A) — toast/confirm wired
  once in AdminShell; one canonical list treatment and one canonical form
  treatment rolled across all six pages, so menu management later inherits
  the pattern.

- **Responsive (user-added):** the whole app must be viewable on laptop
  and tablet (both orientations); the admin and auth pages must also work
  on modern phones. Today the frontend contains zero media queries.

Out of scope: any backend or API change, any route change, any service
change, menu management, order history, product images, a phone-usable
POS register (POS floor is tablet portrait).

## Section 1 — Admin shell & shared plumbing

AdminShell mirrors PosShell's one-time wiring:

- **Topbar:** white, thin bottom border, sticky. Left: small emerald
  `pi-shop` brand mark + "Don Picaso Admin", then the existing
  role-conditional nav links restyled as PrimeNG text buttons with a
  `routerLinkActive` emerald active state. Right: current role as a
  `p-tag`, then Log out as a PrimeNG text button with `pi-sign-out`.
  (The previous phase's "plain logout button" constraint is retired.)
- **Providers:** `MessageService` and `ConfirmationService` provided on
  AdminShell, which hosts `<p-toast>` and `<p-confirmdialog>` once. Child
  pages inject and call — no per-page wiring.
- **Content area:** light `--p-surface-50` page background, centered
  max-width container, consistent padding, so white cards read as surfaces.
- Logout behavior unchanged (`await authService.logout()` then
  `void router.navigateByUrl('/login')`); the existing logout spec keeps
  passing.

## Section 2 — List pages (Brands, Branches, Users)

One canonical treatment, identical across all three:

- **Page header row:** title left, primary action right ("New branch") as
  an emerald `p-button`. Titles are static ("Brands", "Branches", "Users") —
  no parent-entity subtitles, because the parent's name is not in the data
  these pages already fetch and this phase adds no API calls.
- **Toolbar:** one search input (`pInputText` + `pi-search` icon) filtering
  client-side across the visible text columns (name; email/role on Users).
  No server round-trip.
- **Table:** `p-table` on a white card. Sortable: name (plus role/email on
  Users). Status column rendered as `p-tag` — emerald "Active", gray
  "Inactive".
- **Row actions:** Edit (text button, `pi-pencil`) navigating to the
  existing form routes. Deactivate/Reactivate (text button, warn color for
  deactivate) goes through the shared `ConfirmationService` with an
  entity-specific message (e.g. "Deactivate *Espresso Corner*? Staff there
  won't be able to sign in."). Confirm → existing API call → list refresh →
  toast ("Branch deactivated").
- **States:** `p-table` `loading` spinner while fetching; empty-state
  template ("No branches yet — create the first one"); filter-miss message
  when search excludes all rows.
- Existing navigation flows (Brands → Branches → Users chaining, query
  params) untouched.

## Section 3 — Form pages (Brand, Branch, User)

One canonical treatment across all three create/edit pages:

- White card (max-width ~560px), page header with title ("Edit user" /
  "New brand") and a Cancel link back to the list.
- Inputs: `pInputText` for text/email; `p-password` (masked, `[feedback]`
  off) for password; PIN as `pInputText` with `inputmode="numeric"`; the
  Users form's role selection as `p-select`, options still produced by the
  existing role-based filtering logic (unchanged).
- Labels above inputs (same convention as the login card). API errors as
  inline `p-message severity="error"`, replacing plain error text.
- Save: emerald `p-button` with `[loading]` bound to the existing
  submitting flag; on success, toast ("User created") then the existing
  navigation back to the list.
- The Users form's **Reset credential** action becomes a secondary
  (outlined) button with its own confirm dialog; it stays fully independent
  of Save, exactly as currently built.

## Section 4 — Responsive design

Targets: POS usable at 768px width and up (tablet portrait → laptop);
admin and auth usable at 360px and up (modern phones). No horizontal page
scroll at any supported width — wide content scrolls inside its own
container.

**POS (`/pos`):** keep the side-by-side catalog + checkout layout at all
supported widths, made fluid rather than redesigned:

- The checkout panel gets a narrower minimum width (~280px) below 1024px;
  the catalog takes the remainder, and the existing auto-fill product grid
  reflows to fewer columns on its own.
- Topbar, search bar, and category tabs tighten padding below 1024px; the
  category tab row scrolls horizontally if it overflows.
- The payment dialog is already a modal with fluid width — verified, not
  redesigned. No stacked/bottom-sheet cart this phase.

**Admin:** built responsive as part of the restyle, not retrofitted:

- Topbar wraps to two rows on narrow screens; below ~480px nav buttons
  compress (tighter padding, keep icons + labels while they fit).
- Each list table sits in an `overflow-x: auto` wrapper inside its card,
  so phones scroll the table horizontally instead of breaking the page.
- Page container padding steps down at tablet and phone widths; form cards
  go full-width with reduced padding below ~480px.

**Auth (login, staff PIN, device setup):** already near-fluid
(`min(…, 90vw)` cards); verify at 360px and fix any overflow — notably the
PIN digit grid and card paddings.

## Testing

- Frontend-only: zero backend, route, or service changes.
- Existing specs mostly assert `textContent` and drive class APIs and
  survive as-is. Where a spec queries swapped DOM (e.g. brands-list table
  cells), update selectors to the new structure with equivalent
  assertions — never weakened.
- New coverage: one list page proves sort + filter + confirm-deactivate
  against real rendered DOM; one form page proves toast-on-save and the
  inline error message. The remaining pages reuse the proven pattern with
  their existing behavioral specs.
- Test-environment constraints carried forward: specs run under
  `provideRouter([])`, so tested navigations follow the established
  `void navigateByUrl` pattern; the single pre-existing NG04002 baseline
  noise line (staff-login.spec.ts) remains accepted.
- Responsive behavior is CSS-only and jsdom cannot measure layout, so it
  is verified with browser screenshots (Playwright) at 1366, 1024, 768,
  and 390px widths across POS, admin list/form, and auth pages as part of
  the implementation's verification step — not with unit specs.

## Error handling

- API failures on list load: inline `p-message` with a retry affordance in
  the card body (replacing any current plain-text error), consistent with
  the POS menu-unavailable pattern.
- API failures on save/deactivate: error toast plus the form's inline
  `p-message` where field context matters; never silent.

## Units and boundaries

- AdminShell: layout + toast/confirm hosting only; no data fetching.
- Each list/form component keeps its current data-service dependencies
  (BrandsService/BranchesService/UsersService) and behavior; this phase
  changes their templates/styles and adds confirm/toast calls only.
- Shared visual conventions live in the canonical treatments (the first
  list page and first form page built become the reference the others
  transcribe), not in a premature shared-component library. If a third
  consumer of an identical fragment appears later (e.g. menu management),
  extraction can happen then.

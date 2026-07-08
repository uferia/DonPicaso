# POS Ordering UI + Menu Catalog — Design

## Purpose

The Admin Dashboard sub-project shipped CRUD for Brands/Branches/Users, but `/pos` still
renders `pos-placeholder.ts` and the backend has no menu concept — nothing supplies the
`productId`/`unitPrice` the existing order contract expects. This is the third planned
sub-project (Identity/Login → Admin Dashboard → **Menu/POS**). It delivers the staff-facing
POS ordering screen (modeled on the green/white tablet-POS reference mockups), the backend
menu catalog it reads from, a full payment step (cash/card with change due), and two auth
UX gaps: a logout affordance (the service method exists but no UI calls it) and a
redesigned login experience. PrimeNG is introduced as the app's component library,
scoped in this phase to the POS and auth screens only.

## Current State (verified)

- `frontend/src/app/features/pos/pos-placeholder.ts` is a one-line placeholder behind
  `roleGuard(Role.Staff)`.
- `Modules.Sales` has only `CreateOrder` (`POST /api/v1/orders`): `clientOrderId`
  (idempotency key), `branchId`, `brandId`, `totalAmount`, `items[{productId,
  productName, quantity, unitPrice}]`. No discount, tax, or payment fields.
- No product/menu catalog exists anywhere — no entities, endpoints, or seed data.
- `core/offline/` has a Dexie `pendingOrders` queue (`OfflineOrderDb`, schema v1; payload
  stored as an opaque object, not indexed) and `OrderSyncService` for offline-first order
  replay with idempotent `clientOrderId`.
- `AuthService.logout()` exists (best-effort server revoke + unconditional local clear)
  but has zero UI call sites. Admin shell and POS have no logout control.
- Login page is a plain template-driven form; staff-login already has a
  roster-select + PIN-pad interaction. The device's branch binding
  (`donpicaso.deviceBranchId` in localStorage) is stored independently of the auth
  session, so logging out does not un-provision the device.
- PrimeNG is not installed. Frontend is Angular 21 standalone components + signals,
  SCSS, Vitest.

## Scope

In scope:

1. New backend `Modules.Menu` vertical module (own `DbContext` + migrations, wired via
   `AddMenuModule()`/`MapMenuModule()`): brand-scoped `Category` and `Product` entities,
   a `GetMenu` read endpoint, and dev seed data.
2. Extend `Modules.Sales` `Order` with money breakdown (subtotal, discount, tax) and
   payment fields (method, cash tendered, change due), with server-side re-validation of
   the money math.
3. Install PrimeNG (+ `@primeuix/themes`, `primeicons`) with a green-primary Aura preset.
4. Replace `pos-placeholder.ts` with the full POS ordering screen: product grid with
   search, bottom category tabs, checkout cart panel with quantity steppers and
   discount %, and a payment dialog (cash/card, change due) that submits through the
   existing offline-first sync path.
5. Menu read service with an offline cache so the POS remains usable when the tablet is
   disconnected.
6. Logout wiring: admin shell top bar (→ `/login`) and POS top bar (→ `/staff-login`).
7. Redesigned login page and restyled staff PIN screen using PrimeNG (same behavior).
8. Vitest coverage for cart math, menu caching, payment computation, and the new
   components; MSTest coverage for `GetMenu` and the extended `CreateOrder` validation.

Out of scope (explicitly deferred):

- Admin CRUD pages for categories/products (menu management) — next phase; this phase is
  seed-data + read API only.
- Image upload/storage. `Product.ImageUrl` exists but seed data leaves it null; the POS
  renders styled placeholder tiles. Real images plug in later without schema changes.
- Hold/park order, order history, receipts/printing, kitchen display.
- Card payment *processing* — "Card" is recorded as the payment method only.
- Per-branch tax configuration — tax rate is a single backend config value for now.
- Migrating existing admin pages (Brands/Branches/Users) to PrimeNG. The only admin-shell
  change is the logout button, styled to fit the current shell.

## Backend: Modules.Menu

Third vertical module following the Identity/Sales pattern: one folder per feature with
Handler/Validator/Endpoint, own `MenuDbContext` + Npgsql migrations, registered from
`Program.cs`.

Entities (brand-scoped — the tenancy model shares a Brand's menu across its branches):

- `Category`: `Id`, `BrandId`, `Name`, `DisplayOrder`, `IsActive`.
- `Product`: `Id`, `CategoryId`, `BrandId`, `Name`, `Price` (decimal), `ImageUrl`
  (nullable), `DisplayOrder`, `IsActive`.

Feature — `GetMenu` (`GET /api/v1/menu`):

- Authenticated, `Staff` and above. Brand resolved from the JWT `brandId` claim — the
  client never supplies a brand id (same trust model as staff login).
- Returns active categories (by `DisplayOrder`) with nested active products, plus
  `taxRatePercent` read from configuration (`Menu:TaxRatePercent` in `appsettings.json`).
- No cross-module coupling: orders continue to snapshot `productName`/`unitPrice` at
  order time, so Sales never joins against Menu tables.

Seeding: dev-only `MenuSeeder` (same startup hook pattern as `IdentitySeeder`) creating
several categories (e.g. Coffee, Beverages, Snacks, Desserts) with a handful of products
each for the seeded brand. `ImageUrl` stays null.

## Backend: Order payment extension

`Order` (and the `CreateOrderCommand` contract) gains:

- `Subtotal`, `DiscountPercent`, `DiscountAmount`, `TaxRatePercent`, `TaxAmount` —
  `TotalAmount` remains and stays the authoritative charged amount.
- `PaymentMethod` (enum: `Cash`, `Card`), `CashTendered` (nullable decimal), `ChangeDue`
  (nullable decimal). Both nullable fields required-and-consistent for `Cash`, must be
  null for `Card`.

`CreateOrderCommandValidator` re-checks the money math the client computed:

- `Subtotal == Σ(quantity × unitPrice)` over items.
- `DiscountAmount == round(Subtotal × DiscountPercent / 100, 2)`; `DiscountPercent`
  in [0, 100].
- `TaxAmount == round((Subtotal − DiscountAmount) × TaxRatePercent / 100, 2)`.
- `TotalAmount == Subtotal − DiscountAmount + TaxAmount`.
- Cash: `CashTendered ≥ TotalAmount` and `ChangeDue == CashTendered − TotalAmount`.

Rounding rule: half-up to 2 decimals, applied identically in the Angular cart service and
the validator so a client-computed order always passes server validation.

One Sales migration adds the new columns. Existing rows: not a concern — dev-stage data.

## Frontend: PrimeNG setup

- Add `primeng` (v21, matching Angular 21), `@primeuix/themes`, `primeicons`.
- `app.config.ts`: `providePrimeNG` with an Aura preset customized via `definePreset` to
  a green primary palette (mockup's green/white look); dark mode selector disabled.
- `providers` also gain PrimeNG's `MessageService`/`ConfirmationService` where used
  (scoped to the POS shell, not root, since only POS uses toast/confirm this phase).

## Frontend: POS screen

New `core/menu/menu.service.ts`:

- `loadMenu()` fetches `GET /api/v1/menu`; on success caches the response JSON in
  localStorage (`donpicaso.menuCache`). On failure, falls back to the cache. No cache and
  no network → the POS shows a "can't load menu / Retry" state. Exposes signals:
  `categories`, `taxRatePercent`, `source` (`network | cache | unavailable`).

New `features/pos/cart.service.ts` (provided in the POS shell, not root):

- Signal state: `lines` (`{product, quantity}[]`), `discountPercent`.
- Computed: `subtotal`, `discountAmount`, `taxAmount`, `total` (using
  `menu.taxRatePercent` and the shared rounding rule).
- Operations: `add(product)` (increments if present), `increment`/`decrement` (0 removes),
  `remove(line)`, `setDiscountPercent(pct)` (clamped 0–100), `clear()`.
- Pure and unit-tested; no HTTP.

`features/pos/pos-shell/` (replaces `pos-placeholder.ts` at `/pos`, layout per the first
reference mockup):

- **Top bar** (green): brand/app name, offline indicator when menu came from cache or
  orders are queued, staff name, logout button.
- **Main area**: search `p-inputtext` filtering the active category's products by name;
  product tile grid (CSS grid of tappable cards — image when `imageUrl` set, otherwise a
  styled placeholder with the product's initials; name; price). Tap adds to cart.
- **Bottom**: category tab bar (icon + label per category, active state per mockup).
- **Right panel — Checkout**: cart lines (name, `−`/qty/`+` steppers via `p-button`,
  remove icon, line price), discount % `p-inputnumber`, Sub Total / Discount / Tax /
  Total rows, full-width **Pay ($total)** `p-button` (disabled when cart empty), and
  **Cancel Order** (outlined, danger) which opens `p-confirmdialog` before `clear()`.

`features/pos/payment-dialog/`:

- `p-dialog` opened by Pay. `p-selectbutton` for Cash/Card. Cash shows a tendered-amount
  `p-inputnumber` with live change due (negative → confirm disabled). Card shows no extra
  fields.
- Confirm builds the full order payload (items snapshot + money breakdown + payment
  fields) and hands it to `OrderSyncService` exactly as today (online POST or Dexie
  queue). `CreateOrderPayload`/`NewOrder` types in `offline-order-db.ts` are extended to
  mirror the new contract — no Dexie schema version bump needed (payload isn't indexed).
- On success: `p-toast` ("Order placed" or "Order queued — offline"), dialog closes, cart
  resets. Discount resets with the cart.

## Frontend: auth polish

- **Login** (`features/auth/login/`): logic unchanged; new visual — centered card on a
  soft green-tinted background, app name/logo, `p-inputtext` (email), `p-password`
  (masked, toggle), full-width primary submit with loading state, `p-message` for the
  error.
- **Staff PIN screen** (`features/auth/staff-login/`): identical behavior (roster select
  → PIN pad → submit); restyled with PrimeNG buttons/cards to match the login page.
- **Logout**:
  - Admin shell top bar: shows current user identity + Logout button →
    `authService.logout()` → navigate `/login`. Plain button styled to the existing
    shell — no other admin changes.
  - POS top bar: Logout button → if cart non-empty, `p-confirmdialog` warns the cart
    will be discarded → `authService.logout()` → navigate `/staff-login`. Device branch
    binding is untouched, so the tablet lands on the PIN screen for the next staff
    member.

## Error handling

- Menu fetch failure → cache fallback → explicit unavailable/Retry state (never a blank
  grid with no explanation).
- Order submission uses the existing offline-first path; the payment dialog surfaces
  "queued" vs "placed" honestly via the toast.
- Server-side money-math validation failures return 400 with FluentValidation details;
  the POS treats this as a bug-surface (shows a generic "couldn't place order" toast and
  keeps the cart intact) since a correct client never triggers it.
- Logout remains best-effort server-side + unconditional local clear (existing behavior).

## Testing

Backend (MSTest + FluentAssertions + Moq, in-memory EF):

- `GetMenu`: returns only active categories/products for the caller's brand, ordered by
  `DisplayOrder`; includes configured tax rate; excludes other brands.
- `CreateOrder` validator: each money-math rule above (pass + fail cases), cash/card
  field consistency, rounding edge cases.

Frontend (Vitest, existing patterns):

- `CartService`: add/increment/decrement/remove/clear, discount clamping, subtotal/
  discount/tax/total math including rounding edges (e.g. 3 × $0.10 with 1.5% tax).
- `MenuService`: network success populates cache; failure falls back to cache; neither →
  `unavailable`.
- Payment dialog: change-due computation, confirm disabled when tendered < total,
  payload shape handed to `OrderSyncService`.
- POS shell: renders categories/products from a stubbed menu, add-to-cart wiring, search
  filter, logout confirm when cart non-empty.
- Login/admin-shell: logout button calls `logout()` and navigates to the right route.

## Sub-project note

This phase is sizeable but coherent (one backend module + one order extension + one
frontend feature + auth polish). The natural follow-on phases, already deferred above:
menu admin CRUD (with image handling), and any real payment processing.

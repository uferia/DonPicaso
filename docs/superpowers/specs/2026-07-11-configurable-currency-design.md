# Configurable Currency (PHP Default) — Design

**Date:** 2026-07-11
**Status:** Approved

## Problem

Every money value in the POS renders through Angular's bare `| currency`
pipe, which hardcodes USD ("$2.50"). The platform runs in the Philippines:
prices must display in Philippine peso (₱), and the currency must be
configurable rather than baked in — any ISO 4217 code should work by
changing configuration only, with no code changes.

## Decision (user-approved)

Currency is **backend deployment configuration**, following the existing
tax-rate precedent: it lives in `appsettings.json`, rides to the frontend
on the menu response, and every POS money display formats with it.

## Backend (Modules.Menu)

- `MenuOptions` gains `CurrencyCode` (string), bound from
  `Menu:CurrencyCode` alongside the existing `Menu:TaxRatePercent`.
  `appsettings.json` sets it to `"PHP"`.
- `GetMenuQueryHandler`'s `MenuResult` gains `CurrencyCode`, populated from
  the options — exactly parallel to `TaxRatePercent`. The endpoint already
  serializes the result; no endpoint change beyond the record field.
- The value is an opaque ISO 4217 code to the backend: no validation list,
  no formatting. Formatting is entirely a frontend concern.

## Frontend

- `MenuResponse` gains `currencyCode: string`.
- `MenuService` exposes `currencyCode` as a read-only signal, default
  `'PHP'`. When applying a fetched or cached menu payload, the service uses
  `payload.currencyCode ?? 'PHP'` so a stale offline cache written before
  this feature (no `currencyCode` field) falls back cleanly instead of
  leaking `undefined` into the pipe.
- All nine POS money displays change from `| currency` to
  `| currency: menu.currencyCode() : 'symbol-narrow'`:
  - `product-catalog.html` (1: tile price)
  - `cart-panel.html` (6: line total, subtotal, discount, tax, total, Pay button)
  - `payment-dialog.html` (2: amount due, change due)
- Discovered during implementation, a tenth spot: the Cash-tendered
  `p-inputNumber` in `payment-dialog.html` hardcoded `currency="USD"` — it
  becomes `[currency]="menu.currencyCode()"`.
- `'symbol-narrow'` renders PHP as **₱** (and USD as $, EUR as €, etc.) via
  Angular's CLDR data — this is what makes any configured code display its
  proper symbol without code changes.
- `CartPanel` gains a `protected readonly menu = inject(MenuService)`;
  `PaymentDialog` and `ProductCatalog` already inject it.

## Flexibility contract

Changing `Menu:CurrencyCode` to any ISO 4217 code (USD, EUR, SGD, …) and
restarting the API is the complete procedure for switching currencies.
Known constraint, accepted: the platform's money math is fixed at 2
decimals end-to-end (numeric(12,2), the lockstep rounding rule), so
zero-decimal currencies like JPY will display correctly but still compute
at 2 decimals.

## Out of scope

- Storing a currency on orders (Sales records bare amounts; revisit when
  an order read-side/history exists).
- Per-brand or per-branch currencies.
- Any admin UI for currency.
- Locale-based number formatting (grouping/decimal separators stay en-US).

## Testing

- Backend: `GetMenuQueryHandler` test asserts `CurrencyCode` flows from
  `MenuOptions` to the result.
- Frontend: `menu.service.spec.ts` asserts (a) `currencyCode` from a fetched
  response is exposed, (b) a cached payload without `currencyCode` falls
  back to `'PHP'`.
- One rendered-DOM test (product catalog) asserts a `₱` price appears when
  the menu carries `currencyCode: 'PHP'`.
- Existing money-math tests are unaffected: amounts and rounding do not
  change, only display formatting.

# Identity, Tenancy & Login — Design

## Purpose

RestaurantEmpire (Don Picaso) needs to move from a single unauthenticated
`CreateOrder` endpoint to a real multi-role, multi-tenant system: staff log
into POS tablets to take orders, managers and franchise owners run their
locations, and corporate oversees the whole brand. This is the first of
three planned sub-projects (Identity/Login → Menu → Admin Dashboard) that
together wire the UI up to the API. Franchise/multi-tenancy is treated as
foundational here rather than a later bolt-on, since retrofitting tenant
scoping onto Users/Branches/Menu after the fact would be far more
disruptive than deciding it up front.

## Current State (verified)

- Backend: `RestaurantEmpire.Api` (.NET 10, ASP.NET Core minimal APIs)
  hosts a single module, `Modules.Sales`, following a vertical-slice
  pattern — its own `SalesDbContext`, its own EF Core migrations
  (Postgres schema `sales`), FluentValidation validators, and a
  `AddSalesModule()` / `MapSalesModule()` composition-root pair called
  from `Program.cs`. There is no identity/auth of any kind today — the
  `CreateOrder` endpoint is unauthenticated.
- The existing `Order` entity (`Features/Orders/Order.cs`) already has
  `BrandId` and `BranchId` fields — raw, unvalidated GUIDs with no real
  tenancy module behind them yet. The new Identity module's entities are
  named to match this existing vocabulary exactly (`Brand`, `Branch`)
  rather than introducing a second set of terms (`Franchise`,
  `Restaurant`) for the same concepts — found and corrected during
  plan-writing, confirmed with the user.
- CORS is already configured in `Program.cs` for `http://localhost:4200`
  (Angular dev) plus Capacitor/Ionic origins, anticipating a future
  mobile-packaged POS app.
- Frontend: a real Angular 21 standalone workspace exists (`frontend/`),
  currently just a minimal shell plus `core/offline` (Dexie-backed
  offline order queue). No routing, no auth, no login UI exists yet.
- No customer-facing app exists or is planned in this phase — this is
  and remains, for now, a staff-facing POS system.

## Tenancy & Role Model (decided)

- **Hierarchy**: Corporate (Don Picaso) → Brand (a franchise) → Branch
  (physical restaurant location). A brand can own multiple branches.
  Corporate defines brand/menu standards; brands operate
  semi-independently day to day but roll up to corporate.
- **Roles** (4-tier): `Corporate`, `BrandOwner`, `BranchManager`,
  `Staff`. Each tier manages the tier directly below it within its own
  branch of the hierarchy. `Corporate` is the exception: it can act at
  any level (create/edit brands, branches, managers, or staff
  directly), per explicit decision — not just the top of a strict
  top-down chain.
- Full CRUD/provisioning UI for this hierarchy (creating brands,
  branches, users) is **out of scope for this sub-project** — it's
  the subject of the later Admin Dashboard sub-project. This sub-project
  ships the schema, auth mechanics, and login UI only, using seed data
  to make the login flow independently testable.

## Scope

In scope:
1. New `Modules.Identity` backend module: `Brand`, `Branch`,
   `User`, `RefreshToken` entities and an initial EF Core migration
   (Postgres schema `identity`), seeded with one account per role.
2. JWT-based authentication: email+password login for admin-tier roles,
   PIN-based login for staff/branch-manager on shared POS tablets.
3. Role-hierarchy + tenancy-scope authorization policies, enforced via a
   custom `AuthorizationHandler`.
4. Angular: login screens (`/login`, `/staff-login` + device setup),
   `AuthService`, auth HTTP interceptor, route guards.
5. Automated tests for the new auth command handlers, the authorization
   handler, and the frontend auth service/interceptor/guards.

Out of scope (explicitly deferred):
- Admin dashboard UI for provisioning brands/branches/users
  (next sub-project) — this phase relies on seed data instead.
- Menu domain and POS order-building UI (the sub-project after that).
- Wiring `Order.BrandId`/`Order.BranchId` up to real foreign keys
  against the new `Brand`/`Branch` tables — that's Sales-module work for
  a later phase; this phase only establishes the Identity-side tables.
- PIN brute-force lockout / rate limiting (flagged as a known gap;
  acceptable for an internal staff tool on day one, should be revisited
  before this is customer-facing or handles payment data).
- Password reset / forgot-password flows.
- Any customer-facing login (kiosk/website), until that surface exists.

## Design

### 1. Backend module structure

`Modules.Identity` mirrors `Modules.Sales` exactly:

- `IdentityDbContext` (Postgres schema `identity`), own migrations.
- FluentValidation validators for login/staff-login commands.
- Minimal-API endpoints per feature (`LoginEndpoint`,
  `StaffLoginEndpoint`, `RefreshTokenEndpoint`, `LogoutEndpoint`,
  `StaffListEndpoint` for a branch's staff roster).
- `AddIdentityModule(connectionString, jwtOptions)` /
  `MapIdentityModule()` composition-root pair, called from `Program.cs`
  alongside `AddSalesModule` / `MapSalesModule`.
- `Program.cs` additionally wires `AddAuthentication().AddJwtBearer(...)`
  and `AddAuthorization()` with the custom policies below.

### 2. Data model (schema `identity`)

- **Brand**: `Id` (Guid), `Name`, `CreatedAt`.
- **Branch**: `Id` (Guid), `BrandId` (FK), `Name`, `CreatedAt`.
  Always belongs to exactly one brand.
- **User**: `Id` (Guid), `Email` (nullable — set for admin-tier
  accounts), `PasswordHash` (nullable), `DisplayName`, `PinHash`
  (nullable — set for Staff/BranchManager accounts), `Role` (enum:
  `Corporate`, `BrandOwner`, `BranchManager`, `Staff`),
  `BrandId` (nullable FK), `BranchId` (nullable FK),
  `CreatedAt`.
  - Scoping rule, enforced in command handlers (not DB constraints):
    `Corporate` → no scope. `BrandOwner` → `BrandId` required,
    `BranchId` null. `BranchManager` / `Staff` → both required,
    and `Branch.BrandId` must match `User.BrandId`.
- **RefreshToken**: `Id`, `UserId` (FK), `TokenHash`, `ExpiresAt`,
  `RevokedAt` (nullable).

A migration seeds one Brand, one Branch, and one user per role
(including a known PIN for the seeded Staff account) so the full login
flow is exercisable without any provisioning UI.

### 3. Auth flows

**Admin login** (`Corporate`, `BrandOwner`, `BranchManager`):
`POST /api/v1/auth/login { email, password }`. Validates against
`PasswordHash`. On success, issues a short-lived JWT access token
(~15 min; claims: `sub`, `role`, `brandId`, `branchId`) plus a
refresh token (~7 days, stored hashed in `RefreshToken`).
`POST /api/v1/auth/refresh { refreshToken }` exchanges a valid, unexpired,
unrevoked refresh token for a new access token.

**Staff PIN login**: the POS tablet is configured once with a
`branchId` (a one-time local "set up this device" step, persisted
on-device). The staff login screen calls
`GET /api/v1/auth/staff/{branchId}/users` to list Staff/
BranchManager accounts at that branch (id + display name only, no
credentials). Staff taps their name, enters a 4-digit PIN, and
`POST /api/v1/auth/staff-login { branchId, userId, pin }` validates
against `PinHash` and issues the same token pair, with a longer access
token lifetime (~12 hrs, since it's a shared all-day device).

**Logout**: `POST /api/v1/auth/logout` revokes the refresh token
server-side; Angular clears local session state.

### 4. Authorization

A custom `AuthorizationHandler` enforces:
- **Role hierarchy**: `Corporate` > `BrandOwner` > `BranchManager`
  > `Staff`. Policies express a minimum required role
  (e.g. `RequireBranchManagerOrAbove`).
- **Tenancy scope**: for roles below `Corporate`, the handler compares
  the resource's `brandId`/`branchId` against the caller's JWT
  claims and denies access outside their own branch (org-tree sense).
  `Corporate` bypasses scope checks entirely (acts at any level, per
  the tenancy decision above).

### 5. Frontend

- `frontend/src/app/core/auth/`: `AuthService` (login, staffLogin,
  logout, refresh, current-user state exposed as a signal),
  `authInterceptor` (`HttpInterceptorFn` — attaches
  `Authorization: Bearer <token>`; on a 401, attempts one silent
  refresh and retries once before redirecting to login), and functional
  route guards (`CanActivateFn`) checking the role claim.
- `app.routes.ts` additions: `/login` (email+password),
  `/staff-login` (avatar-tap + PIN, including a first-run device-setup
  step to capture `branchId`), `/admin/**` (guarded:
  BranchManager and above), `/pos/**` (guarded: Staff and above).
- `provideHttpClient(withInterceptors([authInterceptor]))` wired into
  `app.config.ts`.
- Token storage: the access token is held only in memory (an
  `AuthService` field, lost on full page reload — acceptable since the
  interceptor can silently refresh it). The refresh token is persisted
  in `localStorage` so a reload doesn't force a full re-login; this is
  the standard tradeoff for a bearer-token SPA with no cookie-based
  session (see Architecture options, approach A vs C).

## Error Handling / Edge Cases

- Invalid credentials (email/password or PIN) → generic 401, no user
  enumeration (don't reveal whether the email/user exists).
- Expired or revoked refresh token → interceptor gives up after one
  failed refresh attempt and forces full re-login.
- Staff login on a device with no configured `branchId` yet →
  redirected to the device-setup step first.
- A branch with zero staff accounts → staff login screen shows an
  empty state rather than erroring.
- PIN brute-force: explicitly not handled in this pass (see Scope).

## Testing

- Backend (MSTest, following the `Modules.Sales.Tests` pattern): unit
  tests for `LoginCommandHandler`, `StaffLoginCommandHandler`,
  `RefreshTokenCommandHandler`, and the tenancy-scoping
  `AuthorizationHandler` (role hierarchy + scope + Corporate bypass).
- Frontend (Vitest): `AuthService` (token storage, refresh-on-401
  behavior), `authInterceptor`, and route guards (permit/deny per
  role).
- Manual E2E smoke test: log in as each seeded role (admin login and
  staff PIN login), confirm a protected endpoint returns 401 without a
  token and 200 with correctly scoped claims when authenticated.

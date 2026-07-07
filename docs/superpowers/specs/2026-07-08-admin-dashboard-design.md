# Admin Dashboard — Design

## Purpose

The Identity/Login sub-project shipped auth mechanics and login UI but deferred all
provisioning to seed data — there is no way today to create a Brand, Branch, or User
except via the EF Core migration seeder. This is the second of the three planned
sub-projects (Identity/Login → **Admin Dashboard** → Menu). It replaces seed-data-only
provisioning with real CRUD UI for Brands, Branches, and Users, scoped by the existing
role hierarchy and tenancy-scope authorization infrastructure (added at the end of the
Identity phase but not yet consumed by any endpoint).

## Current State (verified)

- `Modules.Identity` has `Brand`, `Branch`, `User`, `RefreshToken` entities and auth
  endpoints (login, staff-login, refresh, logout, staff roster, `/me`), but no
  create/update/list endpoints for `Brand`/`Branch`/`User` — they're only ever
  materialized by `IdentitySeeder`.
- `RoleRequirement`/`RoleAuthorizationHandler` (in `Authorization/`) already implement
  role-hierarchy + tenancy-scope checks (`TenancyScope.None/Brand/Branch`, with
  `Corporate` bypassing scope) but have zero call sites — no endpoint uses a
  `TenancyScope` other than `None` yet.
- `Brand`, `Branch`, `User` have no `IsActive` flag — nothing can be deactivated today.
- Frontend has `/admin` guarded at `roleGuard(Role.BranchManager)` and above, currently
  rendering `features/admin/admin-placeholder.ts`. No admin pages, forms, or services
  exist yet.
- Role hierarchy (unchanged from Identity phase): `Corporate` > `BrandOwner` >
  `BranchManager` > `Staff`. Each tier manages the tier directly below it within its own
  branch of the hierarchy; `Corporate` acts at any level.

## Scope

In scope:
1. Backend CRUD for `Brand`, `Branch`, `User` under `Modules.Identity`, enforcing the
   creation rule "each tier creates the tier(s) below it, within its own scope" and the
   existing tenancy-scope infrastructure.
2. Soft deactivate/reactivate (`IsActive` flag) for all three entities — no hard delete
   exposed anywhere in the UI.
3. Cascading *effective* active-check at login time: a user can log in only if
   `User.IsActive AND Branch.IsActive (if assigned) AND Brand.IsActive (if assigned)`.
   This is a live check, not a write-time cascade — deactivating a Brand does not flip
   any flag on its Branches/Users.
4. Admin-set credentials: creating or resetting a user's password/PIN is typed directly
   into the form by the admin (no generated one-time reveal, no email flow).
5. Full user edit including role and brand/branch reassignment, with the same
   credential invariants `CreateAdmin`/`CreateStaff` already enforce (admin-tier ↔
   password, Staff ↔ PIN) re-applied on a role change, including a forced credential
   reset in the same save when the credential type changes.
6. Angular admin section: nested routes/pages for Brands, Branches, Users, replacing
   `admin-placeholder.ts`, landing each role at the appropriate level (Corporate →
   Brands; BrandOwner → their Brand's Branches; BranchManager → their Branch's Users).
7. Automated tests for the new command handlers, the newly-consumed authorization
   scoping, the effective-active login rule, and the frontend services/forms/guards.

Out of scope (explicitly deferred):
- Hard delete of any entity.
- Generated/emailed credentials, password-reset-via-email, self-service password reset.
- Menu domain and POS order-building UI (the next sub-project after this one).
- Wiring `Order.BrandId`/`Order.BranchId` to real foreign keys against `Brand`/`Branch`
  (Sales-module work, still deferred).
- Audit logging of admin actions (who deactivated whom, etc.) — not built now, worth
  revisiting once this is more than an internal tool.
- Any "last Corporate account" safety net — an admin can deactivate the only Corporate
  user; no guard rail against it in v1.

## Design

### 1. Data model changes

New migration on `Modules.Identity`:
- `Brand.IsActive` (bool, default `true`)
- `Branch.IsActive` (bool, default `true`)
- `User.IsActive` (bool, default `true`)

`User` gains a role/assignment-change path (distinct from the existing `CreateAdmin`/
`CreateStaff` factories, which remain as-is for initial creation and seeding):
a `ChangeRole(...)`-style method enforcing the same invariants — assigning `Staff`
clears `PasswordHash` and requires a new `PinHash`; assigning an admin-tier role clears
`PinHash` and requires a new `PasswordHash`.

### 2. Backend features (new, under `Modules.Identity/Features/`)

- **Brands**: `CreateBrand`, `ListBrands`, `GetBrand`, `UpdateBrand` (name),
  `DeactivateBrand`, `ReactivateBrand`. Corporate-only.
- **Branches**: `CreateBranch`, `ListBranches` (by `brandId`), `GetBranch`,
  `UpdateBranch` (name), `DeactivateBranch`, `ReactivateBranch`. Corporate or the
  owning `BrandOwner` (`TenancyScope.Brand`).
- **Users**: `CreateUser`, `ListUsers` (by `branchId` or `brandId`), `GetUser`,
  `UpdateUser` (name, role, brand/branch reassignment), `ResetCredential` (password or
  PIN, admin-typed), `DeactivateUser`, `ReactivateUser`. Corporate, the owning
  `BrandOwner` (`TenancyScope.Brand`), or the owning `BranchManager`
  (`TenancyScope.Branch`) — each limited to creating/editing roles below their own.

Creation-rule enforcement (command-handler level, mirroring the existing
`CreateAdmin`/`CreateStaff` role-mismatch guards):
- `Corporate` → can create/edit any role, any brand/branch.
- `BrandOwner` → can create/edit `BranchManager`/`Staff` only, within their own brand.
- `BranchManager` → can create/edit `Staff` only, within their own branch.

### 3. Authorization wiring

Each new endpoint gets a `RoleRequirement` policy pairing a minimum role with the
appropriate `TenancyScope`:
- Brand-level endpoints: `TenancyScope.Brand`, route parameter `brandId`.
- Branch-level and user-level endpoints: `TenancyScope.Branch`, route parameter
  `branchId`.
- `Corporate` bypasses scope checks entirely (already implemented).

This is the first real consumer of `RoleRequirement`/`RoleAuthorizationHandler` beyond
`TenancyScope.None`.

### 4. Login-time effective-active check

`LoginCommandHandler` and `StaffLoginCommandHandler` extend their existing lookup to
also load the user's `Branch`/`Brand` (when assigned) and require all three `IsActive`
flags to be true, folding this into the same generic-401 timing-safe path already used
for "user not found" / "wrong credential" (no enumeration signal for "this account
exists but is deactivated"). `StaffRoster`/staff-login's branch list filters out
branches or brands that are inactive.

### 5. Frontend

- `features/admin/` restructured with nested routes replacing `admin-placeholder.ts`:
  - `/admin` → `AdminShell` (nav shell; visible sections vary by role)
  - `/admin/brands`, `/admin/brands/:brandId/branches`, `/admin/branches/:branchId/users`
  - Corporate lands on Brands; BrandOwner lands directly on their Brand's Branches;
    BranchManager lands directly on their Branch's Users.
- List pages (table: name, status, row actions) and create/edit forms per entity:
  `Brands`/`BrandForm`, `Branches`/`BranchForm`, `Users`/`UserForm`.
- `UserForm` shows/hides Brand/Branch fields based on selected role, and exposes a
  separate "Reset Password"/"Reset PIN" action (independent of the main save) matching
  whichever credential type applies to the currently-selected role.
- Deactivate/reactivate: row-level action with a confirm step.
- New `core/admin/` services (`BrandsService`, `BranchesService`, `UsersService`)
  wrapping the new endpoints, following the existing `AuthService` pattern.

## Error Handling / Edge Cases

- Deactivated user (or assigned to an inactive Branch/Brand) attempting login/staff-login
  → identical generic 401 to bad credentials.
- Role change that swaps credential type forces a credential reset in the same save —
  a user can never end up with neither a `PasswordHash` nor a `PinHash`.
- Deactivating a Brand does not touch its Branches'/Users' own `IsActive` flags — the
  login-time check walks up to Brand/Branch live, so reactivating a Brand alone restores
  login for everything under it without any bulk write.
- Deactivating the only `Corporate` account is allowed; no safeguard in v1.
- A `BranchManager`/`BrandOwner` attempting to create/edit a role above what they're
  permitted to manage → 403 (existing role-hierarchy check already returns this shape).

## Testing

- Backend (MSTest): command handler tests per new Brand/Branch/User operation,
  including role-change invariant enforcement and credential-reset paths; authorization
  handler tests extended to cover the newly-consumed `TenancyScope.Brand`/`Branch`
  policies on real endpoints; `LoginCommandHandler`/`StaffLoginCommandHandler` tests for
  the effective-active rule (inactive user / inactive branch / inactive brand,
  independently).
- Frontend (Vitest): `BrandsService`/`BranchesService`/`UsersService` request/response
  shapes; `UserForm`'s role-driven field adaptation; route guard coverage for the new
  nested `/admin/*` routes per role.
- Manual E2E smoke test: as Corporate, create a Brand → Branch → BranchManager → Staff
  chain end to end, log in as each; deactivate a Brand and confirm the Staff account
  under it can no longer log in; reactivate and confirm login succeeds again.

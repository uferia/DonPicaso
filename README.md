# DonPicaso (RestaurantEmpire)

A multi-brand, multi-branch restaurant point-of-sale platform. A .NET backend serves a role-based Angular admin/POS frontend, with offline-first order capture designed for tablets running the app via Capacitor.

## Tenancy model

- **Corporate** — owns one or more **Brands**.
- **Brand** — owns one or more **Branches** (physical restaurant locations).
- **Branch** — has **Staff** who log in with a 4-digit PIN on a branch-scoped POS tablet.

Roles: `Corporate`, `BrandOwner`, `BranchManager`, `Staff`. Admin-tier roles (`Corporate`/`BrandOwner`/`BranchManager`) authenticate with email + password; `Staff` authenticate with a PIN against a roster scoped to their branch. Deactivating a Brand or Branch doesn't cascade — it's enforced live as an "effective-active" check at login/roster time. Nothing is hard-deleted; every removal is a reversible `IsActive = false`.

## Solution structure

```
RestaurantEmpire.sln
src/
  RestaurantEmpire.Api/        ASP.NET Core minimal API host (composition root)
  Modules/
    Modules.Identity/          Auth, Brands, Branches, Users, admin CRUD
    Modules.Sales/             Orders
tests/
  Modules.Identity.Tests/
  Modules.Sales.Tests/
frontend/                      Angular 21 app (admin + POS)
docs/superpowers/               Design specs and implementation plans
```

Each module is a vertical slice: one folder per Command/Query, with its own Handler, Validator (when needed), and Endpoint, plus its own EF Core `DbContext` and migrations. The API host wires modules together via `AddXModule()` / `MapXModule()` extension methods in `Program.cs` — there's no shared "core" project.

## Tech stack

- **Backend:** .NET 10, ASP.NET Core minimal APIs, EF Core + Npgsql, FluentValidation, JWT auth
- **Database:** PostgreSQL 16
- **Frontend:** Angular 21 (standalone components, signals, template-driven forms), SCSS, Vitest
- **Offline sync:** Dexie (IndexedDB) for order capture when the POS tablet is offline
- **Testing:** MSTest + FluentAssertions + Moq, EF Core `UseInMemoryDatabase` for handler tests

## Running locally

### 1. Database

```bash
docker compose up -d
```

Starts Postgres 16 at `localhost:5432`, database `restaurant_empire`, user/password `postgres`/`postgres` (matches `src/RestaurantEmpire.Api/appsettings.json` — don't change one without the other).

### 2. Backend API

```bash
dotnet run --project src/RestaurantEmpire.Api
```

Runs at `http://localhost:5098` (HTTPS profile also available at `https://localhost:7045`). In `Development`, `Program.cs` auto-seeds identity data on startup and exposes an OpenAPI document.

EF Core migrations (per module, since each has its own `DbContext`):

```bash
dotnet ef migrations add <Name> --project src/Modules/Modules.Identity --startup-project src/RestaurantEmpire.Api
dotnet ef migrations add <Name> --project src/Modules/Modules.Sales --startup-project src/RestaurantEmpire.Api
```

`dotnet-ef` is pinned via `dotnet-tools.json` — run `dotnet tool restore` first if it's not already installed.

### 3. Frontend

```bash
cd frontend
npm install
npm start
```

Serves at `http://localhost:4200` with a dev proxy (`proxy.conf.json`) forwarding API calls to the backend.

## Testing

```bash
dotnet test                 # all backend tests
cd frontend && npm test     # frontend unit tests (Vitest)
```

## Frontend structure

- `core/auth/` — login, JWT/session handling, role guard
- `core/offline/` — Dexie-backed offline order queue and sync service (POS)
- `features/auth/` — login, staff PIN login, device setup
- `features/admin/` — nested `/admin` route tree: Brands → Branches → Users, each with list/form pages
- `features/pos/` — POS ordering UI (placeholder — not yet built out)

Route guards gate `/admin` behind `BranchManager`+ and `/pos` behind `Staff`.

## Conventions

- Commits are authored solely by the repo owner — no `Co-Authored-By` trailers (see `CLAUDE.md`).
- Design specs and implementation plans for each unit of work live under `docs/superpowers/specs/` and `docs/superpowers/plans/`.

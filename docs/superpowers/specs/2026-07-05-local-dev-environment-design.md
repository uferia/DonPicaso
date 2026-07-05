# Local Dev Environment Bring-Up — Design

## Purpose

Get the existing RestaurantEmpire (Don Picaso) codebase actually running locally:
a Postgres database via Docker, the .NET Sales module backend against it, and a
real Angular workspace scaffolded around the two hand-written offline-sync
files that currently exist with no project around them. This is infrastructure
wiring for code that already exists — no new product features.

## Current State (verified)

- **Backend**: `RestaurantEmpire.Api` (.NET 10, ASP.NET Core minimal APIs) references
  `Modules.Sales`, a vertical-slice module with a `CreateOrder` feature already
  implemented and tested (`tests/Modules.Sales.Tests`). EF Core migration
  `InitialSalesSchema` already exists, targeting a Postgres `sales` schema
  (`sales.orders`, `sales.order_items`).
- **Connection string** (`src/RestaurantEmpire.Api/appsettings.json`):
  `Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres`
- **API dev port**: `http://localhost:5098` (`https://localhost:7045` also
  configured), per `Properties/launchSettings.json`.
- **CORS**: already configured in `Program.cs` to allow `http://localhost:4200`
  (Angular's default dev port) plus Capacitor/Ionic origins for the packaged
  mobile shell.
- **Route**: `POST /api/v1/orders`, per `CreateOrderEndpoint.cs`.
- **Frontend**: `frontend/src/app/core/offline/` contains `offline-order-db.ts`
  (Dexie/IndexedDB wrapper) and `order-sync.service.ts` (Angular injectable
  service, offline-first order queue/replay). Both reference Angular and Dexie
  APIs but neither `package.json` nor `angular.json` exist yet — there is no
  actual Angular workspace, and `dexie` is not an installed dependency.
- **Tooling available**: Docker Desktop (running), Node v22.14.0/npm 11.12.1,
  global `@angular/cli@21.1.0`, .NET SDK 10.0.301, `dotnet-ef` pinned via
  `dotnet-tools.json` (not yet restored locally).
- **No conflicting Docker state**: no existing container/volume for this
  project; an unrelated `app-gruuber` project has its own stopped Postgres
  container under a different name.

## Scope

In scope:
1. Dockerized Postgres, wired to the existing connection string.
2. Running the existing EF Core migration against it.
3. A minimal but real Angular 21 standalone workspace scaffolded into
   `frontend/`, incorporating the existing `core/offline` files unchanged.
4. A dev-time proxy so the frontend's relative `/api/v1/orders` calls reach
   the backend.
5. End-to-end verification that all three pieces (Postgres, API, Angular dev
   server) run together and a request round-trips.

Out of scope (explicitly deferred, not part of this change):
- Any actual ordering/POS UI (cart, item list, checkout screens).
- Capacitor mobile shell packaging/config.
- Production deployment, CI, Dockerizing the .NET API or Angular app itself.
- Auth/identity.

## Design

### 1. Postgres via Docker Compose

A `docker-compose.yml` at the repo root defines a single `postgres` service:

```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: restaurant_empire
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - donpicaso_postgres_data:/var/lib/postgresql/data

volumes:
  donpicaso_postgres_data:
```

Credentials/db name match `appsettings.json` exactly — no backend config
changes needed. `docker compose up -d` brings it up; data persists across
container restarts via the named volume.

After the container is healthy:
- `dotnet tool restore` — installs `dotnet-ef` per `dotnet-tools.json`.
- `dotnet ef database update --project src/Modules/Modules.Sales --startup-project src/RestaurantEmpire.Api`
  — applies `InitialSalesSchema`, creating the `sales` schema and its tables.

### 2. Angular workspace scaffold

`frontend/` already contains hand-authored files that assume an Angular
project exists around them. Rather than hand-rolling `package.json`/
`angular.json`, scaffold a real Angular 21 standalone workspace and merge the
existing files in:

- Generate a fresh workspace (routing enabled, SCSS styling, Vitest as the
  unit test runner, standalone components — no NgModules) in a scratch
  directory, since `ng new` requires an empty target and
  `frontend/src/app/core/offline/` already has content. (Correction from an
  earlier draft: Angular CLI 21.1.0's `--test-runner` only supports `karma`
  or `vitest` natively — there is no built-in Jest option. Vitest is the
  fast/unbundled runner originally intended here.)
- Merge the generated `package.json`, `angular.json`, `tsconfig*.json`,
  `src/main.ts`, `src/index.html`, `src/styles.scss`, and the generated
  `src/app/app.*` root component into `frontend/`, without overwriting
  `src/app/core/offline/offline-order-db.ts` or `order-sync.service.ts`.
- Add `dexie` to `package.json` dependencies (used by `offline-order-db.ts`
  but not yet declared anywhere).
- Wire `provideHttpClient()` into the app config so `OrderSyncService` (which
  injects `HttpClient`) resolves correctly.
- The root component stays a minimal shell: enough to boot and prove
  `OrderSyncService`/`offlineOrderDb` construct without errors. No ordering UI.

### 3. Dev-time API proxy

`OrderSyncService` calls a relative `/api/v1/orders` — it has no origin baked
in. Add `frontend/proxy.conf.json`:

```json
{
  "/api": {
    "target": "http://localhost:5098",
    "secure": false
  }
}
```

Wire it into the Angular CLI's serve options (`angular.json` →
`serve.options.proxyConfig`, or equivalently an npm `start` script using
`ng serve --proxy-config proxy.conf.json`) so `npm start` proxies `/api/*` to
the backend automatically. Local dev then doesn't depend on the CORS policy
in `Program.cs` at all — that policy remains relevant for the Capacitor-
packaged app later, which is out of scope here.

### 4. Verification

Manual smoke test, in order:
1. `docker compose up -d` → `docker compose ps` shows `postgres` healthy.
2. `dotnet tool restore && dotnet ef database update ...` → succeeds, no
   pending migrations.
3. `dotnet run --project src/RestaurantEmpire.Api` → API listening on
   `http://localhost:5098`; `/openapi` reachable in Development.
4. `npm start` (in `frontend/`) → Angular dev server on
   `http://localhost:4200`.
5. A POST through the proxy (`http://localhost:4200/api/v1/orders` with a
   valid `CreateOrderCommand` body, e.g. via curl or the browser devtools)
   returns `201 Created` and a row appears in `sales.orders`.

## Error Handling / Edge Cases

- If port `5432` is already bound locally (e.g. a native Postgres install),
  `docker compose up` will fail loudly with a clear port-conflict error —
  no special handling needed, the fix is user-driven (stop the conflicting
  service or remap the port).
- If `dotnet ef database update` is run before the container finishes
  initializing, it will fail with a connection error; re-running after a
  few seconds resolves it. Not automated/retried — this is a one-time local
  setup step, not a repeated CI operation.

## Addendum (found during plan-writing)

The repo has no `.gitignore` anywhere, and `bin/`/`obj/` build artifacts are
currently tracked in git — a plain `dotnet build` dirties tracked binary
files. This must be fixed before scaffolding Angular, since `node_modules/`
would otherwise be committed. Added as a first task: a root `.gitignore`
covering .NET/Node/OS artifacts, plus `git rm -r --cached . && git add .` to
untrack the already-committed build output (no working-tree files deleted).

## Testing

No new automated tests are introduced by this change — it is environment
wiring, not application logic. Existing tests
(`tests/Modules.Sales.Tests`) are unaffected. The Vitest test runner configured
in the Angular scaffold will run the CLI's own generated default spec
(root component) as a sanity check that the test toolchain works; no
tests are written for `core/offline` in this pass since that code predates
and is out of scope for this change.

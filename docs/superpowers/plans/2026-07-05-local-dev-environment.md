# Local Dev Environment Bring-Up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Get the existing RestaurantEmpire (Don Picaso) backend, a Dockerized Postgres, and a real Angular workspace all running together locally, with a proven end-to-end request path.

**Architecture:** No new application code. Wire up infrastructure around code that already exists: run Postgres in Docker Compose using the connection string already in `appsettings.json`, apply the existing EF Core migration, scaffold a real Angular 21 workspace around the two hand-written offline-sync files already in `frontend/src/app/core/offline/`, and add a dev-time proxy so the frontend's relative API calls reach the backend.

**Tech Stack:** .NET 10 / ASP.NET Core minimal APIs, EF Core + Npgsql, PostgreSQL 16 (Docker), Angular 21 (standalone, Vitest, SCSS), Docker Compose.

## Global Constraints

- Postgres container credentials/db name must exactly match the existing connection string in `src/RestaurantEmpire.Api/appsettings.json`: `Host=localhost;Port=5432;Database=restaurant_empire;Username=postgres;Password=postgres`. Do not change `appsettings.json`.
- Angular workspace: standalone components (no NgModules), SCSS styling, Vitest as the unit test runner (Angular CLI 21.1.0's `--test-runner` only supports `karma` or `vitest` — there is no built-in Jest option).
- Do not modify or move `frontend/src/app/core/offline/offline-order-db.ts` or `order-sync.service.ts` — the scaffold must be built around them, not replace them.
- No new product/ordering UI in this plan — root component stays a minimal generated shell. Out of scope: Capacitor packaging, auth, CI, Dockerizing the .NET API or Angular app itself.
- API route is `POST /api/v1/orders` on `http://localhost:5098` (dev), already implemented — do not change it.

---

### Task 1: Add root `.gitignore` and untrack existing build artifacts

The repo currently has no `.gitignore` anywhere, and `bin/`/`obj/` build output is accidentally tracked in git (confirmed: a plain `dotnet build` dirties tracked binary files). This must be fixed before scaffolding Angular, otherwise `node_modules/` (hundreds of MB) would get committed in Task 4.

**Files:**
- Create: `.gitignore`
- Modify (untrack only, no content changes): all currently-tracked `bin/`, `obj/`, and `TestResults/` paths under `src/` and `tests/`

**Interfaces:**
- Produces: a repo where `git status` after any `dotnet build`, `npm install`, or `ng build` stays clean of build-artifact noise. All later tasks depend on this before adding `frontend/node_modules`.

- [ ] **Step 1: Verify the current problem**

Run: `git status --porcelain`
Expected: clean (no output), confirming the working tree starts clean before this task's changes.

Run: `dotnet build src/RestaurantEmpire.Api/RestaurantEmpire.Api.csproj` then `git status --porcelain | head -5`
Expected: several `M` lines under `src/RestaurantEmpire.Api/bin/...` and `src/RestaurantEmpire.Api/obj/...` — this demonstrates the bug this task fixes.

- [ ] **Step 2: Create the root `.gitignore`**

```
# .NET
bin/
obj/
*.user
.vs/

# Test results
[Tt]est[Rr]esult*/
*.trx

# Node / Angular
node_modules/
dist/
.angular/
npm-debug.log*

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 3: Untrack ignored files without deleting them from disk**

Run: `git rm -r --cached . > /dev/null` then `git add .`
Expected: no error. This unstages every tracked file and restages only what `.gitignore` now allows, so previously-tracked `bin/`/`obj`/`TestResults` paths become untracked while the working-tree files themselves stay on disk.

- [ ] **Step 4: Verify the fix**

Run: `git status --porcelain | grep -E "bin/|obj/|TestResults/"`
Expected: no output (nothing under `bin/`, `obj/`, or `TestResults/` is staged as tracked anymore).

Run: `git status --porcelain | grep "^A "  | wc -l` and `git status --porcelain | grep "^D "  | wc -l`
Expected: the `D` (deleted-from-index) count roughly matches the number of build-artifact files found in Step 1's exploration; the `A` count reflects only `.gitignore` plus any legitimately re-added source files (should just be `.gitignore`).

Run: `dotnet build src/RestaurantEmpire.Api/RestaurantEmpire.Api.csproj` then `git status --porcelain`
Expected: clean — rebuilding no longer dirties git status.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add root .gitignore and untrack build artifacts"
```

---

### Task 2: Run Postgres via Docker Compose

**Files:**
- Create: `docker-compose.yml`

**Interfaces:**
- Produces: a Postgres 16 instance reachable at `localhost:5432`, database `restaurant_empire`, user/password `postgres`/`postgres` — matching `src/RestaurantEmpire.Api/appsettings.json` exactly. Task 3 depends on this being up and healthy.

- [ ] **Step 1: Verify no port conflict up front**

Run: `docker ps -a --filter "publish=5432"`
Expected: no container currently publishing port 5432 (confirmed clean in prior investigation — an unrelated `app-gruuber` project's Postgres container exists but is stopped and uses a different name/network).

- [ ] **Step 2: Create `docker-compose.yml`**

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
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  donpicaso_postgres_data:
```

- [ ] **Step 3: Start it**

Run: `docker compose up -d`
Expected: output shows the `postgres` container being created and started, with no errors (exact container name depends on the Compose project name derived from the directory, e.g. `donpicaso-postgres-1`).

- [ ] **Step 4: Verify it's healthy**

Run: `docker compose ps`
Expected: the `postgres` service shows `STATUS` containing `healthy` (may take a few seconds after start — re-run if it still says `starting`).

Run: `docker compose exec postgres psql -U postgres -d restaurant_empire -c "SELECT 1;"`
Expected: output shows a single row with `1`, confirming the `restaurant_empire` database exists and accepts connections with the configured credentials.

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml
git commit -m "Add docker-compose.yml for local Postgres"
```

---

### Task 3: Apply the existing EF Core migration

No new migration is written here — `InitialSalesSchema` already exists in `src/Modules/Modules.Sales/Persistence/Migrations/`. This task only applies it to the Postgres instance from Task 2.

**Files:** none created or modified — this task changes database state, not repo files.

**Interfaces:**
- Consumes: the Postgres instance from Task 2 (`localhost:5432`, db `restaurant_empire`).
- Produces: the `sales` schema with `orders` and `order_items` tables, matching `Order.cs`/`OrderItem.cs`/`OrderEntityConfiguration.cs`. Task 6's end-to-end POST depends on this schema existing.

- [ ] **Step 1: Restore the pinned `dotnet-ef` tool**

Run: `dotnet tool restore`
Expected: `Tool 'dotnet-ef' (version '10.0.9') was restored... Restore was successful.`

- [ ] **Step 2: Verify the "before" state — no schema yet**

Run: `docker compose exec postgres psql -U postgres -d restaurant_empire -c "\dn"`
Expected: only the default `public` schema listed — no `sales` schema yet.

- [ ] **Step 3: Apply the migration**

Run: `dotnet ef database update --project src/Modules/Modules.Sales --startup-project src/RestaurantEmpire.Api`
Expected: build succeeds, output ends with `Done.` and no errors. (Confirmed working command shape — `dotnet ef dbcontext info` with the same `--project`/`--startup-project` flags already resolved `SalesDbContext` correctly against `Npgsql.EntityFrameworkCore.PostgreSQL`, `restaurant_empire`, `tcp://localhost:5432` during design investigation.)

- [ ] **Step 4: Verify the "after" state**

Run: `dotnet ef migrations list --project src/Modules/Modules.Sales --startup-project src/RestaurantEmpire.Api`
Expected: exactly one migration, `20260705141546_InitialSalesSchema`, with no `(pending)` marker.

Run: `docker compose exec postgres psql -U postgres -d restaurant_empire -c "\dt sales.*"`
Expected: two rows — `sales.orders` and `sales.order_items`.

- [ ] **Step 5: No commit**

This task changes only database state (already-committed migration files are unchanged), so there is nothing new to commit. Confirm with `git status --porcelain` → expected: clean.

---

### Task 4: Scaffold the Angular workspace around the existing offline-sync files

`frontend/src/app/core/offline/offline-order-db.ts` and `order-sync.service.ts` already exist but have no Angular workspace around them (no `package.json`/`angular.json`). Verified during design investigation: running `ng new` with `--directory=.` inside a directory that already contains `src/app/core/offline/*.ts` generates the workspace cleanly around those files without touching or overwriting them.

**Files:**
- Create (via `ng new`): `frontend/angular.json`, `frontend/package.json`, `frontend/tsconfig.json`, `frontend/tsconfig.app.json`, `frontend/tsconfig.spec.json`, `frontend/.gitignore`, `frontend/.editorconfig`, `frontend/README.md`, `frontend/src/main.ts`, `frontend/src/index.html`, `frontend/src/styles.scss`, `frontend/src/app/app.ts`, `frontend/src/app/app.html`, `frontend/src/app/app.scss`, `frontend/src/app/app.spec.ts`, `frontend/src/app/app.config.ts`, `frontend/src/app/app.routes.ts`, `frontend/public/favicon.ico`, `frontend/.vscode/*`
- Modify: `frontend/package.json` (add `dexie` dependency), `frontend/src/app/app.config.ts` (add `provideHttpClient()`)
- Untouched (must still exist afterward, byte-for-byte): `frontend/src/app/core/offline/offline-order-db.ts`, `frontend/src/app/core/offline/order-sync.service.ts`

**Interfaces:**
- Consumes: `frontend/src/app/core/offline/order-sync.service.ts`, which injects Angular's `HttpClient` — the scaffold must provide it.
- Produces: a `frontend/` directory where `npm run build`, `npm test`, and `npm start` all work. Task 5 depends on `frontend/angular.json` existing to add the proxy config.

- [ ] **Step 1: Verify the "before" state**

Run: `ls frontend` and `ls frontend/src/app/core/offline`
Expected: only `src/` exists at the top level; `core/offline` contains exactly `offline-order-db.ts` and `order-sync.service.ts`; no `package.json`/`angular.json` anywhere in `frontend/`.

- [ ] **Step 2: Scaffold the workspace**

Run (from inside `frontend/`):
```bash
cd frontend
ng new frontend --directory=. --style=scss --routing --test-runner=vitest --skip-git --defaults --package-manager=npm
```
Expected: a list of `CREATE`/`UPDATE` lines for `angular.json`, `package.json`, `src/main.ts`, `src/app/app.ts`, etc., followed by npm installing dependencies, ending without error. (`--skip-git` is required because `frontend/` lives inside the parent repo's existing `.git` — we don't want a nested repo initialized.)

- [ ] **Step 3: Verify existing files were preserved and new files are present**

Run: `cat frontend/src/app/core/offline/offline-order-db.ts | head -5`
Expected: unchanged — starts with `import Dexie, { Table } from 'dexie';`.

Run: `ls frontend/angular.json frontend/package.json frontend/src/app/app.ts`
Expected: all three exist.

- [ ] **Step 4: Add the `dexie` dependency**

Run (from inside `frontend/`): `npm install dexie --save`
Expected: `package.json` `dependencies` now includes a `"dexie": "^x.y.z"` entry; `npm install` exits 0.

- [ ] **Step 5: Wire `HttpClient` for `OrderSyncService`**

Edit `frontend/src/app/app.config.ts`:

```ts
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient()
  ]
};
```

- [ ] **Step 6: Verify the build type-checks (including the offline-sync files)**

Run (from inside `frontend/`): `npm run build`
Expected: exits 0. This is the step that proves `order-sync.service.ts` and `offline-order-db.ts` compile cleanly against the real Angular/TypeScript/Dexie versions now installed — they were previously unverified hand-written files with no workspace to compile them.

- [ ] **Step 7: Verify the generated test suite runs**

Run (from inside `frontend/`): `npm test -- --run`
Expected: the generated `src/app/app.spec.ts` suite passes — `2 passed` (the two tests: `should create the app`, `should render title`).

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "Scaffold Angular 21 workspace around existing offline-sync files"
```

---

### Task 5: Add dev-time API proxy

`OrderSyncService` calls a relative `/api/v1/orders` with no origin baked in. Wire the Angular dev server to proxy `/api/*` to the backend so this resolves correctly under `ng serve`.

**Files:**
- Create: `frontend/proxy.conf.json`
- Modify: `frontend/angular.json` (`projects.frontend.architect.serve.options`)

**Interfaces:**
- Consumes: the API's dev URL, `http://localhost:5098` (from `src/RestaurantEmpire.Api/Properties/launchSettings.json`, unchanged by this plan).
- Produces: `npm start` (== `ng serve`) proxying `/api/*` to the backend. Task 6's end-to-end curl through `http://localhost:4200/api/v1/orders` depends on this.

- [ ] **Step 1: Create the proxy config**

Create `frontend/proxy.conf.json`:

```json
{
  "/api": {
    "target": "http://localhost:5098",
    "secure": false
  }
}
```

- [ ] **Step 2: Wire it into the `serve` target**

Edit `frontend/angular.json` — in `projects.frontend.architect.serve`, add an `options` key alongside the existing `configurations`:

```json
        "serve": {
          "builder": "@angular/build:dev-server",
          "options": {
            "proxyConfig": "proxy.conf.json"
          },
          "configurations": {
            "production": {
              "buildTarget": "frontend:build:production"
            },
            "development": {
              "buildTarget": "frontend:build:development"
            }
          },
          "defaultConfiguration": "development"
        },
```

- [ ] **Step 3: Verify `angular.json` is still valid and the build is unaffected**

Run (from inside `frontend/`): `node -e "JSON.parse(require('fs').readFileSync('angular.json'))"`
Expected: no output, exit 0 (valid JSON).

Run (from inside `frontend/`): `npm run build`
Expected: exits 0 — `proxyConfig` only affects `serve`, so the production build is unaffected.

- [ ] **Step 4: Commit**

```bash
git add frontend/proxy.conf.json frontend/angular.json
git commit -m "Add dev-server proxy from /api to the .NET backend"
```

---

### Task 6: End-to-end smoke test

Prove all the pieces run together: Postgres (Task 2/3), the API (existing code, now with schema applied), and the Angular dev server (Task 4/5) with its proxy.

**Files:** none — this task only runs and verifies, it changes no repo files.

**Interfaces:**
- Consumes: everything produced by Tasks 2–5.
- Produces: a verified, working local dev loop — the deliverable of this whole plan.

- [ ] **Step 1: Confirm Postgres is up**

Run: `docker compose ps`
Expected: `postgres` service `STATUS` shows `healthy`. If not running, run `docker compose up -d` first.

- [ ] **Step 2: Start the API**

Run in a background/separate process: `dotnet run --project src/RestaurantEmpire.Api`
Expected (from its log output): `Now listening on: http://localhost:5098`.

- [ ] **Step 3: Start the Angular dev server**

Run in a background/separate process (from inside `frontend/`): `npm start`
Expected (from its log output): `Local:   http://localhost:4200/`.

- [ ] **Step 4: POST an order through the proxy**

Run:
```bash
curl -i -X POST http://localhost:4200/api/v1/orders \
  -H "Content-Type: application/json" \
  -d '{
    "clientOrderId": "11111111-1111-1111-1111-111111111111",
    "branchId": "22222222-2222-2222-2222-222222222222",
    "brandId": "33333333-3333-3333-3333-333333333333",
    "totalAmount": 12.50,
    "items": [
      { "productId": "44444444-4444-4444-4444-444444444444", "productName": "Taco", "quantity": 2, "unitPrice": 6.25 }
    ]
  }'
```
Expected: `HTTP/1.1 201 Created`, header `Location: /api/v1/orders/<some-guid>`, JSON body `{"orderId":"<some-guid>"}`. (`totalAmount` of `12.50` matches `2 * 6.25`, satisfying `CreateOrderCommandValidator`'s cross-field rule; the request reaching the API at all — rather than a connection-refused from the Angular dev server — proves the Task 5 proxy works.)

- [ ] **Step 5: Verify the row landed in Postgres**

Run: `docker compose exec postgres psql -U postgres -d restaurant_empire -c "SELECT client_order_id, branch_id, total_amount FROM sales.orders WHERE client_order_id = '11111111-1111-1111-1111-111111111111';"`
Expected: one row, `total_amount` = `12.50`.

- [ ] **Step 6: Replay idempotency check (optional but cheap — validates the sync-service's core assumption)**

Run the exact same `curl` command from Step 4 again.
Expected: `HTTP/1.1 200 OK` this time (not `201`), same `orderId` in the body — confirming the backend's replay-detection (`WasAlreadyProcessed`) that `OrderSyncService` depends on for safe offline-queue retries actually works end to end.

- [ ] **Step 7: Stop background processes**

Stop the `dotnet run` and `npm start` processes started in Steps 2–3 (e.g. `Ctrl+C` in their terminals, or kill the background job).

- [ ] **Step 8: No commit**

This task only exercises the running system; no repo files changed. Confirm with `git status --porcelain` → expected: clean.

---

## Plan Summary

| Task | Deliverable |
|------|-------------|
| 1 | Root `.gitignore`; build artifacts untracked |
| 2 | Postgres running in Docker, matching existing connection string |
| 3 | `sales.orders`/`sales.order_items` schema applied |
| 4 | Real Angular 21 workspace around the existing offline-sync files |
| 5 | Dev-server proxy from `/api` to the .NET backend |
| 6 | Proven end-to-end: POST → API → Postgres, through the Angular proxy |

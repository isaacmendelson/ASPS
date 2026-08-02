# Angular Admin Client -- Architecture Design

**Task:** ASPS-644
**Status:** Proposed
**Date:** 2026-08-02
**Author:** Architect Agent
**Spec Reference:** [ANGULAR_ADMIN_CLIENT_SPEC.md](ANGULAR_ADMIN_CLIENT_SPEC.md)

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Angular Signals State Management](#2-angular-signals-state-management)
3. [Authentication Flow](#3-authentication-flow)
4. [Angular Material Setup](#4-angular-material-setup)
5. [Shared Components Architecture](#5-shared-components-architecture)
6. [API Communication Layer](#6-api-communication-layer)
7. [Shared TypeScript Models](#7-shared-typescript-models)
8. [Docker / Deployment](#8-docker--deployment)
9. [Backend Integration Points](#9-backend-integration-points)
10. [Testing Strategy](#10-testing-strategy)
11. [Architecture Decision Records](#11-architecture-decision-records)
12. [Trade-offs and Risks](#12-trade-offs-and-risks)
13. [Ownership](#13-ownership)

---

## 1. Project Structure

### 1.1 Repository Location

The Angular admin lives at `apps/admin/angular/` inside the existing monorepo. This mirrors the established pattern (`apps/desktop/win/`, `apps/extension/chrome/`).

### 1.2 Folder Layout

```
apps/admin/angular/
├── angular.json
├── package.json
├── package-lock.json
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.spec.json
├── Dockerfile
├── nginx.conf
├── .eslintrc.json
├── src/
│   ├── main.ts                          # Bootstrap (standalone or module)
│   ├── index.html
│   ├── styles.scss                      # Global styles + Material theme import
│   ├── environments/
│   │   ├── environment.ts               # Dev defaults
│   │   └── environment.prod.ts          # Prod defaults (overridden at runtime)
│   └── app/
│       ├── app.component.ts
│       ├── app.component.html
│       ├── app.routes.ts                # Top-level route definitions
│       ├── app.config.ts                # Application providers (standalone bootstrap)
│       │
│       ├── core/                        # Singleton services, guards, interceptors
│       │   ├── auth/
│       │   │   ├── auth.guard.ts        # CanActivate — redirects to Keycloak if unauthenticated
│       │   │   ├── auth.service.ts      # Wraps keycloak-angular, exposes token/role signals
│       │   │   └── auth.interceptor.ts  # Functional HTTP interceptor — attaches Bearer token
│       │   ├── interceptors/
│       │   │   └── error.interceptor.ts # Functional HTTP interceptor — 401/403/5xx handling
│       │   ├── services/
│       │   │   ├── api.service.ts       # Base HTTP service with generic CRUD methods
│       │   │   ├── signalr.service.ts   # SignalR connection lifecycle + JWT auth
│       │   │   ├── notification.service.ts  # Toast/snackbar notifications
│       │   │   └── runtime-config.service.ts # Loads runtime JSON config (API URL, Keycloak URL)
│       │   └── models/                  # Shared TypeScript interfaces
│       │       ├── index.ts             # Barrel export
│       │       ├── paging.model.ts
│       │       ├── user.model.ts
│       │       ├── device.model.ts
│       │       ├── alert.model.ts
│       │       ├── analysis.model.ts
│       │       ├── blacklist.model.ts
│       │       ├── simulation.model.ts
│       │       ├── roadmap.model.ts
│       │       ├── system.model.ts
│       │       └── enums.ts
│       │
│       ├── shared/                      # Reusable UI components, pipes, directives
│       │   ├── components/
│       │   │   ├── paged-table/
│       │   │   │   ├── paged-table.component.ts
│       │   │   │   ├── paged-table.component.html
│       │   │   │   └── paged-table.component.scss
│       │   │   ├── kpi-card/
│       │   │   │   ├── kpi-card.component.ts
│       │   │   │   ├── kpi-card.component.html
│       │   │   │   └── kpi-card.component.scss
│       │   │   ├── confirm-dialog/
│       │   │   │   ├── confirm-dialog.component.ts
│       │   │   │   └── confirm-dialog.component.html
│       │   │   └── empty-state/
│       │   │       ├── empty-state.component.ts
│       │   │       └── empty-state.component.html
│       │   ├── pipes/
│       │   │   ├── severity-badge.pipe.ts
│       │   │   ├── relative-time.pipe.ts
│       │   │   └── key-display.pipe.ts  # Formats Key {type, value} for display
│       │   └── directives/
│       │       └── auto-focus.directive.ts
│       │
│       ├── layout/                      # App shell — always loaded (not lazy)
│       │   ├── main-layout/
│       │   │   ├── main-layout.component.ts
│       │   │   └── main-layout.component.html
│       │   ├── sidebar/
│       │   │   ├── sidebar.component.ts
│       │   │   ├── sidebar.component.html
│       │   │   └── sidebar.component.scss
│       │   └── topbar/
│       │       ├── topbar.component.ts
│       │       ├── topbar.component.html
│       │       └── topbar.component.scss
│       │
│       └── features/                    # One sub-directory per lazy-loaded feature
│           ├── dashboard/
│           │   ├── dashboard.routes.ts
│           │   ├── dashboard.component.ts
│           │   ├── dashboard.component.html
│           │   └── services/
│           │       └── dashboard.service.ts
│           ├── users/
│           │   ├── users.routes.ts
│           │   ├── users-list/
│           │   │   ├── users-list.component.ts
│           │   │   └── users-list.component.html
│           │   ├── user-detail/
│           │   │   ├── user-detail.component.ts
│           │   │   └── user-detail.component.html
│           │   ├── user-risk/
│           │   │   ├── user-risk.component.ts
│           │   │   └── user-risk.component.html
│           │   ├── dialogs/
│           │   │   └── create-user-dialog.component.ts
│           │   └── services/
│           │       ├── users-api.service.ts
│           │       └── users-state.service.ts
│           ├── devices/
│           │   ├── devices.routes.ts
│           │   ├── devices-list/
│           │   ├── device-detail/
│           │   └── services/
│           │       ├── devices-api.service.ts
│           │       └── devices-state.service.ts
│           ├── alerts/
│           │   ├── alerts.routes.ts
│           │   ├── alerts-list/
│           │   ├── alert-detail/
│           │   └── services/
│           │       ├── alerts-api.service.ts
│           │       └── alerts-state.service.ts
│           ├── analysis/
│           │   ├── analysis.routes.ts
│           │   ├── analysis-list/
│           │   └── services/
│           │       └── analysis-api.service.ts
│           ├── blacklists/
│           │   ├── blacklists.routes.ts  # Sub-routes for phishing, phones, banks, categories, domains
│           │   ├── phishing/
│           │   ├── phones/
│           │   ├── banks/
│           │   ├── categories/
│           │   │   ├── categories-list/
│           │   │   ├── category-form/    # Shared create/edit form
│           │   │   └── services/
│           │   ├── domains/
│           │   │   ├── domains-list/
│           │   │   └── services/
│           │   └── services/
│           │       └── blacklists-api.service.ts  # Single API service for all blacklist sub-areas
│           ├── roadmaps/
│           │   ├── roadmaps.routes.ts
│           │   ├── roadmaps-list/
│           │   └── services/
│           │       └── roadmaps-api.service.ts
│           ├── simulations/
│           │   ├── simulations.routes.ts
│           │   ├── simulations-list/
│           │   ├── simulation-form/      # Shared create/edit form with step editor
│           │   └── services/
│           │       ├── simulations-api.service.ts
│           │       └── simulations-state.service.ts
│           ├── system/
│           │   ├── system.routes.ts
│           │   ├── system.component.ts
│           │   └── services/
│           │       └── system-api.service.ts
│           ├── downloads/
│           │   ├── downloads.routes.ts
│           │   └── downloads.component.ts
│           └── access-denied/
│               └── access-denied.component.ts
```

### 1.3 Module Organization -- Standalone Components with Lazy Loading

Angular 18+ favors standalone components over NgModules. The project will use **standalone components** with `loadChildren` / `loadComponent` for lazy loading.

Top-level route configuration in `app.routes.ts`:

```typescript
export const appRoutes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadChildren: () => import('./features/dashboard/dashboard.routes')
          .then(m => m.DASHBOARD_ROUTES)
      },
      {
        path: 'users',
        loadChildren: () => import('./features/users/users.routes')
          .then(m => m.USERS_ROUTES)
      },
      {
        path: 'devices',
        loadChildren: () => import('./features/devices/devices.routes')
          .then(m => m.DEVICES_ROUTES)
      },
      {
        path: 'alerts',
        loadChildren: () => import('./features/alerts/alerts.routes')
          .then(m => m.ALERTS_ROUTES)
      },
      {
        path: 'analysis',
        loadChildren: () => import('./features/analysis/analysis.routes')
          .then(m => m.ANALYSIS_ROUTES)
      },
      {
        path: 'blacklists',
        loadChildren: () => import('./features/blacklists/blacklists.routes')
          .then(m => m.BLACKLISTS_ROUTES)
      },
      {
        path: 'roadmaps',
        loadChildren: () => import('./features/roadmaps/roadmaps.routes')
          .then(m => m.ROADMAPS_ROUTES)
      },
      {
        path: 'simulations',
        loadChildren: () => import('./features/simulations/simulations.routes')
          .then(m => m.SIMULATIONS_ROUTES)
      },
      {
        path: 'system',
        loadChildren: () => import('./features/system/system.routes')
          .then(m => m.SYSTEM_ROUTES)
      },
      {
        path: 'downloads',
        loadComponent: () => import('./features/downloads/downloads.component')
          .then(c => c.DownloadsComponent)
      },
    ]
  },
  {
    path: 'access-denied',
    loadComponent: () => import('./features/access-denied/access-denied.component')
      .then(c => c.AccessDeniedComponent)
  },
  { path: '**', redirectTo: 'dashboard' }
];
```

### 1.4 Lazy Loading Boundaries

Each lazy-loaded chunk corresponds to a sidebar section:

| Chunk | Routes | Estimated Size |
|---|---|---|
| `dashboard` | `/dashboard` | Small -- KPI cards + summary |
| `users` | `/users`, `/users/:key`, `/users/:key/risk` | Medium -- list + detail + risk |
| `devices` | `/devices`, `/devices/:key` | Medium -- list + detail |
| `alerts` | `/alerts`, `/alerts/:key` | Medium -- list + detail |
| `analysis` | `/analysis` | Small -- list only |
| `blacklists` | `/blacklists/phishing`, `phones`, `banks`, `categories/**`, `domains` | Large -- 5 sub-areas |
| `roadmaps` | `/roadmaps` | Small -- list only |
| `simulations` | `/simulations`, `/simulations/new`, `/simulations/:id/edit` | Medium -- list + form |
| `system` | `/system` | Small -- settings page |
| `downloads` | `/downloads` | Tiny -- static page |

The layout shell (`MainLayoutComponent`, `SidebarComponent`, `TopbarComponent`) is NOT lazy-loaded -- it is part of the initial bundle since it wraps every authenticated route.

---

## 2. Angular Signals State Management

### 2.1 Design Decision

**Angular Signals + service-based state** (see ADR-0002). No NgRx. Each feature area that requires state beyond a single component gets an injectable `*StateService` holding writable signals.

### 2.2 Service-Based State Pattern

Every state service follows this structure:

```typescript
@Injectable({ providedIn: 'root' })
export class UsersStateService {
  // --- Writable signals (private set, public read) ---
  private readonly _users = signal<UserWithDeviceCount[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  // --- Paging state ---
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _sortBy = signal<string | null>(null);
  private readonly _sortDirection = signal<'asc' | 'desc'>('asc');

  // --- Public read-only signals ---
  readonly users = this._users.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();

  // --- Computed signals ---
  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly hasData = computed(() => this._users().length > 0);
  readonly isEmpty = computed(() =>
    !this._loading() && this._users().length === 0
  );

  constructor(private usersApi: UsersApiService) {}

  // --- Actions ---
  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize) this._pageSize.set(pageSize);
    this.fetchUsers();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1); // reset to first page on new search
    this.fetchUsers();
  }

  setSort(sortBy: string, direction: 'asc' | 'desc'): void {
    this._sortBy.set(sortBy);
    this._sortDirection.set(direction);
    this.fetchUsers();
  }

  private fetchUsers(): void {
    this._loading.set(true);
    this._error.set(null);

    const request: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
      sortBy: this._sortBy() || undefined,
      sortDirection: this._sortDirection(),
    };

    this.usersApi.getAll(request).subscribe({
      next: (result) => {
        this._users.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err.message ?? 'Failed to load users');
        this._loading.set(false);
      }
    });
  }
}
```

### 2.3 Paged List State Pattern

Every feature area with a paged list follows the same pattern above. The signals are:

| Signal | Type | Purpose |
|---|---|---|
| `_items` | `WritableSignal<T[]>` | Current page items |
| `_totalCount` | `WritableSignal<number>` | Total items matching filters |
| `_loading` | `WritableSignal<boolean>` | Request in flight |
| `_error` | `WritableSignal<string \| null>` | Last error message |
| `_page` | `WritableSignal<number>` | Current 1-based page |
| `_pageSize` | `WritableSignal<number>` | Items per page |
| `_search` | `WritableSignal<string>` | Current search term |
| `_sortBy` | `WritableSignal<string \| null>` | Active sort column |
| `_sortDirection` | `WritableSignal<'asc' \| 'desc'>` | Sort direction |
| `totalPages` | `Signal<number>` | Computed from totalCount/pageSize |
| `isEmpty` | `Signal<boolean>` | Computed: not loading and no items |

### 2.4 Shared State Between Components

For cross-component state (e.g., alert count badge in sidebar), use a service-level signal injected into both components:

```typescript
@Injectable({ providedIn: 'root' })
export class AlertBadgeService {
  private readonly _newAlertCount = signal(0);
  readonly newAlertCount = this._newAlertCount.asReadonly();

  increment(): void {
    this._newAlertCount.update(n => n + 1);
  }

  reset(): void {
    this._newAlertCount.set(0);
  }
}
```

The `SidebarComponent` reads `alertBadgeService.newAlertCount()` in its template. The `SignalRService` calls `alertBadgeService.increment()` when it receives a `DeviceAlert` notification. When the user navigates to the alerts page, `AlertsListComponent.ngOnInit()` calls `alertBadgeService.reset()`.

### 2.5 CRUD Operation State Pattern

For create/update/delete operations, the state service exposes an operation-level signal:

```typescript
// Inside UsersStateService
private readonly _saving = signal(false);
private readonly _saveError = signal<string | null>(null);
readonly saving = this._saving.asReadonly();
readonly saveError = this._saveError.asReadonly();

createUser(request: CreateUserRequest): Observable<void> {
  this._saving.set(true);
  this._saveError.set(null);

  return this.usersApi.create(request).pipe(
    tap(() => {
      this._saving.set(false);
      this.fetchUsers(); // refresh list
    }),
    catchError((err) => {
      this._saving.set(false);
      this._saveError.set(err.message ?? 'Failed to create user');
      return throwError(() => err);
    }),
    map(() => void 0)
  );
}
```

Components subscribe to `saving` and `saveError` to control form states (disable buttons, show spinners, show inline errors).

---

## 3. Authentication Flow

### 3.1 Keycloak PKCE Flow -- Sequence

```
User                Angular SPA              Keycloak                 WebApi
 │                      │                       │                       │
 ├─ Navigate ──────────►│                       │                       │
 │                      ├─ Check token ─────────┤                       │
 │                      │  (no valid token)     │                       │
 │                      ├─ Redirect ───────────►│                       │
 │                      │  (PKCE code_challenge) │                       │
 │  ◄── Login page ─────┤                       │                       │
 ├─ Credentials ────────┼──────────────────────►│                       │
 │                      │  ◄── auth code ───────┤                       │
 │                      ├─ Exchange code ──────►│                       │
 │                      │  (code_verifier)      │                       │
 │                      │  ◄── access_token ────┤                       │
 │                      │      refresh_token    │                       │
 │                      │                       │                       │
 │                      ├─ GET /api/users ──────┼──────────────────────►│
 │                      │  Authorization: Bearer│<access_token>         │
 │                      │                       │      Validate JWT ────┤
 │                      │                       │      (JWKS endpoint)  │
 │                      │  ◄── 200 OK ──────────┼──────────────────────┤
 │  ◄── Render ─────────┤                       │                       │
```

### 3.2 Auth Guard

The auth guard uses the `keycloak-angular` library's built-in guard factory:

```typescript
// auth.guard.ts
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { KeycloakService } from 'keycloak-angular';

export const authGuard = async (): Promise<boolean> => {
  const keycloak = inject(KeycloakService);
  const router = inject(Router);

  const isAuthenticated = keycloak.isLoggedIn();

  if (!isAuthenticated) {
    await keycloak.login({
      redirectUri: window.location.origin + '/dashboard',
    });
    return false;
  }

  // Check Admin role
  const roles = keycloak.getUserRoles(true); // realm roles
  const hasAdminRole = roles.includes('admin') || roles.includes('Admin');

  // Also check groups if roles not present
  const token = keycloak.getKeycloakInstance().tokenParsed;
  const groups: string[] = token?.['groups'] ?? [];
  const isInAdminGroup = groups.some(
    g => g === '/Administrators' || g === 'Administrators'
  );

  if (!hasAdminRole && !isInAdminGroup) {
    router.navigate(['/access-denied']);
    return false;
  }

  return true;
};
```

### 3.3 JWT HTTP Interceptor

```typescript
// auth.interceptor.ts
import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { KeycloakService } from 'keycloak-angular';
import { from, switchMap } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const keycloak = inject(KeycloakService);

  // Only add token to API requests (not external URLs)
  if (!req.url.startsWith('/api') && !req.url.includes('/notificationshub')) {
    return next(req);
  }

  return from(keycloak.getToken()).pipe(
    switchMap(token => {
      if (token) {
        const authReq = req.clone({
          setHeaders: { Authorization: `Bearer ${token}` }
        });
        return next(authReq);
      }
      return next(req);
    })
  );
};
```

### 3.4 Token Refresh Strategy

The `keycloak-angular` adapter handles token refresh automatically:

- **`initOptions.onLoad: 'check-sso'`** -- checks SSO session on app init
- **`initOptions.silentCheckSsoRedirectUri`** -- uses an invisible iframe for silent token renewal
- **`initOptions.enableBearerInterceptor: false`** -- we use our own interceptor (see 3.3) to control which requests get the token
- **Token refresh threshold:** the adapter calls `updateToken(30)` before each request, refreshing if the token expires within 30 seconds

If the refresh token itself is expired (session timeout), the interceptor's 401 handling (see Section 6.3) redirects to Keycloak login.

### 3.5 Admin Role Resolution

The backend resolves admin role from multiple Keycloak claim sources (as seen in `AdminClaimsTransformer.cs` and `Program.cs` `OnTokenValidated`):

1. **Hardcoded usernames** -- `asps-admin`, `isaac`, `admin` (existing behavior)
2. **Groups claim** -- `groups` contains `/Administrators` or `Administrators`
3. **Realm access roles** -- `realm_access.roles` contains `admin`

For the Angular SPA, the JWT access token must contain these same claims. Keycloak client configuration must include the `groups` and `realm_access` claims in the token. This is configured via Keycloak client mappers (see 3.6).

On the Angular side, the `authGuard` checks both `realm_access.roles` and `groups` claims from the parsed JWT. On the backend side, the `AdminClaimsTransformer` applies identically to both Cookie-authenticated and JWT-authenticated principals.

### 3.6 Keycloak Client Configuration -- `asps-angular-admin`

Create a new Keycloak client in the `asps` realm:

| Setting | Value |
|---|---|
| Client ID | `asps-angular-admin` |
| Client Protocol | openid-connect |
| Access Type | public |
| Standard Flow Enabled | true |
| Direct Access Grants | false |
| PKCE Code Challenge Method | S256 |
| Valid Redirect URIs | `http://localhost:4200/*`, `https://<production-url>/*` |
| Valid Post Logout Redirect URIs | `http://localhost:4200/*`, `https://<production-url>/*` |
| Web Origins | `http://localhost:4200`, `https://<production-url>` |
| Root URL | `http://localhost:4200` |

Required client mappers (protocol mappers):

| Mapper Name | Mapper Type | Token Claim Name | Purpose |
|---|---|---|---|
| groups | Group Membership | groups | Populates `groups` claim in the access token |
| realm roles | User Realm Role | realm_access.roles | Populates `realm_access` claim (usually default) |
| audience | Audience Resolve | N/A | Ensures the access token audience includes `asps-angular-admin` |

### 3.7 Application Initialization

```typescript
// app.config.ts
import { APP_INITIALIZER, ApplicationConfig } from '@angular/core';
import { KeycloakService } from 'keycloak-angular';
import { RuntimeConfigService } from './core/services/runtime-config.service';

function initializeKeycloak(
  keycloak: KeycloakService,
  config: RuntimeConfigService
): () => Promise<boolean> {
  return () =>
    config.load().then(() =>
      keycloak.init({
        config: {
          url: config.keycloakUrl,
          realm: 'asps',
          clientId: 'asps-angular-admin',
        },
        initOptions: {
          onLoad: 'check-sso',
          silentCheckSsoRedirectUri:
            window.location.origin + '/assets/silent-check-sso.html',
          checkLoginIframe: false,
          pkceMethod: 'S256',
        },
        enableBearerInterceptor: false,  // We use our own interceptor
      })
    );
}

export const appConfig: ApplicationConfig = {
  providers: [
    // ... other providers
    KeycloakService,
    {
      provide: APP_INITIALIZER,
      useFactory: initializeKeycloak,
      multi: true,
      deps: [KeycloakService, RuntimeConfigService],
    },
  ],
};
```

---

## 4. Angular Material Setup

### 4.1 Theme Configuration

The custom Angular Material theme matches the existing Razor admin color scheme extracted from `_Layout.cshtml`:

| CSS Variable | Hex Value | Angular Material Role |
|---|---|---|
| `--navy` | `#1A2255` | Primary palette |
| `--navy-dark` | `#111740` | Primary darker variant |
| `--green` | `#22C55E` | Accent palette |
| `--danger` | `#ef4444` | Warn palette |
| `--warning` | `#f59e0b` | Custom warn-secondary (not Material default) |

Theme SCSS in `styles.scss`:

```scss
@use '@angular/material' as mat;

// Custom palette definitions
$asps-navy: mat.m2-define-palette((
  50:  #e4e5ec, 100: #bbbdd0, 200: #8d91b1,
  300: #5f6592, 400: #3d447b, 500: #1A2255,
  600: #171e4e, 700: #131944, 800: #0f143b,
  900: #080c2a,
  contrast: (
    50: rgba(0,0,0,.87), 100: rgba(0,0,0,.87), 200: rgba(0,0,0,.87),
    300: #fff, 400: #fff, 500: #fff, 600: #fff, 700: #fff, 800: #fff, 900: #fff
  )
));

$asps-green: mat.m2-define-palette((
  50:  #e5f8eb, 100: #beedce, 200: #93e2ae,
  300: #67d78d, 400: #47cf75, 500: #22C55E,
  600: #1ebf56, 700: #19b84c, 800: #14b042,
  900: #0ca331,
  contrast: (
    50: rgba(0,0,0,.87), 100: rgba(0,0,0,.87), 200: rgba(0,0,0,.87),
    300: rgba(0,0,0,.87), 400: #fff, 500: #fff, 600: #fff, 700: #fff, 800: #fff, 900: #fff
  )
));

$asps-red: mat.m2-define-palette((
  500: #ef4444,
  contrast: (500: #fff)
));

$asps-theme: mat.m2-define-light-theme((
  color: (
    primary: $asps-navy,
    accent: $asps-green,
    warn: $asps-red,
  ),
  typography: mat.m2-define-typography-config(
    $font-family: '"Inter", sans-serif',
    $headline-5: mat.m2-define-typography-level(20px, 28px, 800, 'Syne', -0.5px),
  ),
  density: 0,
));

@include mat.all-component-themes($asps-theme);

// Global overrides matching existing admin styles
:root {
  --navy: #1A2255;
  --navy-dark: #111740;
  --green: #22C55E;
  --danger: #ef4444;
  --warning: #f59e0b;
  --sidebar-w: 260px;
}

body {
  font-family: 'Inter', sans-serif;
  background: #f0f2f8;
  margin: 0;
}
```

### 4.2 Material Component Mapping

| UI Pattern | Material Component | Notes |
|---|---|---|
| Data tables (all lists) | `mat-table` + `mat-paginator` + `mat-sort` | Wrapped by `PagedTableComponent` |
| Search input | `mat-form-field` + `matInput` | With debounce in the paged table |
| Create/edit forms | `mat-form-field`, `mat-select`, `mat-checkbox`, `mat-datepicker` | Standard reactive forms |
| Confirmation dialogs | `MatDialog` + `ConfirmDialogComponent` | Reusable via service method |
| Sidebar navigation | `mat-sidenav` + `mat-nav-list` | Permanent sidenav on desktop, toggle on mobile |
| Top toolbar | `mat-toolbar` | Fixed position, contains title + user menu |
| Alert count badge | `matBadge` | On sidebar "Device Alerts" nav item |
| Severity indicators | `mat-chip` (styled via pipe) | Color-coded: high=red, medium=amber, low=green |
| KPI cards | Custom component (not Material) | Matches existing `stat-card` design |
| Toast notifications | `MatSnackBar` | For success/error messages |
| Tab groups (user detail) | `mat-tab-group` | Profile, Devices, Alerts, Risk Score tabs |
| Loading states | `mat-progress-bar` (indeterminate) | Above table during loads |
| Dropdown menus | `mat-menu` | User menu in topbar, row actions in tables |
| Icon buttons | `mat-icon-button` + `mat-icon` | Edit, delete, view actions |
| Tooltips | `matTooltip` | Action button descriptions |

### 4.3 Sidebar Navigation Pattern

Use `mat-sidenav-container` with a permanent `mat-sidenav` on desktop (>= 1024px) and a toggleable drawer on smaller screens:

```html
<mat-sidenav-container>
  <mat-sidenav #sidenav
    [mode]="isDesktop() ? 'side' : 'over'"
    [opened]="isDesktop()"
    class="sidebar">
    <!-- Navigation content -->
    <mat-nav-list>
      <div class="sidebar-section">Navigation</div>
      <a mat-list-item routerLink="/dashboard" routerLinkActive="active">
        <mat-icon matListItemIcon>dashboard</mat-icon>
        <span matListItemTitle>Dashboard</span>
      </a>
      <div class="sidebar-section">Management</div>
      <a mat-list-item routerLink="/users" routerLinkActive="active">
        <mat-icon matListItemIcon>people</mat-icon>
        <span matListItemTitle>Users</span>
      </a>
      <!-- ... more items ... -->
      <a mat-list-item routerLink="/alerts" routerLinkActive="active">
        <mat-icon matListItemIcon>notifications</mat-icon>
        <span matListItemTitle>Device Alerts</span>
        <span matListItemMeta>
          @if (alertBadge.newAlertCount() > 0) {
            <span class="alert-badge">{{ alertBadge.newAlertCount() }}</span>
          }
        </span>
      </a>
    </mat-nav-list>
  </mat-sidenav>

  <mat-sidenav-content>
    <app-topbar (menuToggle)="sidenav.toggle()"></app-topbar>
    <main class="main-content">
      <router-outlet />
    </main>
  </mat-sidenav-content>
</mat-sidenav-container>
```

The `isDesktop` signal uses Angular's `BreakpointObserver`:

```typescript
private breakpointObserver = inject(BreakpointObserver);
isDesktop = toSignal(
  this.breakpointObserver.observe('(min-width: 1024px)')
    .pipe(map(result => result.matches)),
  { initialValue: true }
);
```

---

## 5. Shared Components Architecture

### 5.1 PagedTableComponent

A generic, reusable server-side paged table wrapping `mat-table`, `mat-paginator`, and `mat-sort`.

**Inputs:**

| Input | Type | Description |
|---|---|---|
| `columns` | `ColumnDef[]` | Column definitions (key, header label, sortable flag, cell template ref) |
| `items` | `Signal<T[]>` | Current page items from state service |
| `totalCount` | `Signal<number>` | Total matching items |
| `loading` | `Signal<boolean>` | Loading state |
| `page` | `Signal<number>` | Current page |
| `pageSize` | `Signal<number>` | Current page size |
| `pageSizeOptions` | `number[]` | Available page sizes (default: `[10, 25, 50, 100]`) |
| `searchPlaceholder` | `string` | Placeholder text for the search input |
| `showSearch` | `boolean` | Whether to show the search bar (default: `true`) |

**Outputs:**

| Output | Type | Description |
|---|---|---|
| `pageChange` | `EventEmitter<PageEvent>` | Emitted on page/pageSize change |
| `sortChange` | `EventEmitter<Sort>` | Emitted on sort change |
| `searchChange` | `EventEmitter<string>` | Emitted on search input (debounced 300ms) |
| `rowClick` | `EventEmitter<T>` | Emitted when a row is clicked |

**ColumnDef interface:**

```typescript
interface ColumnDef {
  key: string;         // Property name on the item
  header: string;      // Display label
  sortable?: boolean;  // Whether this column is sortable (default: false)
  type?: 'text' | 'date' | 'badge' | 'custom';  // Cell rendering type
  templateRef?: string; // For type='custom', reference to ng-template
}
```

**Usage example (users list):**

```html
<app-paged-table
  [columns]="columns"
  [items]="state.users"
  [totalCount]="state.totalCount"
  [loading]="state.loading"
  [page]="state.page"
  [pageSize]="state.pageSize"
  searchPlaceholder="Search users..."
  (pageChange)="onPageChange($event)"
  (sortChange)="onSortChange($event)"
  (searchChange)="onSearchChange($event)"
  (rowClick)="onRowClick($event)">

  <!-- Custom cell templates -->
  <ng-template #roleCell let-row>
    <mat-chip>{{ row.role }}</mat-chip>
  </ng-template>
</app-paged-table>
```

### 5.2 KpiCardComponent

Matches the existing `stat-card` design from `_Layout.cshtml`.

**Inputs:**

| Input | Type | Description |
|---|---|---|
| `label` | `string` | Card label (e.g., "Total Users") |
| `value` | `Signal<number \| string>` | Display value |
| `meta` | `string` | Description below value |
| `icon` | `string` | Material icon name |
| `color` | `'blue' \| 'green' \| 'amber' \| 'red'` | Accent color |
| `routerLink` | `string` | Navigation target on click |
| `loading` | `Signal<boolean>` | Show skeleton placeholder |

### 5.3 ConfirmDialogComponent

A reusable confirmation dialog opened via `MatDialog`.

**Interface:**

```typescript
interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;     // default: "Confirm"
  cancelText?: string;      // default: "Cancel"
  confirmColor?: 'primary' | 'accent' | 'warn';  // default: 'warn'
}
```

**Usage via service helper:**

```typescript
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private dialog = inject(MatDialog);

  confirm(data: ConfirmDialogData): Observable<boolean> {
    return this.dialog
      .open(ConfirmDialogComponent, { data, width: '400px' })
      .afterClosed()
      .pipe(map(result => result === true));
  }
}
```

### 5.4 SeverityBadgePipe

Transforms a severity string into a styled chip/badge. Matches the existing `sev-badge` CSS classes.

```typescript
@Pipe({ name: 'severityBadge', standalone: true })
export class SeverityBadgePipe implements PipeTransform {
  transform(value: string): { label: string; cssClass: string } {
    const level = (value ?? '').toLowerCase();
    switch (level) {
      case 'critical':
      case 'high':
        return { label: value, cssClass: 'sev-badge high' };
      case 'medium':
        return { label: value, cssClass: 'sev-badge medium' };
      case 'low':
        return { label: value, cssClass: 'sev-badge low' };
      default:
        return { label: value || 'Unknown', cssClass: 'sev-badge' };
    }
  }
}
```

Used in templates:

```html
@let badge = row.severity | severityBadge;
<span [class]="badge.cssClass">{{ badge.label }}</span>
```

### 5.5 Component Communication Patterns

| Pattern | When to Use | Example |
|---|---|---|
| `@Input()` / `@Output()` | Parent-child data binding | `PagedTableComponent` columns/events |
| Signal-based service | Cross-component shared state | `AlertBadgeService` (sidebar + SignalR) |
| `MatDialog` data/result | Modal dialogs | `ConfirmDialogService`, `CreateUserDialogComponent` |
| Router params | URL-driven state | `/users/:key` -> `ActivatedRoute.params` |
| Template reference variables | Custom table cell templates | `#roleCell` passed to `PagedTableComponent` |

Avoid `@ViewChild`-based communication and direct component references. Prefer injectable services with signals for shared state, and `@Input`/`@Output` for hierarchical relationships.

---

## 6. API Communication Layer

### 6.1 Base API Service

A generic service that encapsulates standard CRUD patterns and paging:

```typescript
@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private config = inject(RuntimeConfigService);

  private get baseUrl(): string {
    return this.config.apiUrl;  // e.g., "https://localhost:5002"
  }

  // --- Generic CRUD ---

  getOne<T>(path: string): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`);
  }

  getPage<T>(path: string, request: PagedRequest): Observable<PagedResult<T>> {
    const params = this.buildPageParams(request);
    return this.http.get<PagedResult<T>>(`${this.baseUrl}${path}`, { params });
  }

  post<TReq, TRes>(path: string, body: TReq): Observable<TRes> {
    return this.http.post<TRes>(`${this.baseUrl}${path}`, body);
  }

  put<TReq, TRes>(path: string, body: TReq): Observable<TRes> {
    return this.http.put<TRes>(`${this.baseUrl}${path}`, body);
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`);
  }

  // --- Helpers ---

  private buildPageParams(request: PagedRequest): HttpParams {
    let params = new HttpParams()
      .set('page', request.page?.toString() ?? '1')
      .set('pageSize', request.pageSize?.toString() ?? '25');

    if (request.search) params = params.set('search', request.search);
    if (request.sortBy) params = params.set('sortBy', request.sortBy);
    if (request.sortDirection) params = params.set('sortDirection', request.sortDirection);

    return params;
  }
}
```

### 6.2 Paging Request/Response Contract

Matches the spec (Section 3.1) and the backend contract exactly:

```typescript
// paging.model.ts
export interface PagedRequest {
  page?: number;           // 1-based, default 1
  pageSize?: number;       // default 25, max 100
  search?: string;         // free-text filter
  sortBy?: string;         // column name
  sortDirection?: 'asc' | 'desc';  // default 'asc'
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```

Feature-specific API services extend this pattern:

```typescript
@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private api = inject(ApiService);

  getAll(request: PagedRequest): Observable<PagedResult<UserWithDeviceCount>> {
    return this.api.getPage<UserWithDeviceCount>('/api/users', request);
  }

  getByKey(keyType: string, keyValue: string): Observable<UserDetails> {
    return this.api.getOne<UserDetails>(`/api/users/${keyType}/${keyValue}/details`);
  }

  create(request: CreateUserRequest): Observable<{ key: Key; message: string }> {
    return this.api.post('/api/users', request);
  }

  update(keyType: string, keyValue: string, request: UpdateUserRequest): Observable<{ message: string }> {
    return this.api.put(`/api/users/${keyType}/${keyValue}`, request);
  }

  delete(keyType: string, keyValue: string): Observable<{ message: string }> {
    return this.api.delete(`/api/users/${keyType}/${keyValue}`);
  }

  getRiskScore(keyType: string, keyValue: string): Observable<UserRiskScore> {
    return this.api.getOne(`/api/users/${keyType}/${keyValue}/risk-score`);
  }
}
```

### 6.3 Error Interceptor

Handles HTTP errors globally:

```typescript
// error.interceptor.ts
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(NotificationService);
  const keycloak = inject(KeycloakService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          // Token expired or invalid — redirect to Keycloak login
          keycloak.login({ redirectUri: window.location.href });
          break;

        case 403:
          router.navigate(['/access-denied']);
          break;

        case 0:
          // Network error (backend unreachable)
          notification.error(
            'Connection Error',
            'Unable to reach the server. Check your network connection.'
          );
          break;

        default:
          if (error.status >= 500) {
            const message = error.error?.message
              ?? error.error?.Message
              ?? 'An unexpected server error occurred.';
            notification.error('Server Error', message);
          } else if (error.status >= 400) {
            // 4xx client errors — surface the backend message
            const message = error.error?.message
              ?? error.error?.Message
              ?? error.error
              ?? 'Invalid request.';
            notification.warn('Request Failed', message);
          }
      }

      return throwError(() => error);
    })
  );
};
```

**Interceptor registration order** (in `app.config.ts`):

```typescript
provideHttpClient(
  withInterceptors([authInterceptor, errorInterceptor])
)
```

The `authInterceptor` runs first (attaches the token), then `errorInterceptor` handles response errors.

### 6.4 SignalR Service

Connects to the existing `/notificationshub` with JWT authentication:

```typescript
@Injectable({ providedIn: 'root' })
export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private keycloak = inject(KeycloakService);
  private config = inject(RuntimeConfigService);
  private alertBadge = inject(AlertBadgeService);
  private notification = inject(NotificationService);

  private readonly _connected = signal(false);
  readonly connected = this._connected.asReadonly();

  async start(): Promise<void> {
    const hubUrl = `${this.config.apiUrl}/notificationshub`;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => this.keycloak.getToken(),
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('ReceiveNotification', (data: { Message: string }) => {
      if (data.Message === 'DeviceAlert') {
        this.alertBadge.increment();
      }
      this.notification.info('Notification', data.Message);
    });

    this.connection.onreconnected(() => this._connected.set(true));
    this.connection.onclose(() => this._connected.set(false));

    try {
      await this.connection.start();
      this._connected.set(true);
    } catch (err) {
      console.error('SignalR connection failed:', err);
      this._connected.set(false);
    }
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this._connected.set(false);
    }
  }
}
```

The `MainLayoutComponent` starts the SignalR connection on init and stops it on destroy.

---

## 7. Shared TypeScript Models

### 7.1 Location

All TypeScript interfaces live in `src/app/core/models/`. Each domain gets its own file. A barrel `index.ts` re-exports everything:

```
src/app/core/models/
├── index.ts
├── paging.model.ts       # PagedRequest, PagedResult<T>
├── key.model.ts          # Key interface matching Common.Models.Key
├── user.model.ts         # User, UserWithDeviceCount, UserDetails, CreateUserRequest, ...
├── device.model.ts       # Device, UserDevice, CreateUserDeviceRequest, ...
├── alert.model.ts        # DeviceAlert
├── analysis.model.ts     # AnalysisResult
├── blacklist.model.ts    # PhishingWebsite, BlacklistedPhoneNumber, BankWebsite, WebsiteCategory, TrackedDomain
├── simulation.model.ts   # Simulation, SimulationStep
├── roadmap.model.ts      # Roadmap
├── system.model.ts       # SystemVersion, HealthCheck
├── risk-score.model.ts   # UserRiskScore, AxisScores, DimensionScores, Signal, DataSource
└── enums.ts              # DeviceType, MonitoringStatus, OperatingSystem, UserRole, CautionLevel, ...
```

### 7.2 Core Interfaces

```typescript
// key.model.ts
// Mirrors Common.Models.Key
export interface Key {
  type: string;
  value: string;
  instanceName?: string;
}

// enums.ts
// Mirrors Common.Enums.Enumerations
export type DeviceType = 'Unknown' | 'PersonalComputer' | 'MobilePhone' | 'Other';
export type MonitoringStatus = 'Disabled' | 'Enabled';
export type OperatingSystemType = 'Unknown' | 'Windows' | 'Linux' | 'MacOS' | 'Android' | 'iOS';
export type UserRole = 'Unknown' | 'Self' | 'Guardian' | 'Other';
export type CautionLevel = 'Low' | 'Medium' | 'High';
export type TrackMode = 'None' | 'Surf' | 'Click';
export type Severity = 'Unknown' | 'Low' | 'Medium' | 'High' | 'Critical';
export type Priority = 'Low' | 'Medium' | 'High' | 'Critical';

// user.model.ts
export interface User {
  key: Key;
  keycloakUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  dateCreated: string;  // ISO 8601
}

export interface UserWithDeviceCount extends User {
  deviceCount: number;
}

export interface UserDetails extends User {
  devices: UserDevice[];
  accounts: UserAccount[];
}

export interface CreateUserRequest {
  keycloakUserId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: UserRole;
  address?: string;
  city?: string;
  state?: string;
  zip?: string;
  country?: string;
  locale?: string;
  timezone?: string;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  address: string;
  city: string;
  phoneNumber: string;
}
```

(Full set of interfaces as specified in the spec Section 7. The complete set is defined in the spec and must be implemented 1:1.)

### 7.3 Sync Strategy with Backend DTOs

TypeScript interfaces are derived from the C# DTOs in `WebApi/DTOs/Dtos.cs` and `Common/Entities/`. The sync strategy:

1. **Manual alignment** -- Angular interfaces mirror C# DTOs property-by-property. This is acceptable given the small number of models (~20 interfaces) and the infrequent DTO changes in a stable admin CRUD app.

2. **Property name casing** -- Backend Newtonsoft JSON serialization must use `camelCase` (see Section 9.4). TypeScript interfaces use `camelCase` natively. Alignment is automatic once the backend serialization is configured.

3. **Date handling** -- All dates are serialized as ISO 8601 strings (`yyyy-MM-ddTHH:mm:ss.fffZ`). TypeScript models type them as `string`, not `Date`. Components parse to `Date` only when needed for display via the `DatePipe`.

4. **Key type** -- The C# `Key` class serializes as `{ "type": "User", "value": "abc123" }`. The TypeScript `Key` interface matches this structure. Backend API routes use `{keyType}/{keyValue}` path parameters which Angular constructs from `key.type` and `key.value`.

5. **Enums** -- C# enums serialize as their string names when Newtonsoft is configured with `StringEnumConverter` (see Section 9.4). TypeScript uses string union types.

---

## 8. Docker / Deployment

### 8.1 Multi-Stage Dockerfile

```dockerfile
# apps/admin/angular/Dockerfile
# Also referenced from repo root as Dockerfile.angular-admin

# ============================================================
# Stage 1: Build Angular SPA
# ============================================================
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files first (layer caching)
COPY apps/admin/angular/package.json apps/admin/angular/package-lock.json ./
RUN npm ci --no-audit

# Copy source and build
COPY apps/admin/angular/ ./
RUN npm run build -- --configuration=production --output-path=dist

# ============================================================
# Stage 2: Serve with nginx
# ============================================================
FROM nginx:1.27-alpine AS runtime

# Remove default nginx content
RUN rm -rf /usr/share/nginx/html/*

# Copy built SPA
COPY --from=build /app/dist/browser /usr/share/nginx/html

# Copy nginx config
COPY apps/admin/angular/nginx.conf /etc/nginx/conf.d/default.conf

# Copy runtime config injection script
COPY apps/admin/angular/docker-entrypoint.sh /docker-entrypoint.d/40-inject-config.sh
RUN chmod +x /docker-entrypoint.d/40-inject-config.sh

EXPOSE 80

# nginx base image already has CMD ["nginx", "-g", "daemon off;"]
```

### 8.2 nginx.conf

```nginx
# apps/admin/angular/nginx.conf
server {
    listen 80;
    server_name _;

    root /usr/share/nginx/html;
    index index.html;

    # SPA routing: all non-file requests serve index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets aggressively
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        access_log off;
    }

    # Do not cache index.html (it contains hashed asset references)
    location = /index.html {
        add_header Cache-Control "no-cache, no-store, must-revalidate";
        add_header Pragma "no-cache";
        add_header Expires "0";
    }

    # Runtime config file — not cached
    location = /assets/runtime-config.json {
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Gzip
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml;
    gzip_min_length 1000;
}
```

### 8.3 Runtime Configuration Injection

Environment-specific values (API URL, Keycloak URL) are injected at container startup, not baked into the Angular build. This allows the same Docker image to run in dev, staging, and production.

**Mechanism:** An entrypoint script writes `/usr/share/nginx/html/assets/runtime-config.json` from environment variables.

```bash
#!/bin/sh
# apps/admin/angular/docker-entrypoint.sh
# Writes runtime-config.json from environment variables

cat > /usr/share/nginx/html/assets/runtime-config.json <<EOF
{
  "apiUrl": "${API_URL:-http://localhost:5001}",
  "keycloakUrl": "${KEYCLOAK_URL:-http://localhost:8081}",
  "keycloakRealm": "${KEYCLOAK_REALM:-asps}",
  "keycloakClientId": "${KEYCLOAK_CLIENT_ID:-asps-angular-admin}"
}
EOF
```

**Angular RuntimeConfigService:**

```typescript
@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private config: RuntimeConfig | null = null;

  get apiUrl(): string {
    return this.config?.apiUrl ?? environment.apiUrl;
  }
  get keycloakUrl(): string {
    return this.config?.keycloakUrl ?? environment.keycloakUrl;
  }
  get keycloakRealm(): string {
    return this.config?.keycloakRealm ?? 'asps';
  }
  get keycloakClientId(): string {
    return this.config?.keycloakClientId ?? 'asps-angular-admin';
  }

  load(): Promise<void> {
    return fetch('/assets/runtime-config.json')
      .then(response => response.json())
      .then(config => { this.config = config; })
      .catch(() => {
        console.warn('runtime-config.json not found, using environment defaults');
      });
  }
}

interface RuntimeConfig {
  apiUrl: string;
  keycloakUrl: string;
  keycloakRealm: string;
  keycloakClientId: string;
}
```

This is loaded via `APP_INITIALIZER` before Keycloak init (see Section 3.7).

### 8.4 docker-compose.yml Integration

Add to the existing `docker-compose.yml`:

```yaml
  angular-admin:
    build:
      context: .
      dockerfile: apps/admin/angular/Dockerfile
    container_name: asps-angular-admin
    restart: unless-stopped
    depends_on:
      webapi:
        condition: service_started
      keycloak:
        condition: service_healthy
    environment:
      API_URL: "http://asps-webapi:8080"
      KEYCLOAK_URL: "http://keycloak:8080"
      KEYCLOAK_REALM: "asps"
      KEYCLOAK_CLIENT_ID: "asps-angular-admin"
    ports:
      - "4201:80"
    networks:
      - asps-network
```

**Port assignment:**

| Environment | Port | Notes |
|---|---|---|
| Dev (`ng serve`) | 4200 | Angular CLI dev server, proxied API |
| Docker (compose) | 4201 (host) -> 80 (container) | nginx serving static files |
| Production | TBD | Behind reverse proxy |

**Note on Keycloak URL:** The Angular SPA runs in the user's browser, not inside Docker. The browser must reach Keycloak at a browser-accessible URL (e.g., `http://localhost:8081`), not at the Docker-internal `http://keycloak:8080`. The `KEYCLOAK_URL` environment variable must be set to the browser-accessible address. For local Docker dev this is `http://localhost:8081` (the host-mapped port from the Keycloak container).

---

## 9. Backend Integration Points

### 9.1 JWT Bearer Authentication -- Dual Scheme

The existing `Program.cs` auth configuration must be extended to support JWT Bearer alongside Cookie+OIDC. This uses ASP.NET Core's policy scheme to select the authentication scheme based on the request:

```csharp
// In Program.cs — replace the existing AddAuthentication block (when keycloakEnabled)

builder.Services.AddAuthentication(options =>
{
    // Use a policy scheme that auto-selects based on the request
    options.DefaultScheme = "SmartScheme";
    options.DefaultChallengeScheme = "SmartScheme";
})
.AddPolicyScheme("SmartScheme", "Cookie or JWT", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // If the request has a Bearer token, use JWT
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        // For SignalR with access_token query param, use JWT
        if (context.Request.Path.StartsWithSegments("/notificationshub") &&
            context.Request.Query.ContainsKey("access_token"))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        // Otherwise, use cookies (Razor pages)
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(options =>
{
    // ... existing cookie config unchanged ...
})
.AddOpenIdConnect(options =>
{
    // ... existing OIDC config unchanged ...
})
.AddJwtBearer(options =>
{
    options.Authority = keycloakSection["Authority"];
    options.Audience = "asps-angular-admin";
    options.RequireHttpsMetadata = false; // dev only — set true in production

    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "preferred_username",
        RoleClaimType = ClaimTypes.Role,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidAudiences = new[] { "asps-angular-admin", "account" },
    };

    // Handle SignalR JWT from query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments("/notificationshub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
```

The existing `AdminClaimsTransformer` (`IClaimsTransformation`) applies automatically to both Cookie and JWT-authenticated principals -- ASP.NET Core invokes it on every authenticated request regardless of scheme. This means JWT-authenticated Angular users get the `Admin` role added by the same logic as Razor users.

### 9.2 CORS Policy

Add CORS in `Program.cs`:

```csharp
// --- Service registration ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularAdmin", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:4200" };

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Required for SignalR
    });
});

// --- Middleware pipeline (after UseRouting, before UseAuthentication) ---
app.UseCors("AngularAdmin");
```

Configuration in `appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200"]
  }
}
```

In `appsettings.Docker.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:4201", "http://localhost:4200"]
  }
}
```

**Middleware order** (critical -- must be exact):

```csharp
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AngularAdmin");   // <-- after UseRouting
app.UseAuthentication();
app.UseAuthorization();
```

### 9.3 PagedQuery / PagedResult Contract

These go in `Common/Models/` since both WebApi and Business reference the Common project.

```csharp
// Common/Models/Paging.cs
namespace Common.Models;

public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    /// Clamps page/pageSize to safe bounds.
    /// Call this in every controller action before passing to the Business layer.
    /// </summary>
    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 25;
        if (PageSize > 100) PageSize = 100;
        if (SortDirection != "asc" && SortDirection != "desc")
            SortDirection = "asc";
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

For the CQRS layer, paged queries extend the existing `Query` base class:

```csharp
// Common/Messaging/PagedQuery.cs
namespace Common.Messaging;

public abstract class PagedQuery : Query
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
}
```

Usage in a concrete query:

```csharp
public class GetAllUsersPagedQuery : PagedQuery
{
    // No additional properties needed — paging is inherited
}

public class GetAllUsersPagedQueryResult : QueryResult
{
    public PagedResult<UserDto> Result { get; set; } = new();
}
```

### 9.4 Response Format Conventions

| Convention | Current State | Required Change |
|---|---|---|
| **JSON casing** | Newtonsoft default (PascalCase for C# properties) | Add `CamelCasePropertyNamesContractResolver` |
| **Enum serialization** | Numeric values | Add `StringEnumConverter` |
| **Date format** | ISO 8601 (Newtonsoft default) | No change needed |
| **Null handling** | Included | Keep (explicit nulls help Angular) |
| **Error envelope** | Inconsistent (`BadRequest(message)` vs `BadRequest(new { error })`) | Standardize on `{ message: string }` for errors |

Required change in `Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling =
            Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.ContractResolver =
            new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter());
        options.SerializerSettings.DateTimeZoneHandling =
            Newtonsoft.Json.DateTimeZoneHandling.Utc;
    });
```

**Impact assessment:** Adding `CamelCasePropertyNamesContractResolver` changes all API responses from PascalCase to camelCase. This affects the existing Razor pages that consume API responses via JavaScript. The Razor pages use `DataTables` with client-side JavaScript that references response properties. These references must be audited and updated.

**Mitigation options:**

1. **Option A (recommended):** Add the camelCase resolver globally and fix the Razor JS references. This is the clean approach and aligns the entire API surface.
2. **Option B:** Apply camelCase only to new API controllers via a per-controller `JsonResult` wrapper. This avoids breaking Razor but creates inconsistency.

This is a trade-off decision for the implementers. Option A is recommended but the Razor JS audit is required work.

### 9.5 Controller Pattern for New API Endpoints

New controllers should follow the `ICQRSClient` pattern (used by `SimulationsApiController`) rather than the `INetMQClientService` pattern (used by `UsersController`). The `ICQRSClient` pattern is newer, cleaner, and uses the CQRS channel security layer.

```csharp
[ApiController]
[Route("api/devices")]
[Authorize(Roles = "Admin")]
public class DevicesApiController : ControllerBase
{
    private readonly ICQRSClient _cqrsClient;
    private readonly ILogger<DevicesApiController> _logger;

    public DevicesApiController(ICQRSClient cqrsClient, ILogger<DevicesApiController> logger)
    {
        _cqrsClient = cqrsClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request)
    {
        request.Normalize();
        try
        {
            var query = new GetAllDevicesPagedQuery
            {
                Page = request.Page,
                PageSize = request.PageSize,
                Search = request.Search,
                SortBy = request.SortBy,
                SortDirection = request.SortDirection,
            };

            var result = await _cqrsClient.SendQueryAsync<GetAllDevicesPagedQueryResult>(query);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting devices");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}
```

All new controllers must use `[Authorize(Roles = "Admin")]` at the class level (matching the existing `SystemController` pattern) to ensure both Cookie-authenticated and JWT-authenticated admin users are authorized.

---

## 10. Testing Strategy

### 10.1 Unit Tests (Jasmine + Karma)

**Scope:** Services, pipes, guards, interceptors, and component logic.

**Configuration:** Angular CLI default (`ng test`). Karma runner with Chrome Headless.

**Patterns:**

| Test Target | Approach |
|---|---|
| State services | Test signal values after calling actions. Mock API services. Verify computed signals. |
| API services | Test URL construction, parameter building. Use `HttpClientTestingModule`. |
| Pipes | Pure function tests. Input -> output verification. |
| Auth guard | Mock `KeycloakService`. Test authenticated/unauthenticated/non-admin scenarios. |
| Interceptors | Use `HttpClientTestingModule`. Verify headers added, errors handled. |

**Example -- state service test:**

```typescript
describe('UsersStateService', () => {
  let service: UsersStateService;
  let apiSpy: jasmine.SpyObj<UsersApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('UsersApiService', ['getAll']);
    TestBed.configureTestingModule({
      providers: [
        UsersStateService,
        { provide: UsersApiService, useValue: apiSpy },
      ]
    });
    service = TestBed.inject(UsersStateService);
  });

  it('should set loading to true while fetching', () => {
    apiSpy.getAll.and.returnValue(NEVER); // never completes
    service.loadPage(1);
    expect(service.loading()).toBeTrue();
  });

  it('should populate users on success', () => {
    const mockResult: PagedResult<UserWithDeviceCount> = {
      items: [{ /* ... */ }],
      totalCount: 1,
      page: 1,
      pageSize: 25,
      totalPages: 1,
    };
    apiSpy.getAll.and.returnValue(of(mockResult));
    service.loadPage(1);
    expect(service.users().length).toBe(1);
    expect(service.loading()).toBeFalse();
  });

  it('should reset to page 1 on search change', () => {
    apiSpy.getAll.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }));
    service.loadPage(3);
    service.setSearch('test');
    // Verify the API was called with page=1
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
  });
});
```

### 10.2 Component Tests

**Scope:** Verify component templates render correctly with given inputs. Test user interactions (clicks, form inputs) trigger expected outputs.

**Approach:** Use Angular `TestBed` with `ComponentFixture`. Provide mock state services with pre-set signal values.

```typescript
describe('UsersListComponent', () => {
  let fixture: ComponentFixture<UsersListComponent>;

  beforeEach(async () => {
    const mockState = {
      users: signal<UserWithDeviceCount[]>([]),
      loading: signal(false),
      totalCount: signal(0),
      page: signal(1),
      pageSize: signal(25),
      loadPage: jasmine.createSpy(),
      setSearch: jasmine.createSpy(),
    };

    await TestBed.configureTestingModule({
      imports: [UsersListComponent],
      providers: [
        { provide: UsersStateService, useValue: mockState },
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UsersListComponent);
  });

  it('should show empty state when no users', () => {
    fixture.detectChanges();
    const emptyState = fixture.nativeElement.querySelector('app-empty-state');
    expect(emptyState).toBeTruthy();
  });
});
```

### 10.3 E2E Approach

**Framework:** Playwright (preferred over Cypress for Angular 18+ and modern browser support).

**Scope:** Critical user flows only -- not exhaustive page coverage.

| E2E Scenario | Description |
|---|---|
| Login flow | Redirect to Keycloak, authenticate, land on dashboard |
| Dashboard loads | KPI cards display non-zero values |
| Users CRUD | List users, create a user, view details, delete |
| Paged table | Navigate pages, search, sort |
| Alert badge | Verify badge increments (requires SignalR mock or test notification) |
| Access denied | Verify non-admin user sees 403 page |

**E2E environment:** Playwright tests run against the Docker Compose stack (all services up). A test Keycloak user is pre-provisioned.

**Test file location:** `apps/admin/angular/e2e/`

### 10.4 Test Coverage Targets

| Layer | Target | Notes |
|---|---|---|
| Services (state + API) | 90%+ | Business-critical logic lives here |
| Pipes | 100% | Pure functions, easy to test |
| Guards / interceptors | 90%+ | Security-critical |
| Components | 70%+ | Template rendering + interaction |
| E2E | Critical paths | Not measured by coverage -- measured by scenario count |

---

## 11. Architecture Decision Records

### ADR-0001: Dual Authentication Scheme (Cookie + JWT Bearer)

- **Status:** Proposed
- **Date:** 2026-08-02
- **Deciders:** Architect, Backend agent

#### Context

WebApi currently authenticates via Cookie+OIDC (Keycloak). The Razor Pages admin requires this scheme. The new Angular SPA cannot use Cookie auth effectively across origins (SameSite restrictions, CORS complexity). The Angular SPA needs stateless JWT Bearer auth. Both admin UIs must coexist during the migration period and potentially beyond.

#### Decision

Add a `JwtBearer` authentication scheme alongside the existing `Cookie`+`OpenIdConnect` schemes. Use ASP.NET Core's `PolicyScheme` to automatically select the scheme based on the `Authorization: Bearer` header presence. Create a new Keycloak client (`asps-angular-admin`) with public access type and PKCE for the SPA.

#### Consequences

- **Easier:** Angular gets standard JWT auth. No cross-origin cookie complexity. Existing Razor pages work unchanged. The `AdminClaimsTransformer` applies to both schemes automatically.
- **Harder:** Two Keycloak clients to maintain. Backend must validate JWTs from the new client. The `NotificationsHubPolicy` must handle JWT tokens from the query string for SignalR.
- **Accepted trade-off:** Two auth schemes add complexity to `Program.cs`, but this is a well-established ASP.NET Core pattern with clear documentation.

#### Alternatives Considered

1. **Cookie auth for both** -- rejected because cross-origin cookies require `SameSite=None` + HTTPS, and the SPA would need a BFF (Backend For Frontend) pattern which adds unnecessary complexity.
2. **Replace Cookie with JWT entirely** -- rejected because it would break the existing Razor Pages admin during the coexistence period.

---

### ADR-0002: Angular Signals + Service-Based State (No NgRx)

- **Status:** Proposed
- **Date:** 2026-08-02
- **Deciders:** Architect, user (Isaac)

#### Context

The Angular admin is a CRUD-centric application with ~12 feature areas. State management options: NgRx (Redux pattern), NGXS, Akita, or Angular's built-in Signals with service-based state.

#### Decision

Use Angular's built-in Signals (`signal()`, `computed()`, `effect()`) combined with injectable state services. No external state management library. Each feature area gets one `*StateService` that holds writable signals for its data, paging, loading, and error states.

#### Consequences

- **Easier:** Fewer dependencies, less boilerplate, faster development, easier onboarding. Signals are a first-party Angular feature with strong future support.
- **Harder:** No Redux DevTools for time-travel debugging. No standardized action/reducer pattern for complex state transitions.
- **Accepted trade-off:** The admin app has low state complexity (mostly independent paged lists with search/sort). The lack of DevTools is acceptable. If a feature area grows complex enough to need formal state management (unlikely for an admin CRUD app), it can be refactored to use a store pattern within the service without changing the component contract.

#### Alternatives Considered

1. **NgRx** -- rejected as overkill. NgRx adds ~4 files per feature (actions, reducer, effects, selectors) for each CRUD entity. For 12 feature areas with simple list/detail/form patterns, this is excessive.
2. **NGXS** -- simpler than NgRx but still an external dependency. Rejected in favor of first-party Signals.

---

### ADR-0003: Server-Side Paging Contract

- **Status:** Proposed
- **Date:** 2026-08-02
- **Deciders:** Architect

#### Context

All list views in the admin require server-side paging. The current backend returns unpaged results from most list queries (e.g., `GetAll` returns all users). A consistent paging contract is needed between the Angular client and the backend.

#### Decision

Standardize on `PagedRequest` (query parameters) and `PagedResult<T>` (response body) as defined in the spec. Place `PagedRequest` and `PagedResult<T>` in the `Common/Models/` namespace. Place `PagedQuery` (CQRS base class) in `Common/Messaging/`. All new list endpoints accept `[FromQuery] PagedRequest` and return `PagedResult<T>`. Existing list endpoints are modified to accept optional paging parameters (defaulting to page=1, pageSize=25) for backward compatibility.

#### Consequences

- **Easier:** Single consistent contract for all lists. Angular's `PagedTableComponent` binds generically to any list endpoint. Adding new paged lists requires minimal code.
- **Harder:** Existing unpaged list queries in the Business layer must be refactored to support `Skip`/`Take`/`OrderBy`. This is the bulk of the backend work for this epic.
- **Accepted trade-off:** Refactoring existing queries is significant work but is mandatory per the spec. The uniform contract makes it worthwhile.

#### Alternatives Considered

1. **Cursor-based pagination** -- rejected because the admin UI uses page numbers (standard table pagination), not infinite scroll. Cursor-based adds complexity without benefit here.
2. **Client-side paging** -- rejected per spec requirement. Loading thousands of rows client-side is not acceptable for performance.

---

### ADR-0004: Keycloak SPA Client with PKCE

- **Status:** Proposed
- **Date:** 2026-08-02
- **Deciders:** Architect, DevOps

#### Context

The existing `asps-webapi` Keycloak client is confidential (has a `ClientSecret`). A browser-based SPA cannot securely store a client secret. The SPA needs a public client with PKCE for secure authorization code exchange.

#### Decision

Create a new Keycloak client `asps-angular-admin` with access type "public" and PKCE (S256) enabled. Use the `keycloak-angular` npm package (the official Keycloak Angular adapter) rather than a generic OIDC library.

#### Consequences

- **Easier:** `keycloak-angular` provides built-in Angular guard, interceptor, and token refresh. PKCE is the OAuth 2.1 standard for SPAs. The same Keycloak realm and user base is shared with the Razor admin.
- **Harder:** Two Keycloak clients to manage. The `keycloak-angular` library adds a dependency (~30KB gzipped).
- **Accepted trade-off:** A dedicated SPA client is standard practice. The library dependency is small and well-maintained (official Keycloak project).

#### Alternatives Considered

1. **Reuse `asps-webapi` client with BFF pattern** -- rejected because it adds a backend proxy layer and couples the Angular deployment to the WebApi server.
2. **Generic OIDC library (`angular-auth-oidc-client`)** -- viable but requires more custom configuration. `keycloak-angular` is purpose-built for Keycloak and simpler to set up.

---

### ADR-0005: Newtonsoft CamelCase + StringEnumConverter

- **Status:** Proposed
- **Date:** 2026-08-02
- **Deciders:** Architect, Backend agent

#### Context

The backend uses Newtonsoft.Json with default settings (PascalCase property names, numeric enum values). The Angular client expects camelCase JSON and string enum values. The mismatch must be resolved.

#### Decision

Add `CamelCasePropertyNamesContractResolver` and `StringEnumConverter` to the global Newtonsoft serializer settings in `Program.cs`. This changes all API responses globally (including those consumed by existing Razor Pages JavaScript).

#### Consequences

- **Easier:** All API responses become Angular-friendly. TypeScript interfaces match response properties directly. String enums are human-readable.
- **Harder:** Existing Razor Pages JavaScript (`DataTables` column references, AJAX handlers) must be audited for PascalCase property references and updated to camelCase.
- **Accepted trade-off:** The audit is bounded work (known set of Razor pages). The alternative (per-controller serialization) creates inconsistency and long-term maintenance burden.

#### Alternatives Considered

1. **Per-controller serialization settings** -- rejected because it creates two different JSON conventions in the same API, which is confusing and error-prone.
2. **Leave PascalCase, adapt Angular** -- rejected because it fights JavaScript/TypeScript conventions and requires awkward property mapping in every model.

---

## 12. Trade-offs and Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Newtonsoft PascalCase-to-camelCase change breaks Razor JS | Major | Audit all Razor page JS for property references before applying the change. Deploy as a separate task with QA. |
| Token refresh race condition (multiple concurrent 401s) | Low | `keycloak-angular` handles this internally with a refresh mutex. The HTTP interceptor queues requests during refresh. |
| CORS misconfiguration blocks Angular in dev/Docker | Medium | Use environment-specific CORS origins via `appsettings.{Environment}.json`. Include `http://localhost:4200` in dev and Docker configs. |
| SignalR auth with JWT (query string token exposure) | Medium | SignalR sends the JWT via query string for WebSocket upgrade. This is the standard pattern. HTTPS encrypts the query string in transit. Token lifetime is short (5 min). |
| Two CQRS client interfaces (`ICQRSClient` + `INetMQClientService`) | Low (technical debt) | New controllers use `ICQRSClient` exclusively. Migrating old controllers is a separate cleanup task. |
| `PagedQuery<T>` / `PagedResult<T>` placement | Low | Placed in `Common/Models/` and `Common/Messaging/`. Both WebApi and Business reference Common. |
| Keycloak client `asps-angular-admin` groups/roles claims missing | Medium | Explicitly document the required Keycloak client mappers (Section 3.6). Verify during Keycloak setup task. |
| Runtime config not loaded (fetch fails) | Low | `RuntimeConfigService.load()` catches errors and falls back to `environment.ts` defaults. Works for `ng serve` dev without Docker. |
| Angular Material theme mismatch with Razor admin | Low | Theme colors are extracted from `_Layout.cshtml` CSS variables. Visual parity can be verified side-by-side. |
| Large `blacklists` lazy chunk (5 sub-areas) | Low | If the chunk grows too large, split into sub-route lazy loading within the blacklists feature. Monitor bundle size with `ng build --stats-json`. |

---

## 13. Ownership

| Component | Owner Agent | Dependencies | Phase |
|---|---|---|---|
| Angular project scaffold (CLI init, folder structure, build pipeline) | Frontend agent | None | 1 |
| Angular Material theme + shared components | Frontend agent | None | 1 |
| Angular auth module (guard, interceptor, Keycloak init) | Frontend agent | Keycloak client (DevOps) + JWT Bearer (Backend) | 1 |
| Angular layout shell (sidebar, topbar, main layout) | Frontend agent | Material theme | 1 |
| Keycloak `asps-angular-admin` client creation + mappers | DevOps agent | Keycloak access | 1 |
| JWT Bearer auth in `Program.cs` (PolicyScheme + JwtBearer) | Backend agent | ADR-0001 | 1 |
| CORS policy in `Program.cs` | Backend agent | ADR-0001 | 1 |
| Newtonsoft camelCase + StringEnumConverter | Backend agent | ADR-0005 + Razor JS audit | 1 |
| `PagedRequest` / `PagedResult<T>` / `PagedQuery` in Common | Backend agent | ADR-0003 | 1 |
| Dashboard feature + `GET /api/dashboard/summary` | Frontend + Backend | Paging contract + auth | 2 |
| Users feature + modified/new API endpoints | Frontend + Backend | Paging contract + auth | 2 |
| Devices feature + new API endpoints | Frontend + Backend | Paging contract + auth | 2 |
| Alerts feature + new API endpoints + SignalR integration | Frontend + Backend | Paging contract + auth + SignalR | 3 |
| Analysis feature + new API endpoints | Frontend + Backend | Paging contract + auth | 3 |
| Blacklists feature (5 sub-areas) + new API endpoints | Frontend + Backend | Paging contract + auth | 4 |
| Roadmaps feature + new API endpoints | Frontend + Backend | Paging contract + auth | 5 |
| Simulations feature + extended API endpoints | Frontend + Backend | Paging contract + auth | 5 |
| System feature + new API endpoint | Frontend + Backend | Paging contract + auth | 6 |
| Downloads feature (static page) | Frontend agent | None | 6 |
| Docker container (nginx Dockerfile) | DevOps agent | Angular build | 1 |
| docker-compose.yml integration | DevOps agent | All containers | 1 |
| Localization (i18n/RTL) | Frontend agent | All features complete | 6 |
| E2E test setup + critical path tests | QA agent | Running Docker stack | 6 |
| Razor JS audit for camelCase migration | Backend agent | ADR-0005 | 1 |

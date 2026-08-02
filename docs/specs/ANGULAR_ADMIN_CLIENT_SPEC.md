# Angular Admin Client — Specification

**Epic:** Angular Admin Client
**Status:** Draft — pending Isaac's approval
**Date:** 2026-08-02
**Author:** CEO Agent

---

## 1. Overview

### 1.1 Goal

Build a modern Angular-based admin client for ASPS that replicates and improves upon the existing Razor Pages admin UI. The Angular client will run as an independent SPA with its own port, communicating with the WebApi backend via REST APIs.

### 1.2 Why

- **Modern stack** — Angular enables richer UX, component reuse, and better state management.
- **Separation of concerns** — decouple the admin UI from the backend server process.
- **Performance** — server-side paging, lazy loading, and client-side caching.
- **Future-ready** — single API surface for web, mobile, and third-party integrations.

### 1.3 Coexistence Strategy

During migration, both admin UIs will operate in parallel:

| Admin | URL | Stack | Port |
|---|---|---|---|
| **Existing (Razor)** | `https://<host>:5001/` | Razor Pages inside WebApi | 5001/5002 |
| **New (Angular)** | `https://<host>:4200/` (dev) / `https://<host>:4201/` (prod) | Angular SPA | 4200/4201 |

The Angular client will consume REST APIs served by the same WebApi backend. No Razor pages will be removed until the Angular client achieves full feature parity and is validated in production.

---

## 2. Architecture

### 2.1 Deployment Topology

```
┌─────────────────┐        REST / SignalR         ┌──────────────────┐
│  Angular SPA    │  ◄──────────────────────────►  │  WebApi (.NET 8) │
│  Port 4200/4201 │        HTTPS + JWT             │  Port 5001/5002  │
└─────────────────┘                                └──────────────────┘
                                                          │
                                                    CQRS (NetMQ)
                                                          │
                                                   ┌──────▼──────┐
                                                   │  Backend    │
                                                   │  Service    │
                                                   └─────────────┘
```

### 2.2 Technology Stack

| Concern | Technology |
|---|---|
| Framework | Angular 18+ (latest stable) |
| Language | TypeScript (strict mode) |
| State management | Angular Signals + service-based state |
| UI components | Angular Material |
| HTTP | Angular HttpClient with interceptors |
| Auth | @auth0/angular-jwt + Keycloak JS adapter |
| Real-time | @microsoft/signalr (npm package) |
| Tables | Angular Material table + mat-paginator (server-side paging) |
| Build | Angular CLI, Webpack/esbuild |
| Testing | Jasmine + Karma (unit), Cypress or Playwright (e2e) |

### 2.3 Authentication Flow

The Angular client authenticates via Keycloak OIDC, same as the Razor admin:

1. User navigates to Angular admin.
2. Angular redirects to Keycloak login page.
3. Keycloak returns an authorization code.
4. Angular exchanges the code for access + refresh tokens (PKCE flow).
5. Angular attaches the JWT access token to every API request via HTTP interceptor.
6. WebApi validates the JWT and resolves the Admin role (same logic as existing cookie auth).

**Backend change required:** WebApi currently uses cookie-based auth for Razor pages. For the Angular client, it must also accept JWT Bearer tokens. Both auth schemes will coexist.

### 2.4 CORS Configuration

WebApi must allow cross-origin requests from the Angular client's origin (`https://localhost:4200` in dev, production URL in prod). Required headers: `Authorization`, `Content-Type`. Methods: `GET`, `POST`, `PUT`, `DELETE`, `OPTIONS`.

---

## 3. Cross-Cutting Requirements

### 3.1 Server-Side Paging

**All list/table endpoints must support server-side paging.** This is a mandatory requirement across every list view.

#### API Contract

Every list endpoint accepts:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | 1 | Page number (1-based) |
| `pageSize` | int | 25 | Items per page (max 100) |
| `search` | string | null | Free-text search filter |
| `sortBy` | string | null | Column to sort by |
| `sortDirection` | string | `asc` | `asc` or `desc` |

Every list endpoint returns:

```json
{
  "items": [...],
  "totalCount": 1234,
  "page": 1,
  "pageSize": 25,
  "totalPages": 50
}
```

#### Affected Endpoints

All list endpoints in the system (see Section 5 for per-domain details):

- Users list
- Devices list
- Device Alerts list
- Analysis Results list
- Known Phishing Websites list
- Blacklisted Phone Numbers list
- Bank Websites list
- Website Categories list
- Tracked Domains list
- Roadmaps list
- Simulations list

### 3.2 Real-Time Notifications (SignalR)

The Angular client connects to the existing SignalR hub at `/notificationshub` for:

- **New device alert badge** — live counter on the Device Alerts nav item.
- **System notifications** — toast alerts for critical events.

Connection uses JWT authentication (same token as REST API calls).

### 3.3 Error Handling

- Global HTTP error interceptor with user-friendly toast messages.
- 401 → redirect to Keycloak login.
- 403 → "Access Denied" page.
- 5xx → generic error with retry option.

### 3.4 Responsive Design

- Desktop-first (matches current Razor admin).
- Sidebar navigation collapsible on smaller screens.
- Data tables horizontally scrollable on narrow viewports.

### 3.5 Localization

- Support English (default) and Hebrew (RTL).
- Use Angular i18n or ngx-translate.
- Existing Hebrew strings in the Roadmap section must be preserved.

---

## 4. Navigation Structure

Angular router structure matching the existing Razor admin sidebar:

```
/                           → redirect to /dashboard
/login                      → Keycloak redirect (handled by auth guard)
/dashboard                  → Dashboard (KPIs, status, threat feed)
/users                      → Users list
/users/:key                 → User details
/users/:key/risk            → User risk score detail
/devices                    → Devices list
/devices/:key               → Device details
/alerts                     → Device alerts list
/alerts/:key                → Alert details
/analysis                   → Analysis results list
/blacklists/phishing        → Known phishing websites
/blacklists/phones          → Blacklisted phone numbers
/blacklists/banks           → Bank websites
/blacklists/categories      → Website categories
/blacklists/categories/new  → Create category
/blacklists/categories/:name/edit → Edit category
/blacklists/domains         → Tracked domains
/roadmaps                   → Roadmaps list (view/create/archive only; editor remains standalone HTML)
/simulations                → Simulations list
/simulations/new            → Create simulation
/simulations/:id/edit       → Edit simulation
/system                     → System configurations
/access-denied              → 403 page
```

**Lazy loading:** Each section (Management, Blacklists, Planning, Testing, System) loads as a separate Angular module for performance.

---

## 5. Feature Specification — Per Domain

### 5.1 Dashboard

**Route:** `/dashboard`

**Current behavior:** KPI cards (Users, Devices, Alerts, Phishing Sites), system status indicator, threat intelligence feed, quick action buttons.

**Angular implementation:**
- 4 KPI summary cards with counts fetched via dedicated API endpoints.
- System status widget (health check via `GET /api/system/health`).
- Threat intelligence feed (latest phishing sites, recent alerts).
- Quick action buttons: Add User, Add Device, View Alerts.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/dashboard/summary` | Returns KPI counts (users, devices, alerts, phishing sites) in a single call |

---

### 5.2 Users

**Route:** `/users`, `/users/:key`, `/users/:key/risk`

**Current behavior:**
- **Index** — list users with device count, search/filter, create user modal, delete action.
- **Details** — user profile, their devices, device registration form, recent alerts (7 days).
- **Risk Detail** — latest risk score with axis/dimension breakdown, signals, explanation, consent map.

**Angular implementation:**
- Users table with server-side paging, search, sort.
- Create user dialog (full admin fields: name, address, city, state, zip, country, locale, timezone).
- User detail page with tabs: Profile | Devices | Alerts | Risk Score.
- Inline device registration form on the Devices tab.

**Existing API coverage:**
| Operation | Existing Endpoint | Status |
|---|---|---|
| Get all users | `GET /api/users` | Exists — needs paging support |
| Get user by key | `GET /api/users/{keyType}/{keyValue}` | Exists |
| Get user details | `GET /api/users/{keyType}/{keyValue}/details` | Exists |
| Create user | `POST /api/users` | Exists — needs extended fields (address, city, etc.) |
| Update user | `PUT /api/users/{keyType}/{keyValue}` | Exists |
| Delete user | `DELETE /api/users/{keyType}/{keyValue}` | Exists |

**New/modified API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/users` | **Modify**: add paging, search, sort, include device counts |
| `POST` | `/api/users` | **Modify**: extend `CreateUserRequest` with Address, City, State, Zip, Country, Locale, Timezone |
| `GET` | `/api/users/{keyType}/{keyValue}/risk-score` | **New**: get latest user risk score |
| `GET` | `/api/users/{keyType}/{keyValue}/alerts` | **New**: get recent alerts for user (with paging) |
| `GET` | `/api/users/{keyType}/{keyValue}/devices` | **New**: get user's devices (currently embedded in details) |

---

### 5.3 Devices

**Route:** `/devices`, `/devices/:key`

**Current behavior:**
- **Index** — list all devices with UID, type, OS, model, owner, monitoring status, search/filter.
- **Details** — device info, owner, 7-day alert history.

**Existing API coverage:**
| Operation | Existing Endpoint | Status |
|---|---|---|
| Create device | `POST /api/userdevices` | Exists |
| Update device | `PUT /api/userdevices/{keyType}/{keyValue}` | Exists |
| Delete device | `DELETE /api/userdevices/{keyType}/{keyValue}` | Exists |

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/devices` | **New**: list all devices with paging, search, sort |
| `GET` | `/api/devices/{keyType}/{keyValue}` | **New**: get device by key |
| `GET` | `/api/devices/uid/{uid}` | **New**: get device by UID |
| `GET` | `/api/devices/{keyType}/{keyValue}/alerts` | **New**: get alerts for device (with paging) |

---

### 5.4 Device Alerts

**Route:** `/alerts`, `/alerts/:key`

**Current behavior:**
- **Index** — list recent alerts, time-range filter (24/72/168 hours), search, severity badges. SignalR-driven real-time badge for new alert count.
- **Details** — full alert context with device/user info and linked analysis result.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/alerts` | **New**: list alerts with paging, search, sort, time-range filter |
| `GET` | `/api/alerts/{keyType}/{keyValue}` | **New**: get alert by key with device/user info |
| `GET` | `/api/alerts/{keyType}/{keyValue}/analysis` | **New**: get analysis result linked to alert |

---

### 5.5 Analysis Results

**Route:** `/analysis`

**Current behavior:** List analysis results with time filter, search. Each result shows alert context, user/device info, severity classification.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/analysis-results` | **New**: list with paging, search, sort, time-range filter |
| `GET` | `/api/analysis-results/{keyType}/{keyValue}` | **New**: get by key with related alert/device/user info |

---

### 5.6 Known Phishing Websites

**Route:** `/blacklists/phishing`

**Current behavior:** Paginated list (50/page), search.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/blacklists/phishing-websites` | **New**: list with server-side paging, search |

---

### 5.7 Blacklisted Phone Numbers

**Route:** `/blacklists/phones`

**Current behavior:** Paginated list (50/page), search.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/blacklists/phone-numbers` | **New**: list with server-side paging, search |

---

### 5.8 Bank Websites

**Route:** `/blacklists/banks`

**Current behavior:** Paginated list, search, filter by IsActive.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/blacklists/bank-websites` | **New**: list with paging, search, IsActive filter |

---

### 5.9 Website Categories

**Route:** `/blacklists/categories`, `/blacklists/categories/new`, `/blacklists/categories/:name/edit`

**Current behavior:** List all categories (hierarchical parent-child), create/edit with parent category and source attribution.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/blacklists/website-categories` | **New**: list with paging, search |
| `GET` | `/api/blacklists/website-categories/parents` | **New**: list parent categories (for dropdowns) |
| `GET` | `/api/blacklists/website-categories/{name}` | **New**: get by name |
| `POST` | `/api/blacklists/website-categories` | **New**: create category |
| `PUT` | `/api/blacklists/website-categories/{name}` | **New**: update category |

---

### 5.10 Tracked Domains

**Route:** `/blacklists/domains`

**Current behavior:** List tracked domains with pagination (50/page), add/edit/delete, category-based filtering, track modes (None/Surf/Click), notify user/all users actions, reason logging.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/blacklists/tracked-domains` | **New**: list with paging, search, category filter |
| `POST` | `/api/blacklists/tracked-domains` | **New**: add tracked domain |
| `PUT` | `/api/blacklists/tracked-domains/{id}` | **New**: update tracked domain |
| `DELETE` | `/api/blacklists/tracked-domains/{id}` | **New**: delete tracked domain |
| `POST` | `/api/blacklists/tracked-domains/{id}/notify-user` | **New**: send notification to assigned user |
| `POST` | `/api/blacklists/tracked-domains/{id}/notify-all` | **New**: send notification to all users |

---

### 5.11 Roadmaps

**Route:** `/roadmaps`

**Decision:** The roadmap SPA editor remains as a standalone static HTML file (not ported to Angular). The Angular admin only provides the list/create/archive management view.

**Current behavior:**
- **Index** — list roadmaps, archive filter, create modal, archive action.
- **Edit** — standalone SPA-based visual roadmap editor (stays as-is).

**Angular implementation:**
- List view with create/archive actions.
- "Edit" link opens the existing standalone roadmap editor in a new tab.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/roadmaps` | **New**: list with paging, archive filter |
| `POST` | `/api/roadmaps` | **New**: create roadmap |
| `POST` | `/api/roadmaps/{id}/archive` | **New**: archive roadmap |

---

### 5.12 Simulations

**Route:** `/simulations`, `/simulations/new`, `/simulations/:id/edit`

**Current behavior:**
- **Index** — list simulations, delete, run actions, shows execution status.
- **Create/Edit** — name, description, JSON-based step editor with multiple step types.

**Existing partial API:**
- `GET /api/simulations/users` — user autocomplete for simulation assignment.
- `GET /api/simulations/devices` — device autocomplete.
- `GET /api/simulations/users/{userKeyField}/devices` — devices for user.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `GET` | `/api/simulations` | **New**: list simulations with paging |
| `GET` | `/api/simulations/{id}` | **New**: get simulation details (including steps) |
| `POST` | `/api/simulations` | **New**: create simulation |
| `PUT` | `/api/simulations/{id}` | **New**: update simulation |
| `DELETE` | `/api/simulations/{id}` | **New**: delete simulation |
| `POST` | `/api/simulations/{id}/run` | **New**: execute simulation |

---

### 5.13 System Configurations

**Route:** `/system`

**Current behavior:** System-wide settings display, "Re-Initialize AS View" maintenance action.

**Existing API:**
- `GET /api/system/version` — system version info (Admin role required).
- `GET /api/system/health` — health check.

**New API needed:**
| Method | Route | Description |
|---|---|---|
| `POST` | `/api/system/reinitialize-asview` | **New**: trigger ASView re-initialization |

---

### 5.14 Downloads Page

**Route:** `/downloads`

**Current behavior:** Static page for downloading client installers/documentation.

**Angular implementation:** Static page, no API needed. Content can be configured via a JSON manifest or hardcoded initially.

---

## 6. API Summary — New Endpoints Required

### New REST Controllers

| Controller | Route Prefix | Endpoints |
|---|---|---|
| `DashboardApiController` | `/api/dashboard` | 1 |
| `DevicesApiController` | `/api/devices` | 4 |
| `DeviceAlertsApiController` | `/api/alerts` | 3 |
| `AnalysisResultsApiController` | `/api/analysis-results` | 2 |
| `BlacklistsApiController` | `/api/blacklists/*` | 12 |
| `RoadmapsApiController` | `/api/roadmaps` | 3 |
| `SimulationsApiController` | `/api/simulations` | 6 (extend existing) |
| `SystemApiController` | `/api/system` | 1 (extend existing) |

### Modified Existing Controllers

| Controller | Changes |
|---|---|
| `UsersController` | Add paging to `GET /api/users`, extend `CreateUserRequest`, add risk-score + alerts + devices sub-endpoints |
| `SimulationsApiController` | Add CRUD + run endpoints (currently only has user/device search) |
| `SystemController` | Add reinitialize-asview endpoint |

### Total: ~32 new API endpoints + ~3 modified endpoints

---

## 7. Shared Models (TypeScript Interfaces)

The Angular client needs TypeScript interfaces mirroring the backend DTOs. Key models:

```typescript
// Paging
interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface PagedRequest {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

// Domain models
interface User { key: string; keycloakUserId: string; firstName: string; lastName: string; role: string; dateCreated: string; }
interface UserWithDeviceCount extends User { deviceCount: number; }
interface UserDetails extends User { devices: UserDevice[]; accounts: UserAccount[]; }
interface UserDevice { key: string; deviceType: string; deviceUid: string; monitoringStatus: string; dateCreated: string; }
interface Device extends UserDevice { operatingSystem: string; model: string; ownerName: string; }
interface DeviceAlert { key: string; alertType: string; deviceName: string; userName: string; severity: string; timestamp: string; metadata: Record<string, any>; }
interface AnalysisResult { key: string; alertKey: string; severity: string; classification: string; deviceName: string; userName: string; timestamp: string; }
interface UserRiskScore { score: number; level: string; confidence: number; axisScores: AxisScores; dimensionScores: DimensionScores; contributingSignals: Signal[]; explanation: string; recommendedActions: string[]; dataSourcesActive: DataSource[]; }
interface Simulation { id: number; name: string; description: string; stepsExecuted: number; createdBy: string; steps: SimulationStep[]; }
interface Roadmap { id: number; title: string; description: string; isArchived: boolean; version: number; lastUpdated: string; data: any; }
interface TrackedDomain { id: number; domain: string; category: string; trackMode: string; assignedUser: string; reason: string; }
interface WebsiteCategory { name: string; parentCategory: string; source: string; }
interface PhishingWebsite { id: number; url: string; dateAdded: string; }
interface BlacklistedPhoneNumber { id: number; phoneNumber: string; dateAdded: string; }
interface BankWebsite { id: number; url: string; bankName: string; isActive: boolean; }

// Enums
type DeviceType = 'Unknown' | 'PersonalComputer' | 'MobilePhone' | 'Other';
type MonitoringStatus = 'Disabled' | 'Enabled';
type OperatingSystem = 'Unknown' | 'Windows' | 'Linux' | 'MacOS' | 'Android' | 'iOS';
type UserRole = 'Unknown' | 'Self' | 'Guardian' | 'Other';
type CautionLevel = 'Low' | 'Medium' | 'High';
type TrackMode = 'None' | 'Surf' | 'Click';
```

---

## 8. Backend Changes Required

### 8.1 Authentication — JWT Bearer Support

WebApi `Program.cs` must add JWT Bearer authentication alongside existing Cookie auth:

```csharp
// Add alongside existing Cookie + OIDC
.AddJwtBearer(options => {
    options.Authority = keycloakAuthority;
    options.Audience = "asps-angular-admin";  // new Keycloak client
    options.RequireHttpsMetadata = true;
    // Map admin role from realm_access
});
```

A new Keycloak client `asps-angular-admin` should be created with:
- Access Type: public (SPA, PKCE flow)
- Valid Redirect URIs: `https://localhost:4200/*`, production URL
- Web Origins: `+` (allow all from valid redirects)

### 8.2 CORS

Add CORS policy in `Program.cs`:

```csharp
builder.Services.AddCors(options => {
    options.AddPolicy("AngularAdmin", policy => {
        policy.WithOrigins("https://localhost:4200", /* production URL */)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // for SignalR
    });
});
```

### 8.3 Paged Query Support in CQRS Layer

The Business layer needs a generic `PagedQuery<T>` base class and `PagedResult<T>` response. Each list query must accept paging parameters and return paged results.

**Implementation pattern:**

```csharp
public class PagedQuery<TResult> : IQuery<PagedResult<TResult>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
```

---

## 9. Angular Project Structure

```
apps/admin/angular/
├── angular.json
├── package.json
├── tsconfig.json
├── src/
│   ├── main.ts
│   ├── index.html
│   ├── styles.scss
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   └── app/
│       ├── app.module.ts
│       ├── app-routing.module.ts
│       ├── app.component.ts
│       ├── core/
│       │   ├── auth/                    ← Keycloak integration
│       │   │   ├── auth.guard.ts
│       │   │   ├── auth.interceptor.ts
│       │   │   └── auth.service.ts
│       │   ├── services/
│       │   │   ├── signalr.service.ts
│       │   │   └── notification.service.ts
│       │   ├── interceptors/
│       │   │   └── error.interceptor.ts
│       │   └── models/                  ← shared TypeScript interfaces
│       │       ├── paging.model.ts
│       │       ├── user.model.ts
│       │       ├── device.model.ts
│       │       └── ...
│       ├── shared/
│       │   ├── components/
│       │   │   ├── sidebar/
│       │   │   ├── topbar/
│       │   │   ├── data-table/          ← reusable paged table component
│       │   │   ├── kpi-card/
│       │   │   └── confirm-dialog/
│       │   └── pipes/
│       │       └── severity-badge.pipe.ts
│       ├── features/
│       │   ├── dashboard/
│       │   │   ├── dashboard.module.ts
│       │   │   └── dashboard.component.ts
│       │   ├── users/
│       │   │   ├── users.module.ts
│       │   │   ├── users-list/
│       │   │   ├── user-details/
│       │   │   └── user-risk/
│       │   ├── devices/
│       │   ├── alerts/
│       │   ├── analysis/
│       │   ├── blacklists/
│       │   │   ├── phishing/
│       │   │   ├── phones/
│       │   │   ├── banks/
│       │   │   ├── categories/
│       │   │   └── domains/
│       │   ├── roadmaps/
│       │   ├── simulations/
│       │   └── system/
│       └── layout/
│           ├── main-layout.component.ts
│           ├── sidebar.component.ts
│           └── topbar.component.ts
```

---

## 10. Implementation Phases (High Level)

| Phase | Scope | Teams |
|---|---|---|
| **1 — Foundation** | Angular project scaffold, auth (Keycloak), layout, shared components, paged table | Frontend + Backend (JWT auth, CORS) + Architect |
| **2 — Core Management** | Dashboard, Users CRUD, Devices CRUD | Frontend + Backend (APIs) |
| **3 — Alerts & Analysis** | Device Alerts list/details, Analysis Results, SignalR integration | Frontend + Backend (APIs) |
| **4 — Blacklists** | All 5 blacklist sections (phishing, phones, banks, categories, tracked domains) | Frontend + Backend (APIs) |
| **5 — Planning & Testing** | Roadmaps (list + SPA editor), Simulations (CRUD + run) | Frontend + Backend (APIs) |
| **6 — System & Polish** | System config, Downloads, RTL/i18n, responsive polish, e2e tests | Frontend |

---

## 11. Decisions (Resolved)

| # | Question | Decision |
|---|---|---|
| 1 | State management | **Angular Signals** + service-based state. NgRx is overkill for an admin CRUD app. |
| 2 | UI component library | **Angular Material** |
| 3 | Monorepo | **`apps/admin/angular/`** in the existing repo |
| 4 | Roadmap SPA editor | **Leave as-is** — standalone static HTML file, not ported to Angular |
| 5 | Docker | **Yes** — nginx container serving the Angular SPA static files |

---

## 12. Acceptance Criteria for the Spec

- [ ] Every Razor admin feature is accounted for in the Angular spec.
- [ ] All API gaps are identified with endpoint definitions.
- [ ] Server-side paging contract is defined and consistent across all list endpoints.
- [ ] Authentication flow (Keycloak + JWT) is specified.
- [ ] Coexistence strategy (dual admin UIs) is clear.
- [ ] Implementation phases are defined with team assignments.

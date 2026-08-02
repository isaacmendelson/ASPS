# ASPS-652 Handoff — Angular Admin: Dashboard + Users Pages

**Task:** ASPS-652  
**Branch:** `asps-652-angular-admin-dashboard-users`  
**Base branch:** `asps-642-angular-admin-client`  
**Status:** In Progress — ready for QA  
**Last updated:** 2026-08-02

---

## Summary

Implemented the Dashboard and Users feature pages for the Angular admin SPA, plus the data services and dialog supporting them.

---

## Completed work

### Dashboard (`features/dashboard/`)

- `dashboard.component.ts` — replaces placeholder. Angular Signals state. Loads `GET /api/dashboard/summary` and `GET /api/alerts?pageSize=5&sortBy=createdAt&sortDirection=desc` on init.
- `dashboard.component.html` — 4 KPI cards (Total Users, Total Devices, Active Alerts 24h, Analysis Results), system status indicator with `data-testid="system-status"`, recent alerts list with empty state, accessible markup (`role`, `aria-label`, `aria-live`).
- `dashboard.component.scss` — responsive grid, alert list styles.
- `services/dashboard.service.ts` — thin wrapper over `ApiService` for the two dashboard endpoints.
- `services/dashboard.service.spec.ts` — verifies endpoint paths and query params.
- `dashboard.component.spec.ts` — 8 tests covering: renders 4 KPI cards, loading state, system status indicator, empty alert state, populated alert list, loading resets to false, error handling.

### Users List (`features/users/users-list/`)

- `users-list.component.ts` — replaces placeholder. Injects `UsersStateService`. Calls `loadPage(1)` on init. Exposes `onPageChange`, `onSortChange`, `onSearchChange`, `onRowClick` (navigates to `/users/:keyType/:keyValue`), `openAddUserDialog`.
- `users-list.component.html` — page header row with Add User button (`data-testid="add-user-btn"`), `<app-paged-table>` with Name/Email/Devices/Status/Created columns. Accessible button with `aria-label`.
- `users-list.component.spec.ts` — 6 tests: creates, renders title, renders table, renders Add User button, calls `loadPage(1)` on init, calls `setSearch` on search change.

### Users State + API Services (`features/users/services/`)

- `users-api.service.ts` — wraps `ApiService` for all user endpoints: `getAll`, `getByKey`, `create`, `update`, `delete`, `getRiskScore`, `getUserDevices`, `getUserAlerts`.
- `users-api.service.spec.ts` — 7 tests verifying every method calls the correct path.
- `users-state.service.ts` — Angular Signals state following the architecture spec pattern. Writable signals for `_users`, `_totalCount`, `_loading`, `_error`, `_saving`, `_saveError`, `_page`, `_pageSize`, `_search`, `_sortBy`, `_sortDirection`. Computed: `totalPages`, `hasData`, `isEmpty`. Public read-only projections. Actions: `loadPage`, `setSearch`, `setSort`, `createUser`.
- `users-state.service.spec.ts` — 8 tests covering: initial state, loading=true while fetching, populates on success, sets error on failure, resets page 1 on search, passes sort params, isEmpty computed, totalPages computed.

### User Detail (`features/users/user-detail/`)

- `user-detail.component.ts` — reads `keyType`/`keyValue` from `ActivatedRoute.snapshot.params`. Loads user details, devices (paged), alerts (paged), and risk score on init. Signals for each loading/data state. Profile form (reactive). `saveProfile()` calls `UsersApiService.update`.
- `user-detail.component.html` — back navigation link, progress bar while loading, `mat-tab-group` with 4 tabs: Profile (editable form), Devices (PagedTable), Alerts (PagedTable), Risk Score (overall score circle + dimension bars + axis bars with ARIA progressbar roles).
- `user-detail.component.scss` — score circle with color by risk level, dimension/axis bar chart.
- `user-detail.component.spec.ts` — 7 tests: creates, loading=true initially, loads user on init, displays user name in header, renders 4 tabs, handles load error gracefully, riskScore() populated after load.

### Create User Dialog (`features/users/dialogs/`)

- `create-user-dialog.component.ts` — standalone dialog component. Reactive form with fields: firstName (required), lastName (required), email (required + email format), phone, address, city. Submit calls `UsersStateService.createUser()`. Shows spinner while saving, surfaces `state.saveError()` inline. Accessible: `aria-required`, `autocomplete` attributes, error messages via `<mat-error>`.

### Users Routes

- `users.routes.ts` — updated to add `users/:keyType/:keyValue` route pointing to `UserDetailComponent`.

### Bug fix (pre-existing)

- `shared/components/kpi-card/kpi-card.component.html` line 7: `@if (loading && loading())` was a compile error (NG4 — always-true condition). Fixed to `@if (loading())`. This was blocking `ng build`.

---

## Changed files

| File | Change |
|---|---|
| `features/dashboard/dashboard.component.ts` | Replaced placeholder — full implementation |
| `features/dashboard/dashboard.component.html` | New |
| `features/dashboard/dashboard.component.scss` | New |
| `features/dashboard/dashboard.component.spec.ts` | New |
| `features/dashboard/services/dashboard.service.ts` | New |
| `features/dashboard/services/dashboard.service.spec.ts` | New |
| `features/users/users-list/users-list.component.ts` | Replaced placeholder — full implementation |
| `features/users/users-list/users-list.component.html` | New |
| `features/users/users-list/users-list.component.scss` | New |
| `features/users/users-list/users-list.component.spec.ts` | New |
| `features/users/user-detail/user-detail.component.ts` | New |
| `features/users/user-detail/user-detail.component.html` | New |
| `features/users/user-detail/user-detail.component.scss` | New |
| `features/users/user-detail/user-detail.component.spec.ts` | New |
| `features/users/dialogs/create-user-dialog.component.ts` | New |
| `features/users/services/users-api.service.ts` | New |
| `features/users/services/users-api.service.spec.ts` | New |
| `features/users/services/users-state.service.ts` | New |
| `features/users/services/users-state.service.spec.ts` | New |
| `features/users/users.routes.ts` | Added `:keyType/:keyValue` child route |
| `shared/components/kpi-card/kpi-card.component.html` | Bug fix — `loading && loading()` → `loading()` |

---

## Test results

```
npx ng test --watch=false --browsers=ChromeHeadless
Chrome Headless 150.0.0.0 (Windows 10): Executed 85 of 85 SUCCESS
TOTAL: 85 SUCCESS
```

(Prior to ASPS-652: 44 tests. Added: 41 tests.)

## Build

```
npx ng build --configuration=development
Application bundle generation complete.
```

No errors. Lazy chunks confirmed: `dashboard-component`, `users-list-component`, `user-detail-component`.

---

## Decisions

1. **Route shape `/users/:keyType/:keyValue`** — matches `Key { type, value }` structure. Navigation from the list is `router.navigate(['/users', user.key.type, user.key.value])`.

2. **User Detail loads all 4 tabs eagerly on init** — devices, alerts, and risk score are fetched in parallel with the profile. This avoids a loading flash when switching tabs. If performance is a concern with very large datasets, the tab loads can be deferred to `(selectedTabChange)` — tracked as a follow-up.

3. **`createUser` passes `keycloakUserId: ''`** — the backend must assign a Keycloak user ID through its provisioning flow. If the backend expects the client to supply this, the dialog will need a field or the backend integration needs clarification. Noted for QA.

4. **`fullName` column** — the Users table column definition has key `fullName` but `UserWithDeviceCount` doesn't have this field — it has `firstName` + `lastName` separately. The table will show empty for that cell until the backend is updated to include a `fullName` computed property, or the column is changed to two separate columns, or a mapped signal is introduced. This is a known gap to discuss with the backend agent.

---

## Known gaps / follow-up

- **`fullName` column in users list** — see decision 4 above. The table column `key: 'fullName'` needs either a `fullName` field from the backend DTO or a client-side mapped signal. Minor — cosmetically empty cell, no crash.
- **`keycloakUserId` in Create User dialog** — currently sends empty string. Needs backend/product decision.
- **Deferred tab loading** — all 4 tabs pre-fetch on UserDetail init. Could be lazy per tab if performance requires.
- **E2E tests** — not in scope for this task (QA agent task per architecture doc).

---

## Continuation point

Ready for:
1. QA review on branch `asps-652-angular-admin-dashboard-users`.
2. Merge into `asps-642-angular-admin-client` after QA PASS + code review.
3. Next feature: Devices list + Device Detail (Phase 2 per architecture ownership table).

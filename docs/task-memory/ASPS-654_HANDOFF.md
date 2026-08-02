# ASPS-654 — Angular Admin Frontend: Blacklists Pages

**JIRA:** ASPS-654
**Branch:** `asps-642-angular-admin-client` (worktree: `worktree-agent-ab511942bcffce9f6`)
**Status:** Implementation complete, ready for QA
**Last updated:** 2026-08-02

---

## Summary

Built all 5 blacklist section pages in the Angular admin frontend. Each section follows the identical patterns established in ASPS-652 (Dashboard + Users).

---

## Completed work

### Models updated
- `apps/admin/angular/src/app/core/models/blacklist.model.ts` — rewritten to match actual backend DTO shapes from ASPS-648. Removed placeholder fields (key, cautionLevel, dateAdded) and added real fields (id, source, dateCreated, isActive/isDeleted). Added request interfaces for create/update operations.

### New services
- `features/blacklists/services/blacklists-api.service.ts` — single API service covering all 5 blacklist sub-areas. Uses ApiService wrapper + HttpClient directly for endpoints needing extra query params (isActive for banks, category for domains).
- `features/blacklists/services/blacklists-state.service.ts` — 5 independent signal-based state services: PhishingStateService, PhonesStateService, BanksStateService, CategoriesStateService, DomainsStateService.

### Components built / updated
| Component | File | Type |
|---|---|---|
| PhishingListComponent | `phishing/phishing-list.component.ts` | Updated stub |
| PhonesListComponent | `phones/phones-list.component.ts` | Updated stub |
| BanksListComponent | `banks/banks-list.component.ts` | Updated stub (+ isActive filter) |
| CategoriesListComponent | `categories/categories-list/categories-list.component.ts` | Updated stub |
| CategoryFormComponent | `categories/category-form/category-form.component.ts` | New (create + edit) |
| DomainsListComponent | `domains/domains-list/domains-list.component.ts` | Updated stub (+ confirm actions) |
| DomainFormDialogComponent | `domains/domain-form-dialog/domain-form-dialog.component.ts` | New |

### Routes
- `blacklists.routes.ts` — added `/categories/new` and `/categories/:name/edit`

### Test files
All 20 changed files include or are paired with spec files. 181/181 tests pass.

---

## Decisions made

1. **Backend shape vs. task brief**: The task brief described models with `key`, `cautionLevel`, `dateAdded` etc. The actual backend DTOs (from ASPS-648) use `id` (int), `source`, `dateCreated`, `isActive/isDeleted`. Chose to match backend reality.

2. **WebsiteCategory get-by-name URL**: Backend uses `/api/blacklists/website-categories/by-name/{name}` (not `/{name}`) to avoid route conflicts with `parents`.

3. **Domains row actions**: Implemented as conditional buttons below the table (on row click) rather than inline actions — cleaner for accessibility and the action set size.

4. **NotifyUser button**: When no `userKey` is set on a domain, passes empty string to the backend (which returns a validation error). A future improvement would be a user-key input in the notify dialog.

---

## Test results

```
ng test --watch=false --browsers=ChromeHeadless
TOTAL: 181 SUCCESS
```

Build:
```
ng build --configuration=development
Application bundle generation complete. [7.5 seconds]
```

The `NG0205: Injector has already been destroyed` console errors are pre-existing from the user-detail component tests (ASPS-652) — none cause test failures.

---

## Changed files

```
apps/admin/angular/src/app/core/models/blacklist.model.ts
apps/admin/angular/src/app/features/blacklists/blacklists.routes.ts
apps/admin/angular/src/app/features/blacklists/banks/banks-list.component.ts
apps/admin/angular/src/app/features/blacklists/banks/banks-list.component.spec.ts
apps/admin/angular/src/app/features/blacklists/categories/categories-list/categories-list.component.ts
apps/admin/angular/src/app/features/blacklists/categories/categories-list/categories-list.component.spec.ts
apps/admin/angular/src/app/features/blacklists/categories/category-form/category-form.component.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/categories/category-form/category-form.component.spec.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/domains/domains-list/domains-list.component.ts
apps/admin/angular/src/app/features/blacklists/domains/domains-list/domains-list.component.spec.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/domains/domain-form-dialog/domain-form-dialog.component.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/domains/domain-form-dialog/domain-form-dialog.component.spec.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/phishing/phishing-list.component.ts
apps/admin/angular/src/app/features/blacklists/phishing/phishing-list.component.spec.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/phones/phones-list.component.ts
apps/admin/angular/src/app/features/blacklists/phones/phones-list.component.spec.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/services/blacklists-api.service.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/services/blacklists-api.service.spec.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/services/blacklists-state.service.ts  [NEW]
apps/admin/angular/src/app/features/blacklists/services/blacklists-state.service.spec.ts  [NEW]
```

---

## Continuation point

Implementation is complete. Next step: QA agent reviews on this worktree branch, then orchestrator merges into `asps-642-angular-admin-client` and pushes.

# ASPS-655 — Angular Admin Frontend: Simulations + Roadmaps + System + Downloads

**Status:** Implementation complete. Build PASS, 302/302 tests PASS. Awaiting QA gate.
**Branch:** `asps-642-angular-admin-client`
**JIRA status:** In Progress (awaiting QA → In Review)
**Last updated:** 2026-08-02

---

## Completed work

### 1. Models updated
- `core/models/simulation.model.ts` — aligned to backend: `keyField`, `name` (was `title`), removed `dateModified`/`status` from UpdateRequest, `creatorKeyField` added.
- `core/models/roadmap.model.ts` — full rewrite to match backend: `id` (number), `name`, `description`, `isArchived`, `createdBy`, `dateCreated`, etc. Removed stale `Key`, `Priority`, `items[]` shape.
- `core/models/system.model.ts` — rewritten to match actual API: `SystemVersion` (version, buildDate, gitCommitId, isPrerelease, isPublicRelease), `SystemHealth` (status, timestamp), `ReinitializeResult` (message).

### 2. Simulations feature
- `features/simulations/services/simulations-api.service.ts` — getAll, getByKey, create, update, delete, run
- `features/simulations/services/simulations-state.service.ts` — signals-based state
- `features/simulations/simulations-list/simulations-list.component.ts` — paged table, row selection, Edit/Run/Delete actions, Create button
- `features/simulations/simulation-form/simulation-form.component.ts` — create/edit form with dynamic FormArray step editor (add/remove steps)
- `features/simulations/simulations.routes.ts` — added `/new` and `/:keyField/edit` routes

### 3. Roadmaps feature
- `features/roadmaps/services/roadmaps-api.service.ts` — getAll (with includeArchived), create, archive
- `features/roadmaps/services/roadmaps-state.service.ts` — signals-based state with includeArchived toggle
- `features/roadmaps/dialogs/create-roadmap-dialog.component.ts` — MatDialog create form
- `features/roadmaps/roadmaps-list/roadmaps-list.component.ts` — paged table, Include Archived slide-toggle, Archive row action with confirmation

### 4. System feature
- `features/system/services/system-api.service.ts` — getVersion, getHealth, reinitializeAsView
- `features/system/system.component.ts` — version card, health card (with Refresh), Re-Initialize ASView with confirm dialog

### 5. Downloads feature
- `features/downloads/downloads.component.ts` — static download cards for Desktop Agent + Browser Extension

### 6. Test files (all pass)
- simulations-api.service.spec.ts, simulations-state.service.spec.ts, simulations-list.component.spec.ts, simulation-form.component.spec.ts
- roadmaps-api.service.spec.ts, roadmaps-list.component.spec.ts
- system-api.service.spec.ts, system.component.spec.ts
- downloads.component.spec.ts

---

## Verification

- `ng build --configuration=development` — PASS
- `ng test --watch=false --browsers=ChromeHeadless` — **302/302 PASS**

---

## Files changed

All under `apps/admin/angular/src/app/`:
- `core/models/roadmap.model.ts` (modified)
- `core/models/simulation.model.ts` (modified)
- `core/models/system.model.ts` (modified)
- `features/downloads/downloads.component.ts` (modified)
- `features/downloads/downloads.component.spec.ts` (new)
- `features/roadmaps/dialogs/create-roadmap-dialog.component.ts` (new)
- `features/roadmaps/roadmaps-list/roadmaps-list.component.ts` (modified)
- `features/roadmaps/roadmaps-list/roadmaps-list.component.spec.ts` (new)
- `features/roadmaps/services/roadmaps-api.service.ts` (new)
- `features/roadmaps/services/roadmaps-api.service.spec.ts` (new)
- `features/roadmaps/services/roadmaps-state.service.ts` (new)
- `features/simulations/services/simulations-api.service.ts` (new)
- `features/simulations/services/simulations-api.service.spec.ts` (new)
- `features/simulations/services/simulations-state.service.ts` (new)
- `features/simulations/services/simulations-state.service.spec.ts` (new)
- `features/simulations/simulation-form/simulation-form.component.ts` (new)
- `features/simulations/simulation-form/simulation-form.component.spec.ts` (new)
- `features/simulations/simulations-list/simulations-list.component.ts` (modified)
- `features/simulations/simulations-list/simulations-list.component.spec.ts` (new)
- `features/simulations/simulations.routes.ts` (modified)
- `features/system/services/system-api.service.ts` (new)
- `features/system/services/system-api.service.spec.ts` (new)
- `features/system/system.component.ts` (modified)
- `features/system/system.component.spec.ts` (new)

---

## Next step

QA agent review on branch `asps-642-angular-admin-client`. Pass → PR → code review → merge to main.

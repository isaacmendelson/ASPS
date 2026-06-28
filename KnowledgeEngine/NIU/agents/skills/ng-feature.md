---
name: ng-feature
description: Scaffold a new Angular feature (route + standalone component + service + model) using the LIMAT Angular 19 conventions. Reference patterns live at c:/Jobs/LIMAT/CustomerPortal/ClientApp/.
---

# /ng-feature

Generates a complete Angular feature in the LIMAT-style: lazy route + standalone component + injectable service + typed model interfaces. The reference codebase is [LIMAT CustomerPortal ClientApp](c:/Jobs/LIMAT/CustomerPortal/ClientApp/) running Angular 19.2 with standalone components.

## When to invoke
- User wants to add a new feature page to an Angular app (currently LIMAT; ASPS once Angular work begins).
- User says "new Angular feature", "add Angular page", "scaffold component + service".

## Project context

**Where does this skill apply?**

| Project | Path | Status |
|---|---|---|
| LIMAT CustomerPortal | [c:/Jobs/LIMAT/CustomerPortal/ClientApp/](c:/Jobs/LIMAT/CustomerPortal/ClientApp/) | Live — use the patterns here as-is |
| ASPS admin UI (planned) | TBD — Angular migration from Razor not started | When ASPS Angular work begins, re-read this skill and verify the LIMAT patterns still match (versions / Material adoption / RTL defaults may diverge) |

The patterns below come from LIMAT. Cite a LIMAT file (`limat:<path>`) any time you're matching its convention so the user can verify drift.

## LIMAT stack snapshot

- Angular 19.2 — **standalone components** (no NgModules)
- Bootstrap via [src/main.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/main.ts) → `bootstrapApplication(AppComponent, appConfig)`
- Providers in [src/app/app.config.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/app.config.ts)
- Routes in [src/app/app.routes.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/app.routes.ts) — **every feature route uses `loadComponent` (lazy)**
- Angular Material 19.2 as the design system
- ngx-translate (`@ngx-translate/core` 17) with `defaultLanguage: 'he'` — RTL default
- HttpClient with a **functional interceptor** ([core/interceptors/auth.interceptor.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/core/interceptors/auth.interceptor.ts))
- **Functional guards** ([core/guards/auth.guard.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/core/guards/auth.guard.ts))
- Observables (RxJS 7.8). Signals not yet adopted.
- Tests with Jasmine + Karma (`ng test`)

## Project layout

```
src/app/
├── app.component.ts
├── app.config.ts            # providers (router, http, interceptors, i18n, init)
├── app.routes.ts            # Routes — loadComponent per feature
├── core/
│   ├── guards/              # functional guards (CanActivateFn)
│   ├── interceptors/        # functional interceptors (HttpInterceptorFn)
│   ├── models/              # *.models.ts — plain interfaces
│   └── services/            # @Injectable({providedIn:'root'}) classes
└── features/
    ├── <feature>/           # one folder per feature
    │   ├── <feature>.component.ts
    │   └── <other>.component.ts   # dialog / sub-components live next to the host
```

Examples:
- [features/orders/orders.component.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/orders/orders.component.ts) + [features/orders/image-dialog.component.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/orders/image-dialog.component.ts)
- [features/admin/users/users.component.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/admin/users/users.component.ts) + nested `users/user-dialog.component.ts`
- [features/auth/login/login.component.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/auth/login/login.component.ts)

## Ask first

1. **Feature name + folder** — `<feature>` (kebab-case). Place under `features/` (or `features/<group>/<feature>/` for grouped features like `admin/users`).
2. **Route path** — usually `'<feature>'`. Will it be lazy-loaded (yes, default) and guarded (`authGuard`, `adminGuard`, none)?
3. **API endpoint(s)** — what does the service call? Endpoints follow the `/api/<resource>/...` prefix.
4. **Data model** — what's the shape returned by the API? Even a sketchy first cut helps; refine later.
5. **Form-driven?** If the feature has user input → reactive forms (`FormBuilder`/`FormGroup`, [login pattern](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/auth/login/login.component.ts:192-197)). If it's a read-only list/table → template-driven `[(ngModel)]` for filter inputs (orders pattern is fine).
6. **i18n strings** — what translation keys does this feature need? They go in `public/assets/i18n/he.json` (verify path under `public/` matches the running app's served assets).

## Files to create

### 1. Model — `src/app/core/models/<feature>.models.ts`

Plain TypeScript interfaces. Match the API's JSON shape exactly — including `null` unions on optional fields, as in [order.models.ts](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/core/models/order.models.ts):

```typescript
export interface <Feature>Item {
  id: number;
  name: string | null;
  // ... include every field the API can return; mark nullable explicitly
}
```

No classes, no methods. Models are data shape only. If the feature has multiple result types (e.g. Active vs History orders), put them in the same `.models.ts` file.

### 2. Service — `src/app/core/services/<feature>.service.ts`

Match the [OrdersService pattern](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/core/services/orders.service.ts) exactly:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { <Feature>Item } from '../models/<feature>.models';

@Injectable({ providedIn: 'root' })
export class <Feature>Service {
  constructor(private http: HttpClient) {}

  list(filter?: string): Observable<<Feature>Item[]> {
    let params = new HttpParams();
    if (filter) params = params.set('filter', filter);
    return this.http.get<<Feature>Item[]>('/api/<feature>', { params });
  }

  get(id: number): Observable<<Feature>Item> {
    return this.http.get<<Feature>Item>(`/api/<feature>/${id}`);
  }

  create(item: Partial<<Feature>Item>): Observable<<Feature>Item> {
    return this.http.post<<Feature>Item>('/api/<feature>', item);
  }

  update(id: number, item: Partial<<Feature>Item>): Observable<<Feature>Item> {
    return this.http.put<<Feature>Item>(`/api/<feature>/${id}`, item);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`/api/<feature>/${id}`);
  }
}
```

Conventions:
- `@Injectable({ providedIn: 'root' })` — singleton, no manual provider registration.
- Return `Observable<T>`, not `Promise<T>`. RxJS is the LIMAT default.
- `HttpParams` for query strings; never string-concatenate into the URL.
- For file/blob downloads see [downloadExport in orders.service.ts:41-51](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/core/services/orders.service.ts).
- The auth interceptor handles bearer tokens automatically — **don't** set `Authorization` headers in the service.

### 3. Component — `src/app/features/<feature>/<feature>.component.ts`

Single-file standalone component. Template + styles inline. Match the [orders.component.ts shape](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/orders/orders.component.ts):

```typescript
import { Component, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
// ... Material imports as needed
import { TranslateModule } from '@ngx-translate/core';
import { <Feature>Service } from '../../core/services/<feature>.service';
import { <Feature>Item } from '../../core/models/<feature>.models';

@Component({
  selector: 'app-<feature>',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,            // or ReactiveFormsModule for forms
    MatTableModule,         // + the Material modules actually used
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslateModule,
  ],
  template: `
    <div class="<feature>-container">
      <h1>{{ '<feature>.title' | translate }}</h1>

      @if (isLoading) {
        <mat-spinner></mat-spinner>
      } @else {
        <!-- table / form / list -->
      }
    </div>
  `,
  styles: `
    .<feature>-container { padding: 16px; }
    /* ... */
  `,
})
export class <Feature>Component implements OnInit {
  items: <Feature>Item[] = [];
  isLoading = false;
  errorMessage = '';

  constructor(private <feature>Service: <Feature>Service) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.<feature>Service.list().subscribe({
      next: (items) => {
        this.items = items;
        this.isLoading = false;
      },
      error: (err) => {
        this.errorMessage = '<feature>.loadError';
        this.isLoading = false;
        console.error(err);
      },
    });
  }
}
```

Conventions:
- **`standalone: true`** on every component.
- **Explicit `imports: [...]`** — only what's actually used in the template. Don't paste a kitchen-sink import list.
- **Angular 17+ control flow** — use `@if`, `@for`, `@switch`, not `*ngIf` / `*ngFor`. LIMAT's orders component uses `@if` ([line 40-61](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/orders/orders.component.ts)).
- **Translate every user-visible string** — `{{ 'key' | translate }}`. Hard-coded Hebrew text is OK only in early drafts; before merge, move to i18n JSON.
- **`appearance="outline"`** on Material form fields (LIMAT convention).
- **Subscribe with `next` / `error` object form** — not the legacy 3-positional-args API.

### 4. Route — modify `src/app/app.routes.ts`

Add a lazy route entry:

```typescript
{
  path: '<feature>',
  loadComponent: () =>
    import('./features/<feature>/<feature>.component').then(
      (m) => m.<Feature>Component
    ),
  canActivate: [authGuard],  // or [authGuard, adminGuard], or omit if public
},
```

Place alphabetically among the existing routes. **Don't** add a static `import` for the component — the `loadComponent` lazy pattern is the entire reason routes stay small.

### 5. i18n keys — `public/assets/i18n/he.json` (and `en.json` if it exists)

Add keys under a `<feature>` namespace. **Verify the actual filename** with `ls public/assets/i18n/` before assuming — LIMAT's served path may differ from src/.

```json
{
  "<feature>": {
    "title": "כותרת",
    "loading": "טוען...",
    "loadError": "שגיאה בטעינה"
  }
}
```

### 6. Tests — `<feature>.component.spec.ts` and `<feature>.service.spec.ts`

LIMAT runs Karma + Jasmine via `ng test`. Add specs alongside the source — verify with `ng test` before commit.

## Forms — reactive vs template-driven

LIMAT uses both:

- **Reactive forms** ([login pattern](c:/Jobs/LIMAT/CustomerPortal/ClientApp/src/app/features/auth/login/login.component.ts:192-197)) — `FormBuilder`, `FormGroup`, `Validators`. Use for forms with non-trivial validation or multi-step state.
- **Template-driven** (orders filter input) — `[(ngModel)]`. Use for simple single-field bindings.

When in doubt, reactive forms — they scale better.

## Verification

```bash
cd /c/Jobs/LIMAT/CustomerPortal/ClientApp
ng build --configuration development   # must compile clean
ng test --watch=false                  # specs must pass
ng serve                               # smoke test in browser at http://localhost:4200/<feature>
```

`ng build` failures are usually:
- Missing `imports: [...]` entry for a Material module used in the template.
- Wrong path on the model/service import.
- `HttpParams` mutated without reassignment (`params.set` returns a new instance — assign it back).

## Never

- Use NgModules. LIMAT is fully standalone.
- Use `*ngIf` / `*ngFor`. Use `@if` / `@for` (Angular 17+ control flow).
- Subscribe in templates with `| async` *while also* manually subscribing in the component — pick one per stream.
- Set `Authorization` headers in your service. The interceptor does it.
- Hard-code Hebrew strings in templates past first draft. Move to i18n JSON before commit.
- Import from `core/...` into another `core/...` file in a way that creates a cycle. If a guard needs a service that needs the guard, the design is wrong — split.
- Use `providedIn: 'any'` or component-scoped services unless there's a documented reason. The LIMAT convention is root-singletons.

## Output convention

```
Feature: <name>
Route: /<path> (lazy, guards: <list>)
Files created:
  - src/app/core/models/<feature>.models.ts
  - src/app/core/services/<feature>.service.ts
  - src/app/features/<feature>/<feature>.component.ts
  - src/app/features/<feature>/<feature>.component.spec.ts
  - src/app/core/services/<feature>.service.spec.ts
Files modified:
  - src/app/app.routes.ts (added lazy route)
  - public/assets/i18n/he.json (added <feature> namespace)
Reference patterns: limat:features/orders, limat:core/services/orders.service.ts
ng build: PASS/FAIL
ng test: PASS/FAIL (<N>/<M> specs)
```

## Notes for the future ASPS Angular migration

When ASPS starts its Razor → Angular migration:

1. **Re-read this skill** and compare each pattern against the actual ASPS Angular code at that time. The skill assumes LIMAT patterns; ASPS may diverge (different design system, signals adoption, different i18n strategy, RTL handling, etc.).
2. **Surface drift to the user** before scaffolding — don't silently apply LIMAT conventions to ASPS code that has chosen differently.
3. **Update the skill** with ASPS-specific paths once the migration's foundation is laid down. At that point, decide whether to fork into a separate `/ng-feature-asps` skill or keep one skill with conditional sections per project.

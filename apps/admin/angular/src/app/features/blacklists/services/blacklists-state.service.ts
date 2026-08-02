import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, tap, map } from 'rxjs/operators';
import { BlacklistsApiService } from './blacklists-api.service';
import {
  PhishingWebsite,
  BlacklistedPhoneNumber,
  BankWebsite,
  WebsiteCategory,
  CreateWebsiteCategoryRequest,
  UpdateWebsiteCategoryRequest,
  TrackedDomain,
  CreateTrackedDomainRequest,
  UpdateTrackedDomainRequest,
  NotifyUserRequest,
} from '@core/models/blacklist.model';
import { PagedRequest } from '@core/models/paging.model';

// ─── Phishing ─────────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class PhishingStateService {
  private api = inject(BlacklistsApiService);

  private readonly _items = signal<PhishingWebsite[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');

  readonly items = this._items.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();

  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly isEmpty = computed(
    () => !this._loading() && this._items().length === 0
  );

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) this._pageSize.set(pageSize);
    this.fetch();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetch();
  }

  private fetch(): void {
    this._loading.set(true);
    this._error.set(null);
    const req: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
    };
    this.api.getPhishingWebsites(req).subscribe({
      next: (result) => {
        this._items.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load phishing websites');
        this._loading.set(false);
      },
    });
  }
}

// ─── Phone Numbers ────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class PhonesStateService {
  private api = inject(BlacklistsApiService);

  private readonly _items = signal<BlacklistedPhoneNumber[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');

  readonly items = this._items.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();

  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly isEmpty = computed(
    () => !this._loading() && this._items().length === 0
  );

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) this._pageSize.set(pageSize);
    this.fetch();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetch();
  }

  private fetch(): void {
    this._loading.set(true);
    this._error.set(null);
    const req: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
    };
    this.api.getPhoneNumbers(req).subscribe({
      next: (result) => {
        this._items.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load phone numbers');
        this._loading.set(false);
      },
    });
  }
}

// ─── Bank Websites ────────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class BanksStateService {
  private api = inject(BlacklistsApiService);

  private readonly _items = signal<BankWebsite[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _isActive = signal<boolean | undefined>(undefined);

  readonly items = this._items.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();
  readonly isActive = this._isActive.asReadonly();

  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly isEmpty = computed(
    () => !this._loading() && this._items().length === 0
  );

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) this._pageSize.set(pageSize);
    this.fetch();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetch();
  }

  setIsActive(isActive: boolean | undefined): void {
    this._isActive.set(isActive);
    this._page.set(1);
    this.fetch();
  }

  private fetch(): void {
    this._loading.set(true);
    this._error.set(null);
    const req: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
    };
    this.api.getBankWebsites(req, this._isActive()).subscribe({
      next: (result) => {
        this._items.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load bank websites');
        this._loading.set(false);
      },
    });
  }
}

// ─── Website Categories ───────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class CategoriesStateService {
  private api = inject(BlacklistsApiService);

  private readonly _items = signal<WebsiteCategory[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _saveError = signal<string | null>(null);
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _parentCategories = signal<WebsiteCategory[]>([]);

  readonly items = this._items.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly saveError = this._saveError.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();
  readonly parentCategories = this._parentCategories.asReadonly();

  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly isEmpty = computed(
    () => !this._loading() && this._items().length === 0
  );

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) this._pageSize.set(pageSize);
    this.fetch();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetch();
  }

  loadParentCategories(): void {
    this.api.getParentCategories().subscribe({
      next: (cats) => this._parentCategories.set(cats),
      error: () => this._parentCategories.set([]),
    });
  }

  create(request: CreateWebsiteCategoryRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);
    return this.api.createWebsiteCategory(request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetch();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to create category'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  update(key: string, request: UpdateWebsiteCategoryRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);
    return this.api.updateWebsiteCategory(key, request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetch();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to update category'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  clearSaveError(): void {
    this._saveError.set(null);
  }

  private fetch(): void {
    this._loading.set(true);
    this._error.set(null);
    const req: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
    };
    this.api.getWebsiteCategories(req).subscribe({
      next: (result) => {
        this._items.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load website categories');
        this._loading.set(false);
      },
    });
  }
}

// ─── Tracked Domains ──────────────────────────────────────────────────────────

@Injectable({ providedIn: 'root' })
export class DomainsStateService {
  private api = inject(BlacklistsApiService);

  private readonly _items = signal<TrackedDomain[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _saveError = signal<string | null>(null);
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _categoryFilter = signal<string | undefined>(undefined);

  readonly items = this._items.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly saveError = this._saveError.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();
  readonly categoryFilter = this._categoryFilter.asReadonly();

  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly isEmpty = computed(
    () => !this._loading() && this._items().length === 0
  );

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) this._pageSize.set(pageSize);
    this.fetch();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetch();
  }

  setCategoryFilter(category: string | undefined): void {
    this._categoryFilter.set(category);
    this._page.set(1);
    this.fetch();
  }

  create(request: CreateTrackedDomainRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);
    return this.api.createTrackedDomain(request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetch();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to create tracked domain'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  update(id: number, request: UpdateTrackedDomainRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);
    return this.api.updateTrackedDomain(id, request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetch();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to update tracked domain'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  delete(id: number): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);
    return this.api.deleteTrackedDomain(id).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetch();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to delete tracked domain'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  notifyUser(id: number, request: NotifyUserRequest): Observable<void> {
    return this.api.notifyUser(id, request).pipe(
      map(() => void 0),
      catchError((err) => throwError(() => err))
    );
  }

  notifyAll(id: number): Observable<void> {
    return this.api.notifyAll(id).pipe(
      map(() => void 0),
      catchError((err) => throwError(() => err))
    );
  }

  clearSaveError(): void {
    this._saveError.set(null);
  }

  private fetch(): void {
    this._loading.set(true);
    this._error.set(null);
    const req: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
    };
    this.api.getTrackedDomains(req, this._categoryFilter()).subscribe({
      next: (result) => {
        this._items.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load tracked domains');
        this._loading.set(false);
      },
    });
  }
}

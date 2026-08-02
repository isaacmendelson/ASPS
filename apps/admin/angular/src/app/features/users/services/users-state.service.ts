import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, tap, map } from 'rxjs/operators';
import { UsersApiService, CreateUserResponse } from './users-api.service';
import { UserWithDeviceCount } from '@core/models/user.model';
import { CreateUserRequest } from '@core/models/user.model';
import { PagedRequest } from '@core/models/paging.model';

@Injectable({ providedIn: 'root' })
export class UsersStateService {
  private usersApi = inject(UsersApiService);

  // ── Writable signals ─────────────────────────────────────────────────────────
  private readonly _users = signal<UserWithDeviceCount[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _saveError = signal<string | null>(null);

  // ── Paging / filter state ────────────────────────────────────────────────────
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _sortBy = signal<string | null>(null);
  private readonly _sortDirection = signal<'asc' | 'desc'>('asc');

  // ── Public read-only signals ─────────────────────────────────────────────────
  readonly users = this._users.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly saveError = this._saveError.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();

  // ── Computed ─────────────────────────────────────────────────────────────────
  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly hasData = computed(() => this._users().length > 0);
  readonly isEmpty = computed(() =>
    !this._loading() && this._users().length === 0
  );

  // ── Actions ──────────────────────────────────────────────────────────────────

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) {
      this._pageSize.set(pageSize);
    }
    this.fetchUsers();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetchUsers();
  }

  setSort(sortBy: string, direction: 'asc' | 'desc'): void {
    this._sortBy.set(sortBy);
    this._sortDirection.set(direction);
    this.fetchUsers();
  }

  createUser(request: CreateUserRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);

    return this.usersApi.create(request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetchUsers();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to create user'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
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
        this._error.set(err?.message ?? 'Failed to load users');
        this._loading.set(false);
      },
    });
  }
}

import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, tap, map } from 'rxjs/operators';
import { SimulationsApiService } from './simulations-api.service';
import { Simulation, CreateSimulationRequest, UpdateSimulationRequest } from '@core/models/simulation.model';
import { PagedRequest } from '@core/models/paging.model';

@Injectable({ providedIn: 'root' })
export class SimulationsStateService {
  private simulationsApi = inject(SimulationsApiService);

  // ── Writable signals ─────────────────────────────────────────────────────────
  private readonly _simulations = signal<Simulation[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _saveError = signal<string | null>(null);

  // ── Paging state ─────────────────────────────────────────────────────────────
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _sortBy = signal<string | null>(null);
  private readonly _sortDirection = signal<'asc' | 'desc'>('asc');

  // ── Public read-only signals ──────────────────────────────────────────────────
  readonly simulations = this._simulations.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly saveError = this._saveError.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();

  // ── Computed ──────────────────────────────────────────────────────────────────
  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly hasData = computed(() => this._simulations().length > 0);
  readonly isEmpty = computed(() =>
    !this._loading() && this._simulations().length === 0
  );

  // ── Actions ──────────────────────────────────────────────────────────────────

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) {
      this._pageSize.set(pageSize);
    }
    this.fetchSimulations();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetchSimulations();
  }

  setSort(sortBy: string, direction: 'asc' | 'desc'): void {
    this._sortBy.set(sortBy);
    this._sortDirection.set(direction);
    this.fetchSimulations();
  }

  create(request: CreateSimulationRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);

    return this.simulationsApi.create(request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetchSimulations();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to create simulation'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  update(keyField: string, request: UpdateSimulationRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);

    return this.simulationsApi.update(keyField, request).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetchSimulations();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to update simulation'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  delete(keyField: string): Observable<void> {
    return this.simulationsApi.delete(keyField).pipe(
      tap(() => this.fetchSimulations()),
      map(() => void 0)
    );
  }

  run(keyField: string): Observable<{ message: string }> {
    return this.simulationsApi.run(keyField);
  }

  private fetchSimulations(): void {
    this._loading.set(true);
    this._error.set(null);

    const request: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
      sortBy: this._sortBy() || undefined,
      sortDirection: this._sortDirection(),
    };

    this.simulationsApi.getAll(request).subscribe({
      next: (result) => {
        this._simulations.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load simulations');
        this._loading.set(false);
      },
    });
  }
}

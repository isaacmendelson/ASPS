import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, tap, map } from 'rxjs/operators';
import { RoadmapsApiService } from './roadmaps-api.service';
import { Roadmap, CreateRoadmapRequest } from '@core/models/roadmap.model';
import { PagedRequest } from '@core/models/paging.model';

@Injectable({ providedIn: 'root' })
export class RoadmapsStateService {
  private roadmapsApi = inject(RoadmapsApiService);

  // ── Writable signals ─────────────────────────────────────────────────────────
  private readonly _roadmaps = signal<Roadmap[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _saveError = signal<string | null>(null);
  private readonly _includeArchived = signal(false);

  // ── Paging state ─────────────────────────────────────────────────────────────
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');

  // ── Public read-only signals ──────────────────────────────────────────────────
  readonly roadmaps = this._roadmaps.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly saving = this._saving.asReadonly();
  readonly saveError = this._saveError.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();
  readonly includeArchived = this._includeArchived.asReadonly();

  // ── Computed ──────────────────────────────────────────────────────────────────
  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly isEmpty = computed(() =>
    !this._loading() && this._roadmaps().length === 0
  );

  // ── Actions ──────────────────────────────────────────────────────────────────

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) {
      this._pageSize.set(pageSize);
    }
    this.fetchRoadmaps();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetchRoadmaps();
  }

  setIncludeArchived(value: boolean): void {
    this._includeArchived.set(value);
    this._page.set(1);
    this.fetchRoadmaps();
  }

  create(req: CreateRoadmapRequest): Observable<void> {
    this._saving.set(true);
    this._saveError.set(null);

    return this.roadmapsApi.create(req).pipe(
      tap(() => {
        this._saving.set(false);
        this.fetchRoadmaps();
      }),
      catchError((err) => {
        this._saving.set(false);
        this._saveError.set(
          err?.error?.message ?? err?.message ?? 'Failed to create roadmap'
        );
        return throwError(() => err);
      }),
      map(() => void 0)
    );
  }

  archive(id: number): Observable<void> {
    return this.roadmapsApi.archive(id).pipe(
      tap(() => this.fetchRoadmaps()),
      map(() => void 0)
    );
  }

  private fetchRoadmaps(): void {
    this._loading.set(true);
    this._error.set(null);

    const request: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
    };

    this.roadmapsApi.getAll(request, this._includeArchived()).subscribe({
      next: (result) => {
        this._roadmaps.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load roadmaps');
        this._loading.set(false);
      },
    });
  }
}

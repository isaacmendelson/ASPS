import { Injectable, inject, signal, computed } from '@angular/core';
import { AlertsApiService, AlertsPagedRequest } from './alerts-api.service';
import { AlertDto } from '@core/models/alert.model';

export type TimeRange = '1h' | '24h' | '7d' | '30d' | 'all';

const TIME_RANGE_HOURS: Record<TimeRange, number | null> = {
  '1h': 1,
  '24h': 24,
  '7d': 168,
  '30d': 720,
  'all': null,
};

@Injectable({ providedIn: 'root' })
export class AlertsStateService {
  private alertsApi = inject(AlertsApiService);

  // ── Writable signals ─────────────────────────────────────────────────────────
  private readonly _alerts = signal<AlertDto[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  // ── Paging / filter state ─────────────────────────────────────────────────
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _sortBy = signal<string | null>(null);
  private readonly _sortDirection = signal<'asc' | 'desc'>('asc');
  private readonly _timeRange = signal<TimeRange>('all');

  // ── Public read-only signals ──────────────────────────────────────────────
  readonly alerts = this._alerts.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();
  readonly timeRange = this._timeRange.asReadonly();

  // ── Computed ──────────────────────────────────────────────────────────────
  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly hasData = computed(() => this._alerts().length > 0);
  readonly isEmpty = computed(() =>
    !this._loading() && this._alerts().length === 0
  );

  // ── Actions ───────────────────────────────────────────────────────────────

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) {
      this._pageSize.set(pageSize);
    }
    this.fetchAlerts();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetchAlerts();
  }

  setSort(sortBy: string, direction: 'asc' | 'desc'): void {
    this._sortBy.set(sortBy);
    this._sortDirection.set(direction);
    this.fetchAlerts();
  }

  setTimeRange(range: TimeRange): void {
    this._timeRange.set(range);
    this._page.set(1);
    this.fetchAlerts();
  }

  private fetchAlerts(): void {
    this._loading.set(true);
    this._error.set(null);

    const hours = TIME_RANGE_HOURS[this._timeRange()];
    let from: string | undefined;
    if (hours !== null) {
      const fromDate = new Date(Date.now() - hours * 60 * 60 * 1000);
      from = fromDate.toISOString();
    }

    const request: AlertsPagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
      sortBy: this._sortBy() || undefined,
      sortDirection: this._sortDirection(),
      from,
    };

    this.alertsApi.getAll(request).subscribe({
      next: (result) => {
        this._alerts.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load alerts');
        this._loading.set(false);
      },
    });
  }
}

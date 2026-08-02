import { Injectable, inject, signal, computed } from '@angular/core';
import { DevicesApiService } from './devices-api.service';
import { DeviceDto } from '@core/models/device.model';
import { PagedRequest } from '@core/models/paging.model';

@Injectable({ providedIn: 'root' })
export class DevicesStateService {
  private devicesApi = inject(DevicesApiService);

  // ── Writable signals ─────────────────────────────────────────────────────────
  private readonly _devices = signal<DeviceDto[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  // ── Paging / filter state ─────────────────────────────────────────────────
  private readonly _page = signal(1);
  private readonly _pageSize = signal(25);
  private readonly _search = signal('');
  private readonly _sortBy = signal<string | null>(null);
  private readonly _sortDirection = signal<'asc' | 'desc'>('asc');

  // ── Public read-only signals ──────────────────────────────────────────────
  readonly devices = this._devices.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly search = this._search.asReadonly();

  // ── Computed ──────────────────────────────────────────────────────────────
  readonly totalPages = computed(() =>
    Math.ceil(this._totalCount() / this._pageSize())
  );
  readonly hasData = computed(() => this._devices().length > 0);
  readonly isEmpty = computed(() =>
    !this._loading() && this._devices().length === 0
  );

  // ── Actions ───────────────────────────────────────────────────────────────

  loadPage(page: number, pageSize?: number): void {
    this._page.set(page);
    if (pageSize != null) {
      this._pageSize.set(pageSize);
    }
    this.fetchDevices();
  }

  setSearch(search: string): void {
    this._search.set(search);
    this._page.set(1);
    this.fetchDevices();
  }

  setSort(sortBy: string, direction: 'asc' | 'desc'): void {
    this._sortBy.set(sortBy);
    this._sortDirection.set(direction);
    this.fetchDevices();
  }

  private fetchDevices(): void {
    this._loading.set(true);
    this._error.set(null);

    const request: PagedRequest = {
      page: this._page(),
      pageSize: this._pageSize(),
      search: this._search() || undefined,
      sortBy: this._sortBy() || undefined,
      sortDirection: this._sortDirection(),
    };

    this.devicesApi.getAll(request).subscribe({
      next: (result) => {
        this._devices.set(result.items);
        this._totalCount.set(result.totalCount);
        this._loading.set(false);
      },
      error: (err) => {
        this._error.set(err?.message ?? 'Failed to load devices');
        this._loading.set(false);
      },
    });
  }
}

import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { AnalysisApiService, AnalysisPagedRequest } from '../services/analysis-api.service';
import { AnalysisResult } from '@core/models/analysis.model';
import { AnalysisDetailDialogComponent } from '../analysis-detail-dialog/analysis-detail-dialog.component';

type TimeRange = '1h' | '24h' | '7d' | '30d' | 'all';

const TIME_RANGE_HOURS: Record<TimeRange, number | null> = {
  '1h': 1,
  '24h': 24,
  '7d': 168,
  '30d': 720,
  'all': null,
};

@Component({
  selector: 'app-analysis-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatSelectModule,
    PagedTableComponent,
  ],
  templateUrl: './analysis-list.component.html',
  styleUrls: ['./analysis-list.component.scss'],
})
export class AnalysisListComponent implements OnInit {
  private dialog = inject(MatDialog);
  private api = inject(AnalysisApiService);

  // ── State signals ────────────────────────────────────────────────────────────
  readonly items = signal<AnalysisResult[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(25);
  readonly timeRange = signal<TimeRange>('all');
  readonly filterFromCache = signal<boolean | undefined>(undefined);
  readonly filterHasError = signal<boolean | undefined>(undefined);
  readonly filterDeviceType = signal<string | undefined>(undefined);

  readonly deviceTypeOptions = ['PersonalComputer', 'MobilePhone', 'Other', 'Unknown'];

  readonly boolFilterOptions: { label: string; value: boolean | undefined }[] = [
    { label: 'All', value: undefined },
    { label: 'Yes', value: true },
    { label: 'No', value: false },
  ];

  readonly timeRangeOptions: { label: string; value: TimeRange }[] = [
    { label: 'Last Hour', value: '1h' },
    { label: 'Last 24 Hours', value: '24h' },
    { label: 'Last Week', value: '7d' },
    { label: 'Last Month', value: '30d' },
    { label: 'All', value: 'all' },
  ];

  columns: ColumnDef[] = [
    { key: 'timestamp', header: 'Timestamp', sortable: true, type: 'date' },
    { key: 'discriminator', header: 'Type', sortable: true, type: 'text' },
    { key: 'url', header: 'URL', sortable: false, type: 'text' },
    { key: 'score', header: 'Score', sortable: true, type: 'text' },
    { key: 'userName', header: 'User', sortable: false, type: 'text' },
    { key: 'deviceUid', header: 'Device UID', sortable: false, type: 'text' },
    { key: 'deviceType', header: 'Device Type', sortable: false, type: 'text' },
    { key: 'isFromCache', header: 'From Cache', sortable: false, type: 'text' },
    { key: 'hasError', header: 'Has Error', sortable: false, type: 'text' },
  ];

  private _sortBy: string | null = null;
  private _sortDirection: 'asc' | 'desc' = 'asc';
  private _search = '';

  ngOnInit(): void {
    this.fetchPage(1);
  }

  private fetchPage(page: number, pageSize?: number): void {
    if (pageSize != null) {
      this.pageSize.set(pageSize);
    }
    this.loading.set(true);
    this.page.set(page);

    const hours = TIME_RANGE_HOURS[this.timeRange()];
    let from: string | undefined;
    if (hours !== null) {
      from = new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
    }

    const request: AnalysisPagedRequest = {
      page,
      pageSize: this.pageSize(),
      search: this._search || undefined,
      sortBy: this._sortBy || undefined,
      sortDirection: this._sortDirection,
      from,
      isFromCache: this.filterFromCache(),
      hasError: this.filterHasError(),
      deviceType: this.filterDeviceType(),
    };

    this.api.getAll(request).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.fetchPage(event.pageIndex + 1, event.pageSize);
  }

  onSortChange(sort: Sort): void {
    if (sort.active && sort.direction) {
      this._sortBy = sort.active;
      this._sortDirection = sort.direction as 'asc' | 'desc';
    }
    this.fetchPage(1);
  }

  onSearchChange(search: string): void {
    this._search = search;
    this.fetchPage(1);
  }

  onTimeRangeChange(range: TimeRange): void {
    this.timeRange.set(range);
    this.fetchPage(1);
  }

  onFromCacheChange(value: boolean | undefined): void {
    this.filterFromCache.set(value);
    this.fetchPage(1);
  }

  onHasErrorChange(value: boolean | undefined): void {
    this.filterHasError.set(value);
    this.fetchPage(1);
  }

  onDeviceTypeChange(value: string | undefined): void {
    this.filterDeviceType.set(value);
    this.fetchPage(1);
  }

  onRowClick(result: AnalysisResult): void {
    this.dialog.open(AnalysisDetailDialogComponent, {
      data: { keyType: result.key.type, keyValue: result.key.value },
      hasBackdrop: false,
      panelClass: 'draggable-detail-panel',
      width: '700px',
      autoFocus: false,
    });
  }
}

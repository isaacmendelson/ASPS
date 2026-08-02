import { Component, OnInit, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { SeverityBadgePipe } from '@shared/pipes/severity-badge.pipe';
import { AlertsStateService, TimeRange } from '../services/alerts-state.service';
import { AlertBadgeService } from '@core/services/alert-badge.service';
import { AlertDto } from '@core/models/alert.model';
import { AlertDetailDialogComponent } from '../alert-detail-dialog/alert-detail-dialog.component';

@Component({
  selector: 'app-alerts-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    PagedTableComponent,
    SeverityBadgePipe,
  ],
  templateUrl: './alerts-list.component.html',
  styleUrls: ['./alerts-list.component.scss'],
})
export class AlertsListComponent implements OnInit {
  private dialog = inject(MatDialog);
  private alertBadge = inject(AlertBadgeService);
  readonly state = inject(AlertsStateService);

  readonly mappedAlerts = computed(() =>
    this.state.alerts().map(a => ({
      ...a,
      keyField: a.key?.value ?? '',
      analysisKeyField: a.analysisKey?.value ? `AR: ${a.analysisKey.value}` : '',
    }))
  );

  readonly timeRangeOptions: { label: string; value: TimeRange }[] = [
    { label: 'Last Hour', value: '1h' },
    { label: 'Last 24 Hours', value: '24h' },
    { label: 'Last Week', value: '7d' },
    { label: 'Last Month', value: '30d' },
    { label: 'All', value: 'all' },
  ];

  columns: ColumnDef[] = [
    { key: 'timestamp', header: 'Timestamp', sortable: true, type: 'date' },
    { key: 'alertType', header: 'Type', sortable: true, type: 'text' },
    { key: 'priority', header: 'Priority', sortable: true, type: 'text' },
    { key: 'userName', header: 'User', sortable: true, type: 'text' },
    { key: 'deviceUid', header: 'Device UID', sortable: false, type: 'text' },
    { key: 'deviceType', header: 'Device Type', sortable: true, type: 'text' },
    { key: 'operatingSystem', header: 'OS', sortable: true, type: 'text' },
    { key: 'ipAddress', header: 'IP Address', sortable: false, type: 'text' },
    { key: 'url', header: 'Details', sortable: false, type: 'text' },
    { key: 'keyField', header: 'Key', sortable: false, type: 'text' },
  ];

  ngOnInit(): void {
    this.alertBadge.reset();
    this.state.loadPage(1);
  }

  onPageChange(event: PageEvent): void {
    this.state.loadPage(event.pageIndex + 1, event.pageSize);
  }

  onSortChange(sort: Sort): void {
    if (sort.active && sort.direction) {
      this.state.setSort(sort.active, sort.direction as 'asc' | 'desc');
    }
  }

  onSearchChange(search: string): void {
    this.state.setSearch(search);
  }

  onTimeRangeChange(range: TimeRange): void {
    this.state.setTimeRange(range);
  }

  onRowClick(alert: AlertDto): void {
    this.dialog.open(AlertDetailDialogComponent, {
      data: { keyType: alert.key.type, keyValue: alert.key.value },
      hasBackdrop: false,
      panelClass: 'draggable-detail-panel',
      width: '700px',
      autoFocus: false,
    });
  }
}

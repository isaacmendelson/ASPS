import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { SeverityBadgePipe } from '@shared/pipes/severity-badge.pipe';
import { AlertsStateService, TimeRange } from '../services/alerts-state.service';
import { AlertBadgeService } from '@core/services/alert-badge.service';
import { AlertDto } from '@core/models/alert.model';

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
  private router = inject(Router);
  private alertBadge = inject(AlertBadgeService);
  readonly state = inject(AlertsStateService);

  readonly timeRangeOptions: { label: string; value: TimeRange }[] = [
    { label: '24h', value: '24h' },
    { label: '72h', value: '72h' },
    { label: '7d', value: '7d' },
    { label: 'All', value: 'all' },
  ];

  columns: ColumnDef[] = [
    { key: 'alertType', header: 'Alert Type', sortable: true, type: 'text' },
    { key: 'priority', header: 'Severity', sortable: true, type: 'text' },
    { key: 'deviceUid', header: 'Device UID', sortable: false, type: 'text' },
    { key: 'userName', header: 'User', sortable: true, type: 'text' },
    { key: 'timestamp', header: 'Timestamp', sortable: true, type: 'date' },
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
    this.router.navigate(['/alerts', alert.key.type, alert.key.value]);
  }
}

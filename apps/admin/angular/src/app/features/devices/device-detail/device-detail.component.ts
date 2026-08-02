import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { DevicesApiService } from '../services/devices-api.service';
import { DeviceDto } from '@core/models/device.model';
import { AlertDto } from '@core/models/alert.model';
import { PagedRequest } from '@core/models/paging.model';

@Component({
  selector: 'app-device-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatTabsModule,
    MatIconModule,
    MatProgressBarModule,
    PagedTableComponent,
    EmptyStateComponent,
  ],
  templateUrl: './device-detail.component.html',
  styleUrls: ['./device-detail.component.scss'],
})
export class DeviceDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(DevicesApiService);

  // ── Route params ────────────────────────────────────────────────────────────
  private keyType = '';
  private keyValue = '';

  // ── State signals ────────────────────────────────────────────────────────────
  loading = signal(true);
  device = signal<DeviceDto | null>(null);

  // ── Alerts tab state ──────────────────────────────────────────────────────────
  alertsLoading = signal(false);
  readonly alertsItems = signal<AlertDto[]>([]);
  readonly alertsTotalCount = signal(0);
  readonly alertsPage = signal(1);
  readonly alertsPageSize = signal(25);

  // ── Column defs ───────────────────────────────────────────────────────────────
  alertColumns: ColumnDef[] = [
    { key: 'alertType', header: 'Alert Type', sortable: true, type: 'text' },
    { key: 'priority', header: 'Severity', sortable: true, type: 'text' },
    { key: 'userName', header: 'User', sortable: false, type: 'text' },
    { key: 'timestamp', header: 'Timestamp', sortable: true, type: 'date' },
  ];

  ngOnInit(): void {
    const params = this.route.snapshot.params;
    this.keyType = params['keyType'] as string;
    this.keyValue = params['keyValue'] as string;
    this.loadDevice();
    this.loadAlerts();
  }

  private loadDevice(): void {
    this.loading.set(true);
    this.api.getByKey(this.keyType, this.keyValue).subscribe({
      next: (device) => {
        this.device.set(device);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadAlerts(page = 1): void {
    this.alertsLoading.set(true);
    const req: PagedRequest = { page, pageSize: this.alertsPageSize() };
    this.api.getDeviceAlerts(this.keyType, this.keyValue, req).subscribe({
      next: (result) => {
        this.alertsItems.set(result.items);
        this.alertsTotalCount.set(result.totalCount);
        this.alertsPage.set(result.page);
        this.alertsLoading.set(false);
      },
      error: () => {
        this.alertsLoading.set(false);
      },
    });
  }

  onAlertsPageChange(event: PageEvent): void {
    this.loadAlerts(event.pageIndex + 1);
  }

  onAlertSort(_sort: Sort): void {
    this.loadAlerts(1);
  }
}

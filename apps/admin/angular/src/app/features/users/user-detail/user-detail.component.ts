import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { SeverityBadgePipe } from '@shared/pipes/severity-badge.pipe';
import { UsersApiService } from '../services/users-api.service';
import { UserDetails } from '@core/models/user.model';
import { UserRiskScore } from '@core/models/risk-score.model';
import { Device } from '@core/models/device.model';
import { DeviceAlert } from '@core/models/alert.model';
import { PagedResult, PagedRequest } from '@core/models/paging.model';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatTabsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    PagedTableComponent,
    EmptyStateComponent,
    SeverityBadgePipe,
  ],
  templateUrl: './user-detail.component.html',
  styleUrls: ['./user-detail.component.scss'],
})
export class UserDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(UsersApiService);
  private fb = inject(FormBuilder);
  private notification = inject(NotificationService);

  // ── Route params ─────────────────────────────────────────────────────────────
  private keyType = '';
  private keyValue = '';

  // ── State signals ────────────────────────────────────────────────────────────
  loading = signal(true);
  savingProfile = signal(false);
  user = signal<UserDetails | null>(null);
  riskScore = signal<UserRiskScore | null>(null);

  // ── Devices tab state ─────────────────────────────────────────────────────────
  devicesLoading = signal(false);
  devicesResult = signal<PagedResult<Device> | null>(null);
  readonly devicesItems = signal<Device[]>([]);
  readonly devicesTotalCount = signal(0);
  readonly devicesPage = signal(1);
  readonly devicesPageSize = signal(25);

  // ── Alerts tab state ──────────────────────────────────────────────────────────
  alertsLoading = signal(false);
  alertsResult = signal<PagedResult<DeviceAlert> | null>(null);
  readonly alertsItems = signal<DeviceAlert[]>([]);
  readonly alertsTotalCount = signal(0);
  readonly alertsPage = signal(1);
  readonly alertsPageSize = signal(25);

  // ── Column defs ───────────────────────────────────────────────────────────────
  deviceColumns: ColumnDef[] = [
    { key: 'name', header: 'Name', sortable: true },
    { key: 'deviceType', header: 'Type', sortable: false },
    { key: 'monitoringStatus', header: 'Monitoring', sortable: false },
    { key: 'operatingSystem', header: 'OS', sortable: false },
    { key: 'dateRegistered', header: 'Registered', sortable: true, type: 'date' },
  ];

  alertColumns: ColumnDef[] = [
    { key: 'title', header: 'Title', sortable: true },
    { key: 'severity', header: 'Severity', sortable: true },
    { key: 'dateCreated', header: 'Date', sortable: true, type: 'date' },
  ];

  // ── Profile form ──────────────────────────────────────────────────────────────
  profileForm = this.fb.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    address: [''],
    city: [''],
    phoneNumber: [''],
  });

  ngOnInit(): void {
    const params = this.route.snapshot.params;
    this.keyType = params['keyType'] as string;
    this.keyValue = params['keyValue'] as string;
    this.loadUser();
    this.loadDevices();
    this.loadAlerts();
    this.loadRiskScore();
  }

  private loadUser(): void {
    this.loading.set(true);
    this.api.getByKey(this.keyType, this.keyValue).subscribe({
      next: (user) => {
        this.user.set(user);
        this.profileForm.patchValue({
          firstName: user.firstName,
          lastName: user.lastName,
          address: user.address ?? '',
          city: user.city ?? '',
          phoneNumber: user.phoneNumber ?? '',
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadDevices(page = 1): void {
    this.devicesLoading.set(true);
    const req: PagedRequest = { page, pageSize: this.devicesPageSize() };
    this.api.getUserDevices(this.keyType, this.keyValue, req).subscribe({
      next: (result) => {
        this.devicesItems.set(result.items);
        this.devicesTotalCount.set(result.totalCount);
        this.devicesPage.set(result.page);
        this.devicesLoading.set(false);
      },
      error: () => {
        this.devicesLoading.set(false);
      },
    });
  }

  private loadAlerts(page = 1): void {
    this.alertsLoading.set(true);
    const req: PagedRequest = { page, pageSize: this.alertsPageSize() };
    this.api.getUserAlerts(this.keyType, this.keyValue, req).subscribe({
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

  private loadRiskScore(): void {
    this.api.getRiskScore(this.keyType, this.keyValue).subscribe({
      next: (score) => this.riskScore.set(score),
      error: () => {
        // Non-critical — tab shows empty state
      },
    });
  }

  onDevicesPageChange(event: PageEvent): void {
    this.loadDevices(event.pageIndex + 1);
  }

  onAlertsPageChange(event: PageEvent): void {
    this.loadAlerts(event.pageIndex + 1);
  }

  onDeviceSort(_sort: Sort): void {
    this.loadDevices(1);
  }

  onAlertSort(_sort: Sort): void {
    this.loadAlerts(1);
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }
    this.savingProfile.set(true);
    const { firstName, lastName, address, city, phoneNumber } = this.profileForm.value as {
      firstName: string;
      lastName: string;
      address: string;
      city: string;
      phoneNumber: string;
    };
    this.api.update(this.keyType, this.keyValue, {
      firstName,
      lastName,
      address: address ?? '',
      city: city ?? '',
      phoneNumber: phoneNumber ?? '',
    }).subscribe({
      next: () => {
        this.savingProfile.set(false);
        this.notification.success('Profile updated', `${firstName} ${lastName}`);
        this.loadUser();
      },
      error: () => {
        this.savingProfile.set(false);
        this.notification.error('Update failed', 'Could not save profile changes.');
      },
    });
  }

  riskLevelClass(level: string | undefined): string {
    switch (level) {
      case 'Low': return 'status-pill ok';
      case 'Medium': return 'status-pill warning';
      case 'High': return 'status-pill danger';
      case 'Critical': return 'status-pill danger';
      default: return 'status-pill';
    }
  }

  scoreBar(value: number): number {
    return Math.min(100, Math.max(0, value));
  }
}

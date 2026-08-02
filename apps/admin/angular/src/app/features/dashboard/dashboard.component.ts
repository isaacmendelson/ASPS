import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { KpiCardComponent } from '@shared/components/kpi-card/kpi-card.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { DashboardService, DashboardSummary } from './services/dashboard.service';
import { DeviceAlert } from '@core/models/alert.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    KpiCardComponent,
    EmptyStateComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
})
export class DashboardComponent implements OnInit {
  private dashboardService = inject(DashboardService);

  // ── State signals ────────────────────────────────────────────────────────────
  loading = signal(true);
  private _summary = signal<DashboardSummary | null>(null);
  private _recentAlerts = signal<DeviceAlert[]>([]);

  // ── Computed values for KPI cards (as Signal<number|string>) ─────────────────
  totalUsers = computed(() => this._summary()?.totalUsers ?? 0);
  totalDevices = computed(() => this._summary()?.totalDevices ?? 0);
  activeAlerts = computed(() => this._summary()?.activeAlerts24h ?? 0);
  analysisResults = computed(() => this._summary()?.analysisResults ?? 0);
  systemStatus = computed(() => this._summary()?.systemStatus ?? 'ok');
  recentAlerts = computed(() => this._recentAlerts());
  hasAlerts = computed(() => this._recentAlerts().length > 0);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading.set(true);

    // Load summary
    this.dashboardService.getSummary().subscribe({
      next: (summary) => {
        this._summary.set(summary);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });

    // Load recent alerts independently
    this.dashboardService.getRecentAlerts().subscribe({
      next: (result) => {
        this._recentAlerts.set(result.items);
      },
      error: () => {
        // Non-critical — dashboard still functional
      },
    });
  }

  getSeverityClass(alert: DeviceAlert): string {
    const level = (alert.severity ?? '').toLowerCase();
    if (level === 'critical' || level === 'high') return 'sev-badge high';
    if (level === 'medium') return 'sev-badge medium';
    if (level === 'low') return 'sev-badge low';
    return 'sev-badge';
  }
}

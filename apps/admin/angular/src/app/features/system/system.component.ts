import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { SystemApiService } from './services/system-api.service';
import { SystemVersion, SystemHealth } from '@core/models/system.model';

@Component({
  selector: 'app-system',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">System</h1>
      <p class="page-subtitle">Version information, health status, and system actions.</p>
    </div>

    <div class="cards-grid">
      <!-- Version Card -->
      <mat-card class="info-card">
        <mat-card-header>
          <mat-icon mat-card-avatar aria-hidden="true">info</mat-icon>
          <mat-card-title>Version Information</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (versionLoading()) {
            <div class="loading-row" role="status" aria-label="Loading version info">
              <mat-spinner diameter="24"></mat-spinner>
              <span>Loading...</span>
            </div>
          } @else if (versionError()) {
            <p class="error-text" role="alert">{{ versionError() }}</p>
          } @else if (version()) {
            <dl class="info-list">
              <dt>Version</dt>
              <dd>{{ version()!.version }}</dd>
              @if (version()!.buildDate) {
                <dt>Build Date</dt>
                <dd>{{ version()!.buildDate | date: 'dd/MM/yyyy HH:mm' }}</dd>
              }
              @if (version()!.gitCommitId) {
                <dt>Commit</dt>
                <dd class="monospace">{{ version()!.gitCommitId }}</dd>
              }
              <dt>Pre-release</dt>
              <dd>{{ version()!.isPrerelease ? 'Yes' : 'No' }}</dd>
              <dt>Public Release</dt>
              <dd>{{ version()!.isPublicRelease ? 'Yes' : 'No' }}</dd>
            </dl>
          }
        </mat-card-content>
      </mat-card>

      <!-- Health Card -->
      <mat-card class="info-card">
        <mat-card-header>
          <mat-icon mat-card-avatar aria-hidden="true">favorite</mat-icon>
          <mat-card-title>Health Status</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (healthLoading()) {
            <div class="loading-row" role="status" aria-label="Loading health status">
              <mat-spinner diameter="24"></mat-spinner>
              <span>Loading...</span>
            </div>
          } @else if (healthError()) {
            <p class="error-text" role="alert">{{ healthError() }}</p>
          } @else if (health()) {
            <dl class="info-list">
              <dt>Status</dt>
              <dd>
                <span class="status-badge" [class]="'status-' + health()!.status.toLowerCase()" role="status">
                  {{ health()!.status }}
                </span>
              </dd>
              <dt>Checked At</dt>
              <dd>{{ health()!.timestamp | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
            </dl>
          }
        </mat-card-content>
        <mat-card-actions>
          <button
            mat-button
            (click)="refreshHealth()"
            [disabled]="healthLoading()"
            aria-label="Refresh health status">
            <mat-icon aria-hidden="true">refresh</mat-icon>
            Refresh
          </button>
        </mat-card-actions>
      </mat-card>

      <!-- Actions Card -->
      <mat-card class="info-card actions-card">
        <mat-card-header>
          <mat-icon mat-card-avatar aria-hidden="true">settings</mat-icon>
          <mat-card-title>System Actions</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <p class="action-description">
            Re-initialize the ASView subsystem. Use this if the real-time view becomes out of sync.
          </p>
        </mat-card-content>
        <mat-card-actions>
          <button
            mat-flat-button
            color="warn"
            data-testid="reinitialize-btn"
            (click)="onReinitialize()"
            [disabled]="reinitLoading()"
            aria-label="Re-initialize ASView">
            @if (reinitLoading()) {
              <mat-spinner diameter="18"></mat-spinner>
            } @else {
              <ng-container>
                <mat-icon aria-hidden="true">restart_alt</mat-icon>
                Re-Initialize ASView
              </ng-container>
            }
          </button>
        </mat-card-actions>
      </mat-card>
    </div>
  `,
  styles: [`
    .cards-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 16px;
    }
    .info-card mat-card-content { padding-top: 8px; }
    .loading-row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 0;
    }
    .info-list {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 6px 16px;
      margin: 0;
    }
    .info-list dt {
      font-weight: 600;
      color: var(--text-secondary, #666);
      font-size: 13px;
      white-space: nowrap;
    }
    .info-list dd { margin: 0; font-size: 14px; }
    .monospace { font-family: monospace; font-size: 12px; }
    .status-badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 12px;
      font-size: 13px;
      font-weight: 600;
    }
    .status-healthy { background: #e8f5e9; color: #2e7d32; }
    .status-degraded { background: #fff8e1; color: #f57f17; }
    .status-unhealthy { background: #ffebee; color: #c62828; }
    .error-text { color: var(--danger, #d32f2f); font-size: 14px; }
    .action-description { font-size: 14px; color: var(--text-secondary, #666); margin: 0 0 8px; }
    mat-card-actions { padding: 4px 8px 8px; }
    mat-spinner { display: inline-block; }
  `],
})
export class SystemComponent implements OnInit {
  private systemApi = inject(SystemApiService);
  private confirmDialog = inject(ConfirmDialogService);
  private notification = inject(NotificationService);

  readonly version = signal<SystemVersion | null>(null);
  readonly versionLoading = signal(false);
  readonly versionError = signal<string | null>(null);

  readonly health = signal<SystemHealth | null>(null);
  readonly healthLoading = signal(false);
  readonly healthError = signal<string | null>(null);

  readonly reinitLoading = signal(false);

  ngOnInit(): void {
    this.loadVersion();
    this.refreshHealth();
  }

  refreshHealth(): void {
    this.healthLoading.set(true);
    this.healthError.set(null);
    this.systemApi.getHealth().subscribe({
      next: (h) => {
        this.health.set(h);
        this.healthLoading.set(false);
      },
      error: (err: { error?: { message?: string }; message?: string }) => {
        this.healthError.set(
          err?.error?.message ?? err?.message ?? 'Failed to load health status.'
        );
        this.healthLoading.set(false);
      },
    });
  }

  onReinitialize(): void {
    this.confirmDialog
      .confirm({
        title: 'Re-Initialize ASView',
        message:
          'This will restart the ASView subsystem. Active sessions may be temporarily interrupted. Continue?',
        confirmText: 'Re-Initialize',
        confirmColor: 'warn',
      })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.reinitLoading.set(true);
        this.systemApi.reinitializeAsView().subscribe({
          next: (res) => {
            this.reinitLoading.set(false);
            this.notification.success(
              'ASView Re-Initialized',
              res.message ?? 'Operation completed successfully.'
            );
          },
          error: (err: { error?: { message?: string }; message?: string }) => {
            this.reinitLoading.set(false);
            this.notification.error(
              'Re-Initialize Failed',
              err?.error?.message ?? err?.message ?? 'Could not re-initialize ASView.'
            );
          },
        });
      });
  }

  private loadVersion(): void {
    this.versionLoading.set(true);
    this.versionError.set(null);
    this.systemApi.getVersion().subscribe({
      next: (v) => {
        this.version.set(v);
        this.versionLoading.set(false);
      },
      error: (err: { error?: { message?: string }; message?: string }) => {
        this.versionError.set(
          err?.error?.message ?? err?.message ?? 'Failed to load version info.'
        );
        this.versionLoading.set(false);
      },
    });
  }
}

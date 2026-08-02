import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { AlertsApiService } from '../services/alerts-api.service';
import { AlertDto } from '@core/models/alert.model';
import { AnalysisResult } from '@core/models/analysis.model';

export interface AlertDetailDialogData {
  keyType: string;
  keyValue: string;
}

@Component({
  selector: 'app-alert-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    DragDropModule,
  ],
  template: `
    <div cdkDrag cdkDragRootElement=".cdk-overlay-pane" class="detail-popup">
      <div cdkDragHandle class="popup-header">
        <h2 class="popup-title">
          @if (alert()) {
            <span>{{ alert()!.alertType }}</span>
          } @else {
            <span>Alert Details</span>
          }
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" aria-label="Close dialog">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <div class="popup-body">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate" aria-label="Loading alert details"></mat-progress-bar>
        } @else if (alert()) {
          <div class="detail-grid">

            <!-- Alert Info card -->
            <div class="info-card">
              <h3 class="section-title">Alert Details</h3>
              <dl class="detail-list">
                <dt>Type</dt><dd>{{ alert()!.alertType }}</dd>
                <dt>Priority</dt><dd>{{ alert()!.priority }}</dd>
                <dt>Status</dt><dd>{{ alert()!.status }}</dd>
                <dt>Timestamp</dt><dd>{{ alert()!.timestamp | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
                @if (alert()!.ipAddress) {
                  <dt>IP Address</dt><dd>{{ alert()!.ipAddress }}</dd>
                }
                @if (alert()!.url) {
                  <dt>URL</dt><dd class="url-text">{{ alert()!.url }}</dd>
                }
              </dl>
            </div>

            <!-- Device Info card -->
            <div class="info-card">
              <h3 class="section-title">Device</h3>
              <dl class="detail-list">
                <dt>UID</dt><dd>{{ alert()!.deviceUid }}</dd>
                <dt>Type</dt><dd>{{ alert()!.deviceType }}</dd>
                <dt>OS</dt><dd>{{ alert()!.operatingSystem }}</dd>
              </dl>
            </div>

            <!-- User Info card -->
            @if (alert()!.userName) {
              <div class="info-card">
                <h3 class="section-title">User</h3>
                <dl class="detail-list">
                  <dt>Name</dt><dd>{{ alert()!.userName }}</dd>
                  @if (alert()!.userKey) {
                    <dt>User Key</dt><dd>{{ alert()!.userKey!.type }}: {{ alert()!.userKey!.value }}</dd>
                  }
                </dl>
              </div>
            }

            <!-- Analysis section -->
            <div class="info-card full-width">
              <h3 class="section-title">Analysis</h3>
              @if (analysisLoading()) {
                <mat-progress-bar mode="indeterminate" aria-label="Loading analysis"></mat-progress-bar>
              } @else if (analysis()) {
                <dl class="detail-list">
                  <dt>Type</dt><dd>{{ analysis()!.discriminator }}</dd>
                  <dt>Has Error</dt><dd>{{ analysis()!.hasError ? 'Yes' : 'No' }}</dd>
                  <dt>From Cache</dt><dd>{{ analysis()!.isFromCache ? 'Yes' : 'No' }}</dd>
                  @if (analysis()!.errorMessage) {
                    <dt>Error</dt><dd class="error-text">{{ analysis()!.errorMessage }}</dd>
                  }
                  @if (analysis()!.url) {
                    <dt>URL</dt><dd class="url-text">{{ analysis()!.url }}</dd>
                  }
                  @if (analysis()!.score != null) {
                    <dt>Risk Score</dt><dd>{{ analysis()!.score }}</dd>
                  }
                  <dt>Timestamp</dt><dd>{{ analysis()!.timestamp | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
                </dl>
                @if (analysis()!.jsonValue) {
                  <button
                    mat-stroked-button
                    class="json-toggle-btn"
                    (click)="showJson.set(!showJson())">
                    {{ showJson() ? 'Hide JSON' : 'Show JSON' }}
                  </button>
                  @if (showJson()) {
                    <pre class="json-block">{{ prettyJson }}</pre>
                  }
                }
              } @else {
                <p class="empty-text">No analysis result linked to this alert.</p>
              }
            </div>

          </div>
        } @else {
          <p class="empty-text">Alert not found.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .detail-popup {
      min-width: 600px;
      max-width: 800px;
    }

    .popup-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      background: var(--navy, #1e293b);
      color: #fff;
      cursor: move;
      border-radius: 12px 12px 0 0;
      user-select: none;
    }

    .popup-title {
      margin: 0;
      font-size: 16px;
      font-weight: 700;
      font-family: 'Syne', sans-serif;
    }

    .popup-body {
      padding: 16px;
      max-height: 70vh;
      overflow-y: auto;
      background: #f8fafc;
      border-radius: 0 0 12px 12px;
    }

    .detail-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .info-card {
      background: #fff;
      border-radius: 8px;
      padding: 16px;
      box-shadow: 0 1px 3px rgba(0, 0, 0, .06);
    }

    .info-card.full-width {
      grid-column: 1 / -1;
    }

    .section-title {
      font-family: 'Syne', sans-serif;
      font-size: 14px;
      font-weight: 700;
      color: var(--navy, #1e293b);
      margin: 0 0 12px;
    }

    .detail-list {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 6px 12px;
      margin: 0;
    }

    .detail-list dt {
      font-size: 11px;
      font-weight: 600;
      color: #6b7280;
      text-transform: uppercase;
      letter-spacing: .04em;
      align-self: center;
    }

    .detail-list dd {
      font-size: 13px;
      color: #111827;
      margin: 0;
      word-break: break-all;
      align-self: center;
    }

    .url-text {
      word-break: break-all;
      font-size: 12px;
    }

    .error-text {
      color: #dc2626;
    }

    .empty-text {
      color: #9ca3af;
      font-size: 13px;
      margin: 8px 0;
    }

    .json-toggle-btn {
      margin-top: 12px;
      font-size: 12px;
    }

    .json-block {
      margin-top: 8px;
      padding: 12px;
      background: #1e293b;
      color: #e2e8f0;
      border-radius: 6px;
      font-size: 11px;
      line-height: 1.5;
      max-height: 300px;
      overflow: auto;
      white-space: pre-wrap;
      word-break: break-all;
    }
  `],
})
export class AlertDetailDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<AlertDetailDialogComponent>);
  private readonly data = inject<AlertDetailDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(AlertsApiService);

  alert = signal<AlertDto | null>(null);
  analysis = signal<AnalysisResult | null>(null);
  loading = signal(true);
  analysisLoading = signal(false);
  showJson = signal(false);

  get prettyJson(): string {
    const raw = this.analysis()?.jsonValue;
    if (!raw) return '';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  ngOnInit(): void {
    this.loadAlert();
  }

  private loadAlert(): void {
    this.loading.set(true);
    this.api.getByKey(this.data.keyType, this.data.keyValue).subscribe({
      next: (alert) => {
        this.alert.set(alert);
        this.loading.set(false);
        this.loadAnalysis();
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadAnalysis(): void {
    this.analysisLoading.set(true);
    this.api.getAnalysis(this.data.keyType, this.data.keyValue).subscribe({
      next: (result) => {
        this.analysis.set(result);
        this.analysisLoading.set(false);
      },
      error: () => {
        this.analysisLoading.set(false);
      },
    });
  }
}

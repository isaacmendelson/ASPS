import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { AnalysisApiService } from '../services/analysis-api.service';
import { AnalysisResult } from '@core/models/analysis.model';

export interface AnalysisDetailDialogData {
  keyType: string;
  keyValue: string;
}

@Component({
  selector: 'app-analysis-detail-dialog',
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
          @if (result()) {
            <span>{{ result()!.discriminator }}</span>
          } @else {
            <span>Analysis Details</span>
          }
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" aria-label="Close">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <div class="popup-body">
        @if (loading()) {
          <mat-progress-bar mode="indeterminate"></mat-progress-bar>
        } @else if (result()) {
          <div class="detail-grid">
            <!-- Summary -->
            <div class="info-card">
              <h3 class="section-title">Summary</h3>
              <dl class="detail-list">
                <dt>Type</dt><dd>{{ result()!.discriminator }}</dd>
                <dt>Timestamp</dt><dd>{{ result()!.timestamp | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
                <dt>From Cache</dt><dd>{{ result()!.isFromCache ? 'Yes' : 'No' }}</dd>
                <dt>Has Error</dt><dd [class.error-text]="result()!.hasError">{{ result()!.hasError ? 'Yes' : 'No' }}</dd>
                @if (result()!.errorMessage) {
                  <dt>Error</dt><dd class="error-text">{{ result()!.errorMessage }}</dd>
                }
                @if (result()!.url) {
                  <dt>URL</dt>
                  <dd>
                    <a [href]="result()!.url" target="_blank" rel="noopener noreferrer" class="url-link">
                      {{ result()!.url }}
                    </a>
                  </dd>
                }
                @if (result()!.score != null) {
                  <dt>Score</dt><dd>{{ result()!.score }}</dd>
                }
              </dl>
            </div>

            <!-- References -->
            <div class="info-card">
              <h3 class="section-title">References</h3>
              <dl class="detail-list">
                @if (result()!.userKey) {
                  <dt>User</dt><dd>{{ result()!.userKey!.type }}: {{ result()!.userKey!.value }}</dd>
                }
                @if (result()!.deviceAlertKey) {
                  <dt>Alert</dt><dd>{{ result()!.deviceAlertKey!.type }}: {{ result()!.deviceAlertKey!.value }}</dd>
                }
                @if (result()!.userName) {
                  <dt>User Name</dt><dd>{{ result()!.userName }}</dd>
                }
                @if (result()!.deviceUid) {
                  <dt>Device UID</dt><dd>{{ result()!.deviceUid }}</dd>
                }
                @if (result()!.deviceType) {
                  <dt>Device Type</dt><dd>{{ result()!.deviceType }}</dd>
                }
              </dl>
            </div>

            <!-- JSON Payload -->
            @if (prettyJson()) {
              <div class="info-card full-width">
                <h3 class="section-title">Payload</h3>
                <pre class="json-pre">{{ prettyJson() }}</pre>
              </div>
            }
          </div>
        } @else {
          <p class="empty-text">Analysis result not found.</p>
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

    .url-link {
      color: var(--navy, #1e293b);
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

    .json-pre {
      background: #f9fafb;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      padding: 12px 16px;
      font-size: 12px;
      font-family: 'Courier New', monospace;
      overflow-x: auto;
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 300px;
      overflow-y: auto;
      margin: 0;
    }
  `],
})
export class AnalysisDetailDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<AnalysisDetailDialogComponent>);
  private readonly data = inject<AnalysisDetailDialogData>(MAT_DIALOG_DATA);
  private readonly api = inject(AnalysisApiService);

  readonly result = signal<AnalysisResult | null>(null);
  readonly loading = signal(true);
  readonly prettyJson = signal<string | null>(null);

  ngOnInit(): void {
    this.api.getByKey(this.data.keyType, this.data.keyValue).subscribe({
      next: (res) => {
        this.result.set(res);
        this.loading.set(false);
        if (res.jsonValue) {
          try {
            this.prettyJson.set(JSON.stringify(JSON.parse(res.jsonValue), null, 2));
          } catch {
            this.prettyJson.set(res.jsonValue);
          }
        }
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}

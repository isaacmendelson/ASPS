import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { Roadmap } from '@core/models/roadmap.model';

export interface RoadmapDetailDialogData {
  roadmap: Roadmap;
}

@Component({
  selector: 'app-roadmap-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatIconModule,
    MatButtonModule,
    DragDropModule,
  ],
  template: `
    <div cdkDrag cdkDragRootElement=".cdk-overlay-pane" class="detail-popup">
      <div cdkDragHandle class="popup-header">
        <h2 class="popup-title">
          <span>{{ roadmap.name }}</span>
        </h2>
        <button mat-icon-button (click)="dialogRef.close()" aria-label="Close dialog">
          <mat-icon>close</mat-icon>
        </button>
      </div>

      <div class="popup-body">
        <div class="detail-grid">

          <!-- Roadmap Details card -->
          <div class="info-card">
            <h3 class="section-title">Roadmap Details</h3>
            <dl class="detail-list">
              <dt>Name</dt><dd>{{ roadmap.name }}</dd>
              @if (roadmap.description) {
                <dt>Description</dt><dd>{{ roadmap.description }}</dd>
              }
              @if (roadmap.version != null) {
                <dt>Version</dt><dd>v{{ roadmap.version }}</dd>
              }
              <dt>Archived</dt><dd>{{ roadmap.isArchived ? 'Yes' : 'No' }}</dd>
            </dl>
          </div>

          <!-- Dates & Attribution card -->
          <div class="info-card">
            <h3 class="section-title">Dates &amp; Attribution</h3>
            <dl class="detail-list">
              <dt>Created</dt><dd>{{ roadmap.dateCreated | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
              @if (roadmap.createdBy) {
                <dt>Created By</dt><dd>{{ roadmap.createdBy }}</dd>
              }
              @if (roadmap.lastUpdatedAt) {
                <dt>Last Updated</dt><dd>{{ roadmap.lastUpdatedAt | date: 'dd/MM/yyyy HH:mm:ss' }}</dd>
              }
              @if (roadmap.lastUpdatedBy) {
                <dt>Last Updated By</dt><dd>{{ roadmap.lastUpdatedBy }}</dd>
              }
            </dl>
          </div>

        </div>
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
  `],
})
export class RoadmapDetailDialogComponent {
  readonly dialogRef = inject(MatDialogRef<RoadmapDetailDialogComponent>);
  private readonly data = inject<RoadmapDetailDialogData>(MAT_DIALOG_DATA);

  readonly roadmap: Roadmap = this.data.roadmap;
}

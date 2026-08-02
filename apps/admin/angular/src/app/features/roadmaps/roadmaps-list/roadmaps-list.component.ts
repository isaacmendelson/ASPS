import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { RoadmapsStateService } from '../services/roadmaps-state.service';
import { CreateRoadmapDialogComponent } from '../dialogs/create-roadmap-dialog.component';
import { RoadmapDetailDialogComponent } from '../roadmap-detail-dialog/roadmap-detail-dialog.component';
import { Roadmap } from '@core/models/roadmap.model';

@Component({
  selector: 'app-roadmaps-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    PagedTableComponent,
  ],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">Roadmaps</h1>
          <p class="page-subtitle">Plan and track product roadmap items.</p>
        </div>
        <div class="header-actions">
          <mat-slide-toggle
            [ngModel]="state.includeArchived()"
            (ngModelChange)="onIncludeArchivedChange($event)"
            aria-label="Show archived roadmaps">
            Show Archived
          </mat-slide-toggle>
          <button
            mat-flat-button
            color="primary"
            data-testid="create-roadmap-btn"
            aria-label="Create a new roadmap"
            (click)="openCreateDialog()">
            <mat-icon aria-hidden="true">add</mat-icon>
            Create Roadmap
          </button>
        </div>
      </div>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.roadmaps"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search roadmaps..."
      (pageChange)="onPageChange($event)"
      (searchChange)="onSearchChange($event)"
      (rowClick)="onRowClick($event)">
    </app-paged-table>

    @if (selectedRoadmap()) {
      <div class="row-actions" role="toolbar" [attr.aria-label]="'Actions for ' + selectedRoadmap()?.name">
        @if (!selectedRoadmap()?.isArchived) {
          <button
            mat-stroked-button
            color="warn"
            (click)="onArchive(selectedRoadmap()!)"
            aria-label="Archive selected roadmap">
            <mat-icon aria-hidden="true">archive</mat-icon>
            Archive
          </button>
        }
      </div>
    }
  `,
  styles: [`
    .page-header-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 16px;
      flex-wrap: wrap;
    }
    .row-actions {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      flex-wrap: wrap;
    }
  `],
})
export class RoadmapsListComponent implements OnInit {
  readonly state = inject(RoadmapsStateService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notification = inject(NotificationService);

  readonly selectedRoadmap = signal<Roadmap | null>(null);

  columns: ColumnDef[] = [
    { key: 'name', header: 'Name', sortable: true, type: 'text' },
    { key: 'description', header: 'Description', sortable: false, type: 'text' },
    { key: 'createdBy', header: 'Created By', sortable: false, type: 'text' },
    { key: 'dateCreated', header: 'Date Created', sortable: true, type: 'date' },
    { key: 'isArchived', header: 'Is Archived', sortable: false, type: 'text' },
  ];

  ngOnInit(): void {
    this.state.loadPage(1);
  }

  onPageChange(event: PageEvent): void {
    this.state.loadPage(event.pageIndex + 1, event.pageSize);
  }

  onSearchChange(search: string): void {
    this.state.setSearch(search);
  }

  onIncludeArchivedChange(value: boolean): void {
    this.state.setIncludeArchived(value);
  }

  onRowClick(roadmap: Roadmap): void {
    this.selectedRoadmap.set(
      this.selectedRoadmap()?.id === roadmap.id ? null : roadmap
    );
    this.dialog.open(RoadmapDetailDialogComponent, {
      data: { roadmap },
      hasBackdrop: false,
      panelClass: 'draggable-detail-panel',
      width: '700px',
      autoFocus: false,
    });
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(CreateRoadmapDialogComponent, {
      width: '480px',
      disableClose: false,
    });
    ref.afterClosed().subscribe((created: boolean) => {
      if (created) {
        this.state.loadPage(1);
      }
    });
  }

  onArchive(roadmap: Roadmap): void {
    this.confirmDialog
      .confirm({
        title: 'Archive Roadmap',
        message: `Archive roadmap "${roadmap.name}"? It will be hidden from the default view.`,
        confirmText: 'Archive',
        confirmColor: 'warn',
      })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.state.archive(roadmap.id).subscribe({
          next: () => {
            this.notification.success(
              'Roadmap Archived',
              `"${roadmap.name}" has been archived.`
            );
            this.selectedRoadmap.set(null);
          },
          error: (err: { error?: { message?: string }; message?: string }) => {
            this.notification.error(
              'Archive Failed',
              err?.error?.message ?? err?.message ?? 'Could not archive roadmap.'
            );
          },
        });
      });
  }
}

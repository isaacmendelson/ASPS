import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { SimulationsStateService } from '../services/simulations-state.service';
import { Simulation } from '@core/models/simulation.model';

@Component({
  selector: 'app-simulations-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    PagedTableComponent,
  ],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">Simulations</h1>
          <p class="page-subtitle">Manage and run phishing simulations for users.</p>
        </div>
        <button
          mat-flat-button
          color="primary"
          data-testid="create-simulation-btn"
          aria-label="Create a new simulation"
          (click)="onCreate()">
          <mat-icon aria-hidden="true">add</mat-icon>
          Create Simulation
        </button>
      </div>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.simulations"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search simulations..."
      (pageChange)="onPageChange($event)"
      (sortChange)="onSortChange($event)"
      (searchChange)="onSearchChange($event)"
      (rowClick)="onRowClick($event)">
    </app-paged-table>

    @if (selectedSimulation()) {
      <div class="row-actions" role="toolbar" [attr.aria-label]="'Actions for ' + selectedSimulation()?.name">
        <button
          mat-stroked-button
          (click)="onEdit(selectedSimulation()!)"
          aria-label="Edit selected simulation">
          <mat-icon aria-hidden="true">edit</mat-icon>
          Edit
        </button>
        <button
          mat-stroked-button
          color="primary"
          (click)="onRun(selectedSimulation()!)"
          aria-label="Run selected simulation">
          <mat-icon aria-hidden="true">play_arrow</mat-icon>
          Run
        </button>
        <button
          mat-stroked-button
          color="warn"
          (click)="onDelete(selectedSimulation()!)"
          aria-label="Delete selected simulation">
          <mat-icon aria-hidden="true">delete</mat-icon>
          Delete
        </button>
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
    .row-actions {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      flex-wrap: wrap;
    }
  `],
})
export class SimulationsListComponent implements OnInit {
  readonly state = inject(SimulationsStateService);
  private readonly router = inject(Router);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notification = inject(NotificationService);

  readonly selectedSimulation = signal<Simulation | null>(null);

  columns: ColumnDef[] = [
    { key: 'name', header: 'Name', sortable: true, type: 'text' },
    { key: 'description', header: 'Description', sortable: false, type: 'text' },
    { key: 'status', header: 'Status', sortable: true, type: 'text' },
    { key: 'dateCreated', header: 'Date Created', sortable: true, type: 'date' },
  ];

  ngOnInit(): void {
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

  onRowClick(simulation: Simulation): void {
    this.selectedSimulation.set(
      this.selectedSimulation()?.keyField === simulation.keyField ? null : simulation
    );
  }

  onCreate(): void {
    this.router.navigate(['/simulations/new']);
  }

  onEdit(simulation: Simulation): void {
    this.router.navigate(['/simulations', simulation.keyField, 'edit']);
  }

  onRun(simulation: Simulation): void {
    this.confirmDialog
      .confirm({
        title: 'Run Simulation',
        message: `Run simulation "${simulation.name}"? This will start the simulation for assigned users.`,
        confirmText: 'Run',
        confirmColor: 'primary',
      })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.state.run(simulation.keyField).subscribe({
          next: (res) => {
            this.notification.success(
              'Simulation Started',
              res.message ?? `"${simulation.name}" is now running.`
            );
          },
          error: (err: { error?: { message?: string }; message?: string }) => {
            this.notification.error(
              'Run Failed',
              err?.error?.message ?? err?.message ?? 'Could not run simulation.'
            );
          },
        });
      });
  }

  onDelete(simulation: Simulation): void {
    this.confirmDialog
      .confirm({
        title: 'Delete Simulation',
        message: `Delete simulation "${simulation.name}"? This cannot be undone.`,
        confirmText: 'Delete',
        confirmColor: 'warn',
      })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.state.delete(simulation.keyField).subscribe({
          next: () => {
            this.notification.success(
              'Simulation Deleted',
              `"${simulation.name}" has been removed.`
            );
            this.selectedSimulation.set(null);
          },
          error: (err: { error?: { message?: string }; message?: string }) => {
            this.notification.error(
              'Delete Failed',
              err?.error?.message ?? err?.message ?? 'Could not delete simulation.'
            );
          },
        });
      });
  }
}

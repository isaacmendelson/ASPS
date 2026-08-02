import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatDialog } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageEvent } from '@angular/material/paginator';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { DomainsStateService } from '../../services/blacklists-state.service';
import { BlacklistsApiService } from '../../services/blacklists-api.service';
import { DomainFormDialogComponent } from '../domain-form-dialog/domain-form-dialog.component';
import { TrackedDomain } from '@core/models/blacklist.model';
import { NotificationService } from '@core/services/notification.service';
import { WebsiteCategory } from '@core/models/blacklist.model';

@Component({
  selector: 'app-domains-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatSelectModule,
    MatFormFieldModule,
    MatTooltipModule,
    PagedTableComponent,
  ],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">Tracked Domains</h1>
          <p class="page-subtitle">Domains actively monitored for scam and phishing activity.</p>
        </div>
        <button
          mat-flat-button
          color="primary"
          data-testid="add-domain-btn"
          aria-label="Add a new tracked domain"
          (click)="openAddDialog()">
          <mat-icon aria-hidden="true">add</mat-icon>
          Add Domain
        </button>
      </div>

      <div class="filters-row">
        <mat-form-field appearance="outline" class="category-filter">
          <mat-label>Filter by category</mat-label>
          <mat-select [ngModel]="categoryFilter()" (ngModelChange)="onCategoryFilterChange($event)">
            <mat-option [value]="null">All categories</mat-option>
            @for (cat of availableCategories(); track cat) {
              <mat-option [value]="cat">{{ cat }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.items"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search by domain or category..."
      (pageChange)="onPageChange($event)"
      (searchChange)="onSearchChange($event)">
    </app-paged-table>

    @if (selectedDomain()) {
      <div class="row-actions" role="toolbar" [attr.aria-label]="'Actions for ' + selectedDomain()?.domain">
        <button
          mat-stroked-button
          (click)="onEdit(selectedDomain()!)"
          aria-label="Edit selected domain">
          <mat-icon aria-hidden="true">edit</mat-icon>
          Edit
        </button>
        <button
          mat-stroked-button
          color="warn"
          (click)="onDelete(selectedDomain()!)"
          aria-label="Delete selected domain">
          <mat-icon aria-hidden="true">delete</mat-icon>
          Delete
        </button>
        <button
          mat-stroked-button
          (click)="onNotifyUser(selectedDomain()!)"
          aria-label="Notify assigned user of selected domain">
          <mat-icon aria-hidden="true">person</mat-icon>
          Notify User
        </button>
        <button
          mat-stroked-button
          (click)="onNotifyAll(selectedDomain()!)"
          aria-label="Notify all users of selected domain">
          <mat-icon aria-hidden="true">notifications_active</mat-icon>
          Notify All
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
    .filters-row {
      margin-top: 12px;
    }
    .category-filter {
      min-width: 240px;
    }
    .row-actions {
      display: flex;
      gap: 8px;
      margin-top: 16px;
      flex-wrap: wrap;
    }
  `],
})
export class DomainsListComponent implements OnInit {
  readonly state: DomainsStateService = inject(DomainsStateService);
  private readonly blacklistsApi: BlacklistsApiService = inject(BlacklistsApiService);
  private readonly dialog: MatDialog = inject(MatDialog);
  private readonly confirmDialog: ConfirmDialogService = inject(ConfirmDialogService);
  private readonly notification: NotificationService = inject(NotificationService);

  readonly selectedDomain = signal<TrackedDomain | null>(null);
  readonly availableCategories = signal<string[]>([]);
  readonly categoryFilter = signal<string | null>(null);

  columns: ColumnDef[] = [
    { key: 'domain', header: 'Domain', sortable: true, type: 'text' },
    { key: 'category', header: 'Category', sortable: true, type: 'text' },
    { key: 'trackMode', header: 'Track Mode', sortable: false, type: 'text' },
    { key: 'isActive', header: 'Active', sortable: false, type: 'text' },
    { key: 'dateCreated', header: 'Date Added', sortable: true, type: 'date' },
  ];

  ngOnInit(): void {
    this.state.loadPage(1);
    this.blacklistsApi.getParentCategories().subscribe({
      next: (cats: WebsiteCategory[]) =>
        this.availableCategories.set(cats.map((c) => c.name)),
      error: () => this.availableCategories.set([]),
    });
  }

  onPageChange(event: PageEvent): void {
    this.state.loadPage(event.pageIndex + 1, event.pageSize);
  }

  onSearchChange(search: string): void {
    this.state.setSearch(search);
  }

  onCategoryFilterChange(category: string | null): void {
    this.categoryFilter.set(category);
    this.state.setCategoryFilter(category ?? undefined);
  }

  onRowClick(domain: TrackedDomain): void {
    this.selectedDomain.set(
      this.selectedDomain()?.id === domain.id ? null : domain
    );
  }

  openAddDialog(): void {
    const ref = this.dialog.open(DomainFormDialogComponent, {
      width: '520px',
      data: {},
    });
    ref.afterClosed().subscribe((created: boolean) => {
      if (created) this.state.loadPage(1);
    });
  }

  onEdit(domain: TrackedDomain): void {
    const ref = this.dialog.open(DomainFormDialogComponent, {
      width: '520px',
      data: { domain },
    });
    ref.afterClosed().subscribe((saved: boolean) => {
      if (saved) this.selectedDomain.set(null);
    });
  }

  onDelete(domain: TrackedDomain): void {
    this.confirmDialog
      .confirm({
        title: 'Delete Tracked Domain',
        message: `Remove "${domain.domain}" from tracked domains? This cannot be undone.`,
        confirmText: 'Delete',
        confirmColor: 'warn',
      })
      .subscribe((confirmed: boolean) => {
        if (!confirmed) return;
        this.state.delete(domain.id).subscribe({
          next: () => {
            this.notification.success(
              'Domain Deleted',
              `"${domain.domain}" has been removed.`
            );
            this.selectedDomain.set(null);
          },
          error: (err: { error?: { message?: string }; message?: string }) => {
            this.notification.error(
              'Delete Failed',
              err?.error?.message ?? err?.message ?? 'Could not delete domain.'
            );
          },
        });
      });
  }

  onNotifyUser(domain: TrackedDomain): void {
    this.confirmDialog
      .confirm({
        title: 'Notify User',
        message: `Re-send "${domain.domain}" alert to the assigned user?`,
        confirmText: 'Notify',
        confirmColor: 'primary',
      })
      .subscribe((confirmed: boolean) => {
        if (!confirmed) return;
        this.state
          .notifyUser(domain.id, {
            userKeyField: domain.userKey ?? '',
            reason: domain.reason,
          })
          .subscribe({
            next: () =>
              this.notification.success(
                'Notification Sent',
                `User notified for "${domain.domain}".`
              ),
            error: (err: { error?: { message?: string }; message?: string }) =>
              this.notification.error(
                'Notify Failed',
                err?.error?.message ?? err?.message ?? 'Could not notify user.'
              ),
          });
      });
  }

  onNotifyAll(domain: TrackedDomain): void {
    this.confirmDialog
      .confirm({
        title: 'Notify All Users',
        message: `Send "${domain.domain}" alert to ALL users? This will affect every registered user.`,
        confirmText: 'Notify All',
        confirmColor: 'warn',
      })
      .subscribe((confirmed: boolean) => {
        if (!confirmed) return;
        this.state.notifyAll(domain.id).subscribe({
          next: () =>
            this.notification.success(
              'All Users Notified',
              `Alert sent for "${domain.domain}".`
            ),
          error: (err: { error?: { message?: string }; message?: string }) =>
            this.notification.error(
              'Notify Failed',
              err?.error?.message ??
                err?.message ??
                'Could not notify all users.'
            ),
        });
      });
  }
}

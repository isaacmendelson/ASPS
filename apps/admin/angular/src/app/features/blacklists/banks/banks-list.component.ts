import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { PageEvent } from '@angular/material/paginator';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { BanksStateService } from '../services/blacklists-state.service';

@Component({
  selector: 'app-banks-list',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonToggleModule, PagedTableComponent],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">Bank Websites</h1>
          <p class="page-subtitle">Legitimate bank domains monitored for phishing protection.</p>
        </div>
        <mat-button-toggle-group
          [ngModel]="activeFilter"
          (ngModelChange)="onActiveFilterChange($event)"
          aria-label="Filter by status">
          <mat-button-toggle [value]="undefined">All</mat-button-toggle>
          <mat-button-toggle [value]="true">Active</mat-button-toggle>
          <mat-button-toggle [value]="false">Inactive</mat-button-toggle>
        </mat-button-toggle-group>
      </div>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.items"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search by domain or bank name..."
      (pageChange)="onPageChange($event)"
      (searchChange)="onSearchChange($event)">
    </app-paged-table>
  `,
  styles: [`
    .page-header-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }
  `],
})
export class BanksListComponent implements OnInit {
  readonly state = inject(BanksStateService);

  activeFilter: boolean | undefined = undefined;

  columns: ColumnDef[] = [
    { key: 'domain', header: 'Domain', sortable: true, type: 'text' },
    { key: 'bankName', header: 'Bank Name', sortable: true, type: 'text' },
    { key: 'country', header: 'Country', sortable: false, type: 'text' },
    { key: 'dateCreated', header: 'Date Added', sortable: true, type: 'date' },
    { key: 'isActive', header: 'Active', sortable: false, type: 'text' },
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

  onActiveFilterChange(value: boolean | undefined): void {
    this.activeFilter = value;
    this.state.setIsActive(value);
  }
}

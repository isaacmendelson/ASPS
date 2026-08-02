import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { CategoriesStateService } from '../../services/blacklists-state.service';

@Component({
  selector: 'app-categories-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, PagedTableComponent],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">Website Categories</h1>
          <p class="page-subtitle">Categories used to classify tracked domains and content.</p>
        </div>
        <button
          mat-flat-button
          color="primary"
          data-testid="add-category-btn"
          aria-label="Add a new website category"
          (click)="openCreateForm()">
          <mat-icon aria-hidden="true">add</mat-icon>
          Add Category
        </button>
      </div>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.items"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search by name or source..."
      (pageChange)="onPageChange($event)"
      (searchChange)="onSearchChange($event)"
      (rowClick)="onRowClick($event)">
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
export class CategoriesListComponent implements OnInit {
  readonly state = inject(CategoriesStateService);
  private router = inject(Router);

  columns: ColumnDef[] = [
    { key: 'name', header: 'Name', sortable: true, type: 'text' },
    { key: 'parentName', header: 'Parent', sortable: false, type: 'text' },
    { key: 'source', header: 'Source', sortable: false, type: 'text' },
    { key: 'dateCreated', header: 'Date Created', sortable: true, type: 'date' },
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

  onRowClick(category: { name: string }): void {
    this.router.navigate(['/blacklists/categories', category.name, 'edit']);
  }

  openCreateForm(): void {
    this.router.navigate(['/blacklists/categories/new']);
  }
}

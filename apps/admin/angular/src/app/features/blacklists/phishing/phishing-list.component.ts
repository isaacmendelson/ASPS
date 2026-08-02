import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageEvent } from '@angular/material/paginator';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { PhishingStateService } from '../services/blacklists-state.service';

@Component({
  selector: 'app-phishing-list',
  standalone: true,
  imports: [CommonModule, PagedTableComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">Phishing Websites</h1>
      <p class="page-subtitle">Known phishing URLs detected and flagged by the system.</p>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.items"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search by URL or domain..."
      (pageChange)="onPageChange($event)"
      (searchChange)="onSearchChange($event)">
    </app-paged-table>
  `,
})
export class PhishingListComponent implements OnInit {
  readonly state = inject(PhishingStateService);

  columns: ColumnDef[] = [
    { key: 'url', header: 'URL', sortable: false, type: 'text' },
    { key: 'domain', header: 'Domain', sortable: true, type: 'text' },
    { key: 'source', header: 'Source', sortable: false, type: 'text' },
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
}

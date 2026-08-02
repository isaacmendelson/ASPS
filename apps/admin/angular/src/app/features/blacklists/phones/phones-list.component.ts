import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PageEvent } from '@angular/material/paginator';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { PhonesStateService } from '../services/blacklists-state.service';

@Component({
  selector: 'app-phones-list',
  standalone: true,
  imports: [CommonModule, PagedTableComponent],
  template: `
    <div class="page-header">
      <h1 class="page-title">Blacklisted Phone Numbers</h1>
      <p class="page-subtitle">Phone numbers flagged as scam or spam sources.</p>
    </div>

    <app-paged-table
      [columns]="columns"
      [items]="state.items"
      [totalCount]="state.totalCount"
      [loading]="state.loading"
      [page]="state.page"
      [pageSize]="state.pageSize"
      searchPlaceholder="Search by phone number..."
      (pageChange)="onPageChange($event)"
      (searchChange)="onSearchChange($event)">
    </app-paged-table>
  `,
})
export class PhonesListComponent implements OnInit {
  readonly state = inject(PhonesStateService);

  columns: ColumnDef[] = [
    { key: 'phoneNumber', header: 'Phone Number', sortable: true, type: 'text' },
    { key: 'source', header: 'Source', sortable: false, type: 'text' },
    { key: 'notes', header: 'Notes', sortable: false, type: 'text' },
    { key: 'dateCreated', header: 'Date Added', sortable: true, type: 'date' },
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

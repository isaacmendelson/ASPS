import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { AnalysisApiService } from '../services/analysis-api.service';
import { AnalysisResult } from '@core/models/analysis.model';
import { PagedRequest } from '@core/models/paging.model';

@Component({
  selector: 'app-analysis-list',
  standalone: true,
  imports: [
    CommonModule,
    PagedTableComponent,
  ],
  templateUrl: './analysis-list.component.html',
  styleUrls: ['./analysis-list.component.scss'],
})
export class AnalysisListComponent implements OnInit {
  private router = inject(Router);
  private api = inject(AnalysisApiService);

  // ── State signals ────────────────────────────────────────────────────────────
  readonly items = signal<AnalysisResult[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(25);

  columns: ColumnDef[] = [
    { key: 'discriminator', header: 'Type', sortable: true, type: 'text' },
    { key: 'timestamp', header: 'Timestamp', sortable: true, type: 'date' },
    { key: 'hasError', header: 'Has Error', sortable: false, type: 'text' },
    { key: 'url', header: 'URL', sortable: false, type: 'text' },
  ];

  private _sortBy: string | null = null;
  private _sortDirection: 'asc' | 'desc' = 'asc';
  private _search = '';

  ngOnInit(): void {
    this.fetchPage(1);
  }

  private fetchPage(page: number, pageSize?: number): void {
    if (pageSize != null) {
      this.pageSize.set(pageSize);
    }
    this.loading.set(true);
    this.page.set(page);

    const request: PagedRequest = {
      page,
      pageSize: this.pageSize(),
      search: this._search || undefined,
      sortBy: this._sortBy || undefined,
      sortDirection: this._sortDirection,
    };

    this.api.getAll(request).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.fetchPage(event.pageIndex + 1, event.pageSize);
  }

  onSortChange(sort: Sort): void {
    if (sort.active && sort.direction) {
      this._sortBy = sort.active;
      this._sortDirection = sort.direction as 'asc' | 'desc';
    }
    this.fetchPage(1);
  }

  onSearchChange(search: string): void {
    this._search = search;
    this.fetchPage(1);
  }

  onRowClick(result: AnalysisResult): void {
    this.router.navigate(['/analysis', result.key.type, result.key.value]);
  }
}

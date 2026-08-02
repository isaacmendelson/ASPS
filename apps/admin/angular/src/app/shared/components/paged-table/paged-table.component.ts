import {
  Component,
  EventEmitter,
  Input,
  OnInit,
  OnDestroy,
  Output,
  Signal,
  TemplateRef,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatIconModule } from '@angular/material/icon';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';

export interface ColumnDef {
  key: string;
  header: string;
  sortable?: boolean;
  type?: 'text' | 'date' | 'badge' | 'custom';
  templateRef?: string;
}

@Component({
  selector: 'app-paged-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatIconModule,
  ],
  templateUrl: './paged-table.component.html',
  styleUrls: ['./paged-table.component.scss'],
})
export class PagedTableComponent<T> implements OnInit, OnDestroy {
  @Input() columns: ColumnDef[] = [];
  @Input() items!: Signal<T[]>;
  @Input() totalCount!: Signal<number>;
  @Input() loading!: Signal<boolean>;
  @Input() page!: Signal<number>;
  @Input() pageSize!: Signal<number>;
  @Input() pageSizeOptions: number[] = [10, 25, 50, 100];
  @Input() searchPlaceholder = 'Search...';
  @Input() showSearch = true;
  @Input() customTemplates: Record<string, TemplateRef<unknown>> = {};

  @Output() pageChange = new EventEmitter<PageEvent>();
  @Output() sortChange = new EventEmitter<Sort>();
  @Output() searchChange = new EventEmitter<string>();
  @Output() rowClick = new EventEmitter<T>();

  searchValue = '';
  private searchSubject = new Subject<string>();
  private destroy$ = new Subject<void>();

  get displayedColumns(): string[] {
    return this.columns.map(c => c.key);
  }

  ngOnInit(): void {
    this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(value => this.searchChange.emit(value));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchInput(value: string): void {
    this.searchSubject.next(value);
  }

  onPageEvent(event: PageEvent): void {
    this.pageChange.emit(event);
  }

  onSortEvent(sort: Sort): void {
    this.sortChange.emit(sort);
  }

  onRowClick(row: T): void {
    this.rowClick.emit(row);
  }

  // Zero-based page index for mat-paginator
  get pageIndex(): number {
    return (this.page() ?? 1) - 1;
  }
}

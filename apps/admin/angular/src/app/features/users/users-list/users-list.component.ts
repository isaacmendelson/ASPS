import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { UsersStateService } from '../services/users-state.service';
import { CreateUserDialogComponent } from '../dialogs/create-user-dialog.component';
import { UserWithDeviceCount } from '@core/models/user.model';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    PagedTableComponent,
  ],
  templateUrl: './users-list.component.html',
  styleUrls: ['./users-list.component.scss'],
})
export class UsersListComponent implements OnInit {
  private router = inject(Router);
  private dialog = inject(MatDialog);
  readonly state = inject(UsersStateService);

  columns: ColumnDef[] = [
    { key: 'fullName', header: 'Name', sortable: true, type: 'text' },
    { key: 'email', header: 'Email', sortable: true, type: 'text' },
    { key: 'deviceCount', header: 'Devices', sortable: false, type: 'text' },
    { key: 'role', header: 'Status', sortable: false, type: 'text' },
    { key: 'dateCreated', header: 'Created', sortable: true, type: 'date' },
  ];

  // Mapped data with fullName computed
  get tableItems() {
    return {
      ...this.state.users,
      // Transform signal to add fullName — use a computed signal wrapper
    };
  }

  // Use a getter that maps users to include fullName for the table
  readonly mappedUsers = this.state.users;

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

  onRowClick(user: UserWithDeviceCount): void {
    this.router.navigate(['/users', user.key.type, user.key.value]);
  }

  openAddUserDialog(): void {
    const dialogRef = this.dialog.open(CreateUserDialogComponent, {
      width: '520px',
      disableClose: false,
    });
    dialogRef.afterClosed().subscribe((created) => {
      if (created) {
        this.state.loadPage(1);
      }
    });
  }
}

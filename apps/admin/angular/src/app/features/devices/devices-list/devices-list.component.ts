import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { PagedTableComponent, ColumnDef } from '@shared/components/paged-table/paged-table.component';
import { DevicesStateService } from '../services/devices-state.service';
import { DeviceDto } from '@core/models/device.model';

@Component({
  selector: 'app-devices-list',
  standalone: true,
  imports: [
    CommonModule,
    PagedTableComponent,
  ],
  templateUrl: './devices-list.component.html',
  styleUrls: ['./devices-list.component.scss'],
})
export class DevicesListComponent implements OnInit {
  private router = inject(Router);
  readonly state = inject(DevicesStateService);

  columns: ColumnDef[] = [
    { key: 'deviceUid', header: 'UID', sortable: true, type: 'text' },
    { key: 'deviceType', header: 'Device Type', sortable: true, type: 'text' },
    { key: 'operatingSystem', header: 'OS', sortable: true, type: 'text' },
    { key: 'model', header: 'Model', sortable: true, type: 'text' },
    { key: 'userName', header: 'Owner', sortable: true, type: 'text' },
    { key: 'monitoringStatus', header: 'Status', sortable: true, type: 'text' },
    { key: 'dateCreated', header: 'Added On', sortable: true, type: 'date' },
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

  onRowClick(device: DeviceDto): void {
    this.router.navigate(['/devices', device.key.type, device.key.value]);
  }
}

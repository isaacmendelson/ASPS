import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { PagedResult, PagedRequest } from '@core/models/paging.model';
import { DeviceAlert } from '@core/models/alert.model';

export interface DashboardSummary {
  totalUsers: number;
  totalDevices: number;
  activeAlerts24h: number;
  analysisResults: number;
  systemStatus: 'ok' | 'warning' | 'error';
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private api = inject(ApiService);

  getSummary(): Observable<DashboardSummary> {
    return this.api.getOne<DashboardSummary>('/api/dashboard/summary');
  }

  getRecentAlerts(): Observable<PagedResult<DeviceAlert>> {
    const request: PagedRequest = {
      page: 1,
      pageSize: 5,
      sortBy: 'createdAt',
      sortDirection: 'desc',
    };
    return this.api.getPage<DeviceAlert>('/api/alerts', request);
  }
}

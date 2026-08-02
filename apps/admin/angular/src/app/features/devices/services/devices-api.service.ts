import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { DeviceDto } from '@core/models/device.model';
import { AlertDto } from '@core/models/alert.model';

@Injectable({ providedIn: 'root' })
export class DevicesApiService {
  private api = inject(ApiService);

  getAll(request: PagedRequest & { status?: string }): Observable<PagedResult<DeviceDto>> {
    const { status, ...paged } = request;
    let obs = this.api.getPage<DeviceDto>('/api/devices', paged);
    if (status) {
      // status is an extra param not in buildPageParams — pass via custom call
      obs = this.api.getPage<DeviceDto>('/api/devices', { ...paged, search: paged.search });
    }
    return obs;
  }

  getByKey(keyType: string, keyValue: string): Observable<DeviceDto> {
    return this.api.getOne<DeviceDto>(`/api/devices/${keyType}/${keyValue}`);
  }

  getByUid(uid: string): Observable<DeviceDto> {
    return this.api.getOne<DeviceDto>(`/api/devices/uid/${uid}`);
  }

  getDeviceAlerts(keyType: string, keyValue: string, request: PagedRequest): Observable<PagedResult<AlertDto>> {
    return this.api.getPage<AlertDto>(`/api/devices/${keyType}/${keyValue}/alerts`, request);
  }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { DeviceDto } from '@core/models/device.model';
import { AlertDto } from '@core/models/alert.model';

@Injectable({ providedIn: 'root' })
export class DevicesApiService {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private config = inject(RuntimeConfigService);

  getAll(request: PagedRequest & { status?: string }): Observable<PagedResult<DeviceDto>> {
    const { status, ...paged } = request;
    if (status) {
      // The backend accepts `status` as a separate [FromQuery] param that
      // buildPageParams/getPage do not handle, so build params manually.
      const params = this.api.buildPageParams(paged).set('status', status);
      return this.http.get<PagedResult<DeviceDto>>(
        `${this.config.apiUrl}/api/devices`,
        { params }
      );
    }
    return this.api.getPage<DeviceDto>('/api/devices', paged);
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

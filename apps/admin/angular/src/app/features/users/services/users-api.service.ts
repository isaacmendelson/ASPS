import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { UserWithDeviceCount, UserDetails, CreateUserRequest, UpdateUserRequest } from '@core/models/user.model';
import { UserRiskScore } from '@core/models/risk-score.model';
import { DeviceAlert } from '@core/models/alert.model';
import { Device } from '@core/models/device.model';
import { Key } from '@core/models/key.model';

export interface CreateUserResponse {
  key: Key;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private api = inject(ApiService);

  getAll(request: PagedRequest): Observable<PagedResult<UserWithDeviceCount>> {
    return this.api.getPage<UserWithDeviceCount>('/api/users', request);
  }

  getByKey(keyType: string, keyValue: string): Observable<UserDetails> {
    return this.api.getOne<UserDetails>(`/api/users/${keyType}/${keyValue}/details`);
  }

  create(request: CreateUserRequest): Observable<CreateUserResponse> {
    return this.api.post<CreateUserRequest, CreateUserResponse>('/api/users', request);
  }

  update(keyType: string, keyValue: string, request: UpdateUserRequest): Observable<{ message: string }> {
    return this.api.put<UpdateUserRequest, { message: string }>(
      `/api/users/${keyType}/${keyValue}`,
      request
    );
  }

  delete(keyType: string, keyValue: string): Observable<{ message: string }> {
    return this.api.delete<{ message: string }>(`/api/users/${keyType}/${keyValue}`);
  }

  getRiskScore(keyType: string, keyValue: string): Observable<UserRiskScore> {
    return this.api.getOne<UserRiskScore>(`/api/users/${keyType}/${keyValue}/risk-score`);
  }

  getUserDevices(keyType: string, keyValue: string, request: PagedRequest): Observable<PagedResult<Device>> {
    return this.api.getPage<Device>(`/api/users/${keyType}/${keyValue}/devices`, request);
  }

  getUserAlerts(keyType: string, keyValue: string, request: PagedRequest): Observable<PagedResult<DeviceAlert>> {
    return this.api.getPage<DeviceAlert>(`/api/users/${keyType}/${keyValue}/alerts`, request);
  }
}

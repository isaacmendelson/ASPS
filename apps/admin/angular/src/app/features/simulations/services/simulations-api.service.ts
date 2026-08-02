import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import {
  Simulation,
  CreateSimulationRequest,
  UpdateSimulationRequest,
} from '@core/models/simulation.model';

@Injectable({ providedIn: 'root' })
export class SimulationsApiService {
  private api = inject(ApiService);

  getAll(request: PagedRequest): Observable<PagedResult<Simulation>> {
    return this.api.getPage<Simulation>('/api/simulations', request);
  }

  getByKey(keyField: string): Observable<Simulation> {
    return this.api.getOne<Simulation>(`/api/simulations/${keyField}`);
  }

  create(req: CreateSimulationRequest): Observable<{ message: string }> {
    return this.api.post<CreateSimulationRequest, { message: string }>(
      '/api/simulations',
      req
    );
  }

  update(
    keyField: string,
    req: UpdateSimulationRequest
  ): Observable<{ message: string }> {
    return this.api.put<UpdateSimulationRequest, { message: string }>(
      `/api/simulations/${keyField}`,
      req
    );
  }

  delete(keyField: string): Observable<{ message: string }> {
    return this.api.delete<{ message: string }>(`/api/simulations/${keyField}`);
  }

  run(
    keyField: string,
    requestorKeyField?: string
  ): Observable<{ message: string }> {
    return this.api.post<{ requestorKeyField?: string }, { message: string }>(
      `/api/simulations/${keyField}/run`,
      { requestorKeyField }
    );
  }
}

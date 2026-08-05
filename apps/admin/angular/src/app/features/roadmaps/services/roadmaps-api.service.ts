import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { Roadmap, CreateRoadmapRequest } from '@core/models/roadmap.model';
import { RuntimeConfigService } from '@core/services/runtime-config.service';

@Injectable({ providedIn: 'root' })
export class RoadmapsApiService {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private config = inject(RuntimeConfigService);

  getAll(
    request: PagedRequest,
    includeArchived = false
  ): Observable<PagedResult<Roadmap>> {
    let params = this.api.buildPageParams(request);
    params = params.set('includeArchived', String(includeArchived));
    return this.http.get<PagedResult<Roadmap>>(
      `${this.config.apiUrl}/api/roadmaps`,
      { params }
    );
  }

  getById(id: number): Observable<Roadmap> {
    return this.http.get<Roadmap>(
      `${this.config.apiUrl}/api/roadmaps/${id}`
    );
  }

  create(req: CreateRoadmapRequest): Observable<{ message: string }> {
    return this.api.post<CreateRoadmapRequest, { message: string }>(
      '/api/roadmaps',
      req
    );
  }

  archive(id: number): Observable<{ message: string }> {
    return this.api.post<Record<string, never>, { message: string }>(
      `/api/roadmaps/${id}/archive`,
      {}
    );
  }
}

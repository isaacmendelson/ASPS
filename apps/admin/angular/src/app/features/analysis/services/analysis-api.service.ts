import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { AnalysisResult } from '@core/models/analysis.model';

export interface AnalysisPagedRequest extends PagedRequest {
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class AnalysisApiService {
  private http = inject(HttpClient);
  private api = inject(ApiService);
  private config = inject(RuntimeConfigService);

  private get baseUrl(): string {
    return this.config.apiUrl;
  }

  getAll(request: AnalysisPagedRequest): Observable<PagedResult<AnalysisResult>> {
    let params = this.api.buildPageParams(request);
    if (request.from) {
      params = params.set('from', request.from);
    }
    if (request.to) {
      params = params.set('to', request.to);
    }
    return this.http.get<PagedResult<AnalysisResult>>(`${this.baseUrl}/api/analysis-results`, { params });
  }

  getByKey(keyType: string, keyValue: string): Observable<AnalysisResult> {
    return this.api.getOne<AnalysisResult>(`/api/analysis-results/${keyType}/${keyValue}`);
  }
}

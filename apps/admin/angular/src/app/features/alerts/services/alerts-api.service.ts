import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { AlertDto } from '@core/models/alert.model';
import { AnalysisResult } from '@core/models/analysis.model';

export interface AlertsPagedRequest extends PagedRequest {
  from?: string;
  to?: string;
  severity?: string;
}

@Injectable({ providedIn: 'root' })
export class AlertsApiService {
  private http = inject(HttpClient);
  private api = inject(ApiService);
  private config = inject(RuntimeConfigService);

  private get baseUrl(): string {
    return this.config.apiUrl;
  }

  getAll(request: AlertsPagedRequest): Observable<PagedResult<AlertDto>> {
    let params: HttpParams = this.api.buildPageParams(request);
    if (request.from) {
      params = params.set('from', request.from);
    }
    if (request.to) {
      params = params.set('to', request.to);
    }
    if (request.severity) {
      params = params.set('severity', request.severity);
    }
    return this.http.get<PagedResult<AlertDto>>(`${this.baseUrl}/api/alerts`, { params });
  }

  getByKey(keyType: string, keyValue: string): Observable<AlertDto> {
    return this.api.getOne<AlertDto>(`/api/alerts/${keyType}/${keyValue}`);
  }

  getAnalysis(keyType: string, keyValue: string): Observable<AnalysisResult | null> {
    return this.api.getOne<AnalysisResult | null>(`/api/alerts/${keyType}/${keyValue}/analysis`);
  }
}

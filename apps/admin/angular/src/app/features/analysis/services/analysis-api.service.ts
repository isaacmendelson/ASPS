import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import { AnalysisResult } from '@core/models/analysis.model';

@Injectable({ providedIn: 'root' })
export class AnalysisApiService {
  private api = inject(ApiService);

  getAll(request: PagedRequest): Observable<PagedResult<AnalysisResult>> {
    return this.api.getPage<AnalysisResult>('/api/analysis-results', request);
  }

  getByKey(keyType: string, keyValue: string): Observable<AnalysisResult> {
    return this.api.getOne<AnalysisResult>(`/api/analysis-results/${keyType}/${keyValue}`);
  }
}

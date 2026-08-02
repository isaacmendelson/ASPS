import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { PagedRequest, PagedResult } from '../models/paging.model';
import { RuntimeConfigService } from './runtime-config.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private config = inject(RuntimeConfigService);

  private get baseUrl(): string {
    return this.config.apiUrl;
  }

  getOne<T>(path: string): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`);
  }

  getPage<T>(path: string, request: PagedRequest): Observable<PagedResult<T>> {
    const params = this.buildPageParams(request);
    return this.http.get<PagedResult<T>>(`${this.baseUrl}${path}`, { params });
  }

  post<TReq, TRes>(path: string, body: TReq): Observable<TRes> {
    return this.http.post<TRes>(`${this.baseUrl}${path}`, body);
  }

  put<TReq, TRes>(path: string, body: TReq): Observable<TRes> {
    return this.http.put<TRes>(`${this.baseUrl}${path}`, body);
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`);
  }

  buildPageParams(request: PagedRequest): HttpParams {
    let params = new HttpParams()
      .set('page', request.page?.toString() ?? '1')
      .set('pageSize', request.pageSize?.toString() ?? '25');

    if (request.search) {
      params = params.set('search', request.search);
    }
    if (request.sortBy) {
      params = params.set('sortBy', request.sortBy);
    }
    if (request.sortDirection) {
      params = params.set('sortDirection', request.sortDirection);
    }

    return params;
  }
}

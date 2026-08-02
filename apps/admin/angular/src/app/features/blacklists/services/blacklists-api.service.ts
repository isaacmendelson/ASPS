import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { PagedRequest, PagedResult } from '@core/models/paging.model';
import {
  PhishingWebsite,
  BlacklistedPhoneNumber,
  BankWebsite,
  WebsiteCategory,
  CreateWebsiteCategoryRequest,
  UpdateWebsiteCategoryRequest,
  TrackedDomain,
  CreateTrackedDomainRequest,
  UpdateTrackedDomainRequest,
  NotifyUserRequest,
} from '@core/models/blacklist.model';

@Injectable({ providedIn: 'root' })
export class BlacklistsApiService {
  private api = inject(ApiService);
  private http = inject(HttpClient);
  private config = inject(RuntimeConfigService);

  private get baseUrl(): string {
    return this.config.apiUrl;
  }

  // ── Phishing Websites ──────────────────────────────────────────────────────

  getPhishingWebsites(
    request: PagedRequest
  ): Observable<PagedResult<PhishingWebsite>> {
    return this.api.getPage<PhishingWebsite>(
      '/api/blacklists/phishing-websites',
      request
    );
  }

  // ── Phone Numbers ──────────────────────────────────────────────────────────

  getPhoneNumbers(
    request: PagedRequest
  ): Observable<PagedResult<BlacklistedPhoneNumber>> {
    return this.api.getPage<BlacklistedPhoneNumber>(
      '/api/blacklists/phone-numbers',
      request
    );
  }

  // ── Bank Websites ──────────────────────────────────────────────────────────

  getBankWebsites(
    request: PagedRequest,
    isActive?: boolean
  ): Observable<PagedResult<BankWebsite>> {
    let params = this.api.buildPageParams(request);
    if (isActive !== undefined && isActive !== null) {
      params = params.set('isActive', String(isActive));
    }
    return this.http.get<PagedResult<BankWebsite>>(
      `${this.baseUrl}/api/blacklists/bank-websites`,
      { params }
    );
  }

  // ── Website Categories ─────────────────────────────────────────────────────

  getWebsiteCategories(
    request: PagedRequest
  ): Observable<PagedResult<WebsiteCategory>> {
    return this.api.getPage<WebsiteCategory>(
      '/api/blacklists/website-categories',
      request
    );
  }

  getParentCategories(): Observable<WebsiteCategory[]> {
    return this.api.getOne<WebsiteCategory[]>(
      '/api/blacklists/website-categories/parents'
    );
  }

  getWebsiteCategoryByName(name: string): Observable<WebsiteCategory> {
    return this.api.getOne<WebsiteCategory>(
      `/api/blacklists/website-categories/by-name/${encodeURIComponent(name)}`
    );
  }

  createWebsiteCategory(
    request: CreateWebsiteCategoryRequest
  ): Observable<{ message: string; categoryId: string }> {
    return this.api.post<
      CreateWebsiteCategoryRequest,
      { message: string; categoryId: string }
    >('/api/blacklists/website-categories', request);
  }

  updateWebsiteCategory(
    key: string,
    request: UpdateWebsiteCategoryRequest
  ): Observable<{ message: string }> {
    return this.api.put<UpdateWebsiteCategoryRequest, { message: string }>(
      `/api/blacklists/website-categories/${encodeURIComponent(key)}`,
      request
    );
  }

  // ── Tracked Domains ────────────────────────────────────────────────────────

  getTrackedDomains(
    request: PagedRequest,
    category?: string
  ): Observable<PagedResult<TrackedDomain>> {
    let params = this.api.buildPageParams(request);
    if (category) {
      params = params.set('category', category);
    }
    return this.http.get<PagedResult<TrackedDomain>>(
      `${this.baseUrl}/api/blacklists/tracked-domains`,
      { params }
    );
  }

  createTrackedDomain(
    request: CreateTrackedDomainRequest
  ): Observable<{ message: string; trackedDomainId: number }> {
    return this.api.post<
      CreateTrackedDomainRequest,
      { message: string; trackedDomainId: number }
    >('/api/blacklists/tracked-domains', request);
  }

  updateTrackedDomain(
    id: number,
    request: UpdateTrackedDomainRequest
  ): Observable<{ message: string; trackedDomainId: number }> {
    return this.api.put<
      UpdateTrackedDomainRequest,
      { message: string; trackedDomainId: number }
    >(`/api/blacklists/tracked-domains/${id}`, request);
  }

  deleteTrackedDomain(id: number): Observable<{ message: string }> {
    return this.api.delete<{ message: string }>(
      `/api/blacklists/tracked-domains/${id}`
    );
  }

  notifyUser(
    id: number,
    request: NotifyUserRequest
  ): Observable<{ message: string }> {
    return this.api.post<NotifyUserRequest, { message: string }>(
      `/api/blacklists/tracked-domains/${id}/notify-user`,
      request
    );
  }

  notifyAll(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/api/blacklists/tracked-domains/notify-all?id=${id}`,
      {}
    );
  }
}

import { TestBed } from '@angular/core/testing';
import { BlacklistsApiService } from './blacklists-api.service';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';
import { HttpParams } from '@angular/common/http';

describe('BlacklistsApiService', () => {
  let service: BlacklistsApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;
  let httpSpy: jasmine.SpyObj<HttpClient>;

  const emptyPaged = {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 25,
    totalPages: 0,
  };

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getPage',
      'getOne',
      'post',
      'put',
      'delete',
      'buildPageParams',
    ]);
    apiSpy.getPage.and.returnValue(of(emptyPaged));
    apiSpy.getOne.and.returnValue(of([]));
    apiSpy.post.and.returnValue(of({ message: 'OK' }));
    apiSpy.put.and.returnValue(of({ message: 'OK' }));
    apiSpy.delete.and.returnValue(of({ message: 'OK' }));
    apiSpy.buildPageParams.and.returnValue(new HttpParams());

    httpSpy = jasmine.createSpyObj('HttpClient', ['get', 'post']);
    httpSpy.get.and.returnValue(of(emptyPaged));
    httpSpy.post.and.returnValue(of({ message: 'OK' }));

    const configSpy = jasmine.createSpyObj('RuntimeConfigService', [], {
      apiUrl: 'http://localhost:5001',
    });

    TestBed.configureTestingModule({
      providers: [
        BlacklistsApiService,
        { provide: ApiService, useValue: apiSpy },
        { provide: HttpClient, useValue: httpSpy },
        { provide: RuntimeConfigService, useValue: configSpy },
      ],
    });
    service = TestBed.inject(BlacklistsApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  // ── Phishing ────────────────────────────────────────────────────────────────

  describe('getPhishingWebsites', () => {
    it('should call api.getPage with /api/blacklists/phishing-websites', () => {
      const req: PagedRequest = { page: 1, pageSize: 25 };
      service.getPhishingWebsites(req).subscribe();
      expect(apiSpy.getPage).toHaveBeenCalledWith(
        '/api/blacklists/phishing-websites',
        req
      );
    });
  });

  // ── Phone Numbers ───────────────────────────────────────────────────────────

  describe('getPhoneNumbers', () => {
    it('should call api.getPage with /api/blacklists/phone-numbers', () => {
      const req: PagedRequest = { page: 1, pageSize: 25 };
      service.getPhoneNumbers(req).subscribe();
      expect(apiSpy.getPage).toHaveBeenCalledWith(
        '/api/blacklists/phone-numbers',
        req
      );
    });
  });

  // ── Bank Websites ───────────────────────────────────────────────────────────

  describe('getBankWebsites', () => {
    it('should call http.get with bank-websites endpoint', () => {
      const req: PagedRequest = { page: 1, pageSize: 25 };
      service.getBankWebsites(req).subscribe();
      expect(httpSpy.get).toHaveBeenCalled();
      const url: string = httpSpy.get.calls.mostRecent().args[0] as string;
      expect(url).toContain('/api/blacklists/bank-websites');
    });

    it('should include isActive param when provided', () => {
      const req: PagedRequest = { page: 1, pageSize: 25 };
      service.getBankWebsites(req, true).subscribe();
      const options = httpSpy.get.calls.mostRecent().args[1] as { params: HttpParams };
      expect(options.params.get('isActive')).toBe('true');
    });
  });

  // ── Website Categories ──────────────────────────────────────────────────────

  describe('getWebsiteCategories', () => {
    it('should call api.getPage with /api/blacklists/website-categories', () => {
      const req: PagedRequest = { page: 1, pageSize: 25 };
      service.getWebsiteCategories(req).subscribe();
      expect(apiSpy.getPage).toHaveBeenCalledWith(
        '/api/blacklists/website-categories',
        req
      );
    });
  });

  describe('getParentCategories', () => {
    it('should call api.getOne with /api/blacklists/website-categories/parents', () => {
      service.getParentCategories().subscribe();
      expect(apiSpy.getOne).toHaveBeenCalledWith(
        '/api/blacklists/website-categories/parents'
      );
    });
  });

  describe('getWebsiteCategoryByName', () => {
    it('should call api.getOne with correct path', () => {
      service.getWebsiteCategoryByName('Social Media').subscribe();
      expect(apiSpy.getOne).toHaveBeenCalledWith(
        '/api/blacklists/website-categories/by-name/Social%20Media'
      );
    });
  });

  describe('createWebsiteCategory', () => {
    it('should call api.post with /api/blacklists/website-categories', () => {
      service.createWebsiteCategory({ name: 'Test' }).subscribe();
      expect(apiSpy.post).toHaveBeenCalledWith(
        '/api/blacklists/website-categories',
        { name: 'Test' }
      );
    });
  });

  describe('updateWebsiteCategory', () => {
    it('should call api.put with correct path', () => {
      service.updateWebsiteCategory('Social Media', { source: 'Manual' }).subscribe();
      expect(apiSpy.put).toHaveBeenCalledWith(
        '/api/blacklists/website-categories/Social%20Media',
        { source: 'Manual' }
      );
    });
  });

  // ── Tracked Domains ─────────────────────────────────────────────────────────

  describe('getTrackedDomains', () => {
    it('should call http.get with tracked-domains endpoint', () => {
      const req: PagedRequest = { page: 1, pageSize: 25 };
      service.getTrackedDomains(req).subscribe();
      const url: string = httpSpy.get.calls.mostRecent().args[0] as string;
      expect(url).toContain('/api/blacklists/tracked-domains');
    });
  });

  describe('createTrackedDomain', () => {
    it('should call api.post with /api/blacklists/tracked-domains', () => {
      service
        .createTrackedDomain({
          domain: 'scam.com',
          category: 'Phishing',
          trackMode: 1,
          notifyAllUsers: false,
        })
        .subscribe();
      expect(apiSpy.post).toHaveBeenCalledWith(
        '/api/blacklists/tracked-domains',
        jasmine.objectContaining({ domain: 'scam.com' })
      );
    });
  });

  describe('updateTrackedDomain', () => {
    it('should call api.put with correct path', () => {
      service
        .updateTrackedDomain(42, { trackMode: 2, isActive: true })
        .subscribe();
      expect(apiSpy.put).toHaveBeenCalledWith(
        '/api/blacklists/tracked-domains/42',
        jasmine.objectContaining({ trackMode: 2 })
      );
    });
  });

  describe('deleteTrackedDomain', () => {
    it('should call api.delete with correct path', () => {
      service.deleteTrackedDomain(42).subscribe();
      expect(apiSpy.delete).toHaveBeenCalledWith(
        '/api/blacklists/tracked-domains/42'
      );
    });
  });

  describe('notifyUser', () => {
    it('should call api.post with notify-user endpoint', () => {
      service
        .notifyUser(42, { userKeyField: 'user-key-1' })
        .subscribe();
      expect(apiSpy.post).toHaveBeenCalledWith(
        '/api/blacklists/tracked-domains/42/notify-user',
        { userKeyField: 'user-key-1' }
      );
    });
  });

  describe('notifyAll', () => {
    it('should call http.post with notify-all endpoint', () => {
      service.notifyAll(42).subscribe();
      const url: string = httpSpy.post.calls.mostRecent().args[0] as string;
      expect(url).toContain('/api/blacklists/tracked-domains/notify-all?id=42');
    });
  });
});

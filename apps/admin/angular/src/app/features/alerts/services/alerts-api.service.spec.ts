import { TestBed } from '@angular/core/testing';
import { AlertsApiService } from './alerts-api.service';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';
import { HttpParams } from '@angular/common/http';

const MOCK_CONFIG = { apiUrl: 'http://localhost:5001' };

describe('AlertsApiService', () => {
  let service: AlertsApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getOne', 'buildPageParams']);
    apiSpy.getOne.and.returnValue(of({}));
    apiSpy.buildPageParams.and.callFake((req: PagedRequest) => {
      let params = new HttpParams()
        .set('page', req.page?.toString() ?? '1')
        .set('pageSize', req.pageSize?.toString() ?? '25');
      if (req.search) params = params.set('search', req.search);
      return params;
    });

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        AlertsApiService,
        { provide: ApiService, useValue: apiSpy },
        { provide: RuntimeConfigService, useValue: MOCK_CONFIG },
      ],
    });
    service = TestBed.inject(AlertsApiService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call /api/alerts with page params', () => {
    service.getAll({ page: 1, pageSize: 25 }).subscribe();
    const req = httpTesting.expectOne((r) => r.url.includes('/api/alerts'));
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });
  });

  it('getAll should add from param when provided', () => {
    service.getAll({ page: 1, pageSize: 25, from: '2025-01-01T00:00:00Z' }).subscribe();
    const req = httpTesting.expectOne((r) => r.url.includes('/api/alerts'));
    expect(req.request.params.get('from')).toBe('2025-01-01T00:00:00Z');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });
  });

  it('getAll should add severity param when provided', () => {
    service.getAll({ page: 1, pageSize: 25, severity: 'High' }).subscribe();
    const req = httpTesting.expectOne((r) => r.url.includes('/api/alerts'));
    expect(req.request.params.get('severity')).toBe('High');
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });
  });

  it('getByKey should call api.getOne with correct path', () => {
    service.getByKey('Alert', 'abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/alerts/Alert/abc');
  });

  it('getAnalysis should call api.getOne with correct path', () => {
    service.getAnalysis('Alert', 'abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/alerts/Alert/abc/analysis');
  });
});

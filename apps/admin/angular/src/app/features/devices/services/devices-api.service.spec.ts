import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DevicesApiService } from './devices-api.service';
import { ApiService } from '@core/services/api.service';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { of } from 'rxjs';
import { HttpParams } from '@angular/common/http';
import { PagedRequest } from '@core/models/paging.model';

describe('DevicesApiService', () => {
  let service: DevicesApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;
  let httpMock: HttpTestingController;

  const configStub = { apiUrl: 'http://localhost:5001' };

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getPage', 'getOne', 'buildPageParams']);
    apiSpy.getPage.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }));
    apiSpy.getOne.and.returnValue(of({}));
    apiSpy.buildPageParams.and.callFake((req: PagedRequest) => {
      let params = new HttpParams()
        .set('page', req.page?.toString() ?? '1')
        .set('pageSize', req.pageSize?.toString() ?? '25');
      if (req.search) params = params.set('search', req.search);
      if (req.sortBy) params = params.set('sortBy', req.sortBy);
      if (req.sortDirection) params = params.set('sortDirection', req.sortDirection);
      return params;
    });

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        DevicesApiService,
        { provide: ApiService, useValue: apiSpy },
        { provide: RuntimeConfigService, useValue: configStub },
      ],
    });
    service = TestBed.inject(DevicesApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call api.getPage with /api/devices', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getAll(req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/devices', req);
  });

  it('getAll with status should send status query param via HttpClient', () => {
    const req: PagedRequest & { status: string } = { page: 1, pageSize: 25, status: 'Active' };
    service.getAll(req).subscribe();
    const httpReq = httpMock.expectOne(r =>
      r.url === 'http://localhost:5001/api/devices' && r.params.get('status') === 'Active'
    );
    expect(httpReq.request.method).toBe('GET');
    httpReq.flush({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 });
    expect(apiSpy.getPage).not.toHaveBeenCalled();
  });

  it('getByKey should call api.getOne with correct path', () => {
    service.getByKey('Device', 'abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/devices/Device/abc');
  });

  it('getByUid should call api.getOne with correct path', () => {
    service.getByUid('uid-123').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/devices/uid/uid-123');
  });

  it('getDeviceAlerts should call api.getPage with correct path', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getDeviceAlerts('Device', 'abc', req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/devices/Device/abc/alerts', req);
  });
});

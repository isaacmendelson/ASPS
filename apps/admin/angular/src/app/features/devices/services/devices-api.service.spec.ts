import { TestBed } from '@angular/core/testing';
import { DevicesApiService } from './devices-api.service';
import { ApiService } from '@core/services/api.service';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';

describe('DevicesApiService', () => {
  let service: DevicesApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getPage', 'getOne', 'buildPageParams']);
    apiSpy.getPage.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }));
    apiSpy.getOne.and.returnValue(of({}));

    TestBed.configureTestingModule({
      providers: [
        DevicesApiService,
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(DevicesApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call api.getPage with /api/devices', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getAll(req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/devices', req);
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

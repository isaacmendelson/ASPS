import { TestBed } from '@angular/core/testing';
import { DashboardService, DashboardSummary } from './dashboard.service';
import { ApiService } from '@core/services/api.service';
import { of } from 'rxjs';
import { PagedResult } from '@core/models/paging.model';
import { DeviceAlert } from '@core/models/alert.model';

const MOCK_SUMMARY: DashboardSummary = {
  totalUsers: 10,
  totalDevices: 20,
  activeAlerts24h: 3,
  analysisResults: 50,
  systemStatus: 'ok',
};

const MOCK_ALERTS: PagedResult<DeviceAlert> = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 5,
  totalPages: 0,
};

describe('DashboardService', () => {
  let service: DashboardService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getOne', 'getPage']);
    apiSpy.getOne.and.returnValue(of(MOCK_SUMMARY));
    apiSpy.getPage.and.returnValue(of(MOCK_ALERTS));

    TestBed.configureTestingModule({
      providers: [
        DashboardService,
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(DashboardService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call GET /api/dashboard/summary', () => {
    service.getSummary().subscribe(summary => {
      expect(summary).toEqual(MOCK_SUMMARY);
    });
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/dashboard/summary');
  });

  it('should call GET /api/alerts with pageSize=5 and sortBy=createdAt', () => {
    service.getRecentAlerts().subscribe();
    const [path, request] = apiSpy.getPage.calls.mostRecent().args;
    expect(path).toBe('/api/alerts');
    expect(request.pageSize).toBe(5);
    expect(request.sortBy).toBe('createdAt');
    expect(request.sortDirection).toBe('desc');
  });
});

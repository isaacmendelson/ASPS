import { TestBed } from '@angular/core/testing';
import { AlertsStateService } from './alerts-state.service';
import { AlertsApiService } from './alerts-api.service';
import { PagedResult } from '@core/models/paging.model';
import { AlertDto } from '@core/models/alert.model';
import { NEVER, of, throwError } from 'rxjs';

const makeAlert = (id: string): AlertDto => ({
  key: { type: 'Alert', value: id },
  alertType: 'PhishingUrl',
  priority: 'High',
  status: 'New',
  timestamp: new Date().toISOString(),
  deviceUid: `uid-${id}`,
  deviceType: 'PersonalComputer',
  operatingSystem: 'Windows',
  deviceKey: { type: 'Device', value: `d-${id}` },
  userKey: { type: 'User', value: 'u1' },
  userName: 'Alice Smith',
});

const makePaged = (items: AlertDto[]): PagedResult<AlertDto> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 25,
  totalPages: 1,
});

describe('AlertsStateService', () => {
  let service: AlertsStateService;
  let apiSpy: jasmine.SpyObj<AlertsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('AlertsApiService', ['getAll']);

    TestBed.configureTestingModule({
      providers: [
        AlertsStateService,
        { provide: AlertsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(AlertsStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty alerts and loading=false', () => {
    expect(service.alerts()).toEqual([]);
    expect(service.loading()).toBeFalse();
  });

  it('should default timeRange to "all"', () => {
    expect(service.timeRange()).toBe('all');
  });

  it('should set loading=true while fetching', () => {
    apiSpy.getAll.and.returnValue(NEVER);
    service.loadPage(1);
    expect(service.loading()).toBeTrue();
  });

  it('should populate alerts on successful load', () => {
    const alerts = [makeAlert('1'), makeAlert('2')];
    apiSpy.getAll.and.returnValue(of(makePaged(alerts)));
    service.loadPage(1);
    expect(service.alerts().length).toBe(2);
    expect(service.loading()).toBeFalse();
    expect(service.totalCount()).toBe(2);
  });

  it('should set error and clear loading on failure', () => {
    apiSpy.getAll.and.returnValue(throwError(() => new Error('Network error')));
    service.loadPage(1);
    expect(service.loading()).toBeFalse();
    expect(service.error()).toBe('Network error');
  });

  it('should reset to page 1 when setSearch is called', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(3);
    service.setSearch('phishing');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.search).toBe('phishing');
  });

  it('should reset to page 1 and set timeRange when setTimeRange is called', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(2);
    service.setTimeRange('24h');
    expect(service.timeRange()).toBe('24h');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.from).toBeDefined();
  });

  it('should not include from param when timeRange is "all"', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.setTimeRange('all');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.from).toBeUndefined();
  });

  it('isEmpty should be true when loading=false and no alerts', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(1);
    expect(service.isEmpty()).toBeTrue();
  });
});

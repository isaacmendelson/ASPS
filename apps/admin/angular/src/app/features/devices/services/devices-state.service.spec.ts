import { TestBed } from '@angular/core/testing';
import { DevicesStateService } from './devices-state.service';
import { DevicesApiService } from './devices-api.service';
import { PagedResult } from '@core/models/paging.model';
import { DeviceDto } from '@core/models/device.model';
import { NEVER, of, throwError } from 'rxjs';

const makeDevice = (id: string): DeviceDto => ({
  key: { type: 'Device', value: id },
  deviceUid: `uid-${id}`,
  deviceType: 'PersonalComputer',
  operatingSystem: 'Windows',
  monitoringStatus: 'Enabled',
  dateCreated: new Date().toISOString(),
  userKey: { type: 'User', value: 'u1' },
  userName: 'Alice Smith',
});

const makePaged = (items: DeviceDto[]): PagedResult<DeviceDto> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 25,
  totalPages: 1,
});

describe('DevicesStateService', () => {
  let service: DevicesStateService;
  let apiSpy: jasmine.SpyObj<DevicesApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('DevicesApiService', ['getAll']);

    TestBed.configureTestingModule({
      providers: [
        DevicesStateService,
        { provide: DevicesApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(DevicesStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty devices and loading=false', () => {
    expect(service.devices()).toEqual([]);
    expect(service.loading()).toBeFalse();
  });

  it('should set loading=true while fetching', () => {
    apiSpy.getAll.and.returnValue(NEVER);
    service.loadPage(1);
    expect(service.loading()).toBeTrue();
  });

  it('should populate devices on successful load', () => {
    const devices = [makeDevice('1'), makeDevice('2')];
    apiSpy.getAll.and.returnValue(of(makePaged(devices)));
    service.loadPage(1);
    expect(service.devices().length).toBe(2);
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
    service.setSearch('abc');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.search).toBe('abc');
  });

  it('should pass sort params to API', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.setSort('deviceType', 'desc');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.sortBy).toBe('deviceType');
    expect(lastCall.sortDirection).toBe('desc');
  });

  it('isEmpty should be true when loading=false and no devices', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(1);
    expect(service.isEmpty()).toBeTrue();
  });

  it('totalPages computed from totalCount and pageSize', () => {
    apiSpy.getAll.and.returnValue(of({
      items: [],
      totalCount: 50,
      page: 1,
      pageSize: 25,
      totalPages: 2,
    }));
    service.loadPage(1);
    expect(service.totalPages()).toBe(2);
  });
});

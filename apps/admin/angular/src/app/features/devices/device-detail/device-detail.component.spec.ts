import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DeviceDetailComponent } from './device-detail.component';
import { DevicesApiService } from '../services/devices-api.service';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DeviceDto } from '@core/models/device.model';
import { AlertDto } from '@core/models/alert.model';
import { PagedResult } from '@core/models/paging.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

const MOCK_DEVICE: DeviceDto = {
  key: { type: 'Device', value: 'd1' },
  deviceUid: 'uid-d1',
  deviceType: 'PersonalComputer',
  operatingSystem: 'Windows',
  monitoringStatus: 'Enabled',
  make: 'Dell',
  model: 'XPS 15',
  dateCreated: '2025-01-01T00:00:00Z',
  userKey: { type: 'User', value: 'u1' },
  userName: 'Alice Smith',
};

const EMPTY_ALERTS: PagedResult<AlertDto> = {
  items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0,
};

describe('DeviceDetailComponent', () => {
  let fixture: ComponentFixture<DeviceDetailComponent>;
  let component: DeviceDetailComponent;
  let apiSpy: jasmine.SpyObj<DevicesApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('DevicesApiService', ['getByKey', 'getDeviceAlerts']);
    apiSpy.getByKey.and.returnValue(of(MOCK_DEVICE));
    apiSpy.getDeviceAlerts.and.returnValue(of(EMPTY_ALERTS));

    await TestBed.configureTestingModule({
      imports: [DeviceDetailComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: DevicesApiService, useValue: apiSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { keyType: 'Device', keyValue: 'd1' } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DeviceDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should show loading state initially', () => {
    expect(component.loading()).toBeTrue();
  });

  it('should load device details on init', () => {
    fixture.detectChanges();
    expect(apiSpy.getByKey).toHaveBeenCalledWith('Device', 'd1');
    expect(component.device()).toEqual(MOCK_DEVICE);
    expect(component.loading()).toBeFalse();
  });

  it('should display device UID in header', () => {
    fixture.detectChanges();
    const header = fixture.nativeElement.querySelector('.page-title');
    expect(header?.textContent).toContain('uid-d1');
  });

  it('should render 2 tabs', () => {
    fixture.detectChanges();
    const tabs = fixture.nativeElement.querySelectorAll('.mat-mdc-tab');
    expect(tabs.length).toBe(2);
  });

  it('should handle device load error gracefully', () => {
    apiSpy.getByKey.and.returnValue(throwError(() => new Error('Not found')));
    fixture = TestBed.createComponent(DeviceDetailComponent);
    component = fixture.componentInstance;
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.loading()).toBeFalse();
  });

  it('should load alerts on init', () => {
    fixture.detectChanges();
    expect(apiSpy.getDeviceAlerts).toHaveBeenCalledWith('Device', 'd1', jasmine.any(Object));
  });
});

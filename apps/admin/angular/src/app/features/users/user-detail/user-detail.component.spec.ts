import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UserDetailComponent } from './user-detail.component';
import { UsersApiService } from '../services/users-api.service';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { UserDetails } from '@core/models/user.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { PagedResult } from '@core/models/paging.model';
import { Device } from '@core/models/device.model';
import { DeviceAlert } from '@core/models/alert.model';
import { UserRiskScore } from '@core/models/risk-score.model';

const MOCK_USER: UserDetails = {
  key: { type: 'User', value: 'u1' },
  keycloakUserId: 'kc-u1',
  firstName: 'Alice',
  lastName: 'Smith',
  email: 'alice@example.com',
  role: 'Self',
  dateCreated: '2025-01-01T00:00:00Z',
  devices: [],
  accounts: [],
  address: '123 Main St',
  city: 'Tel Aviv',
};

const EMPTY_PAGED = <T>(): PagedResult<T> => ({
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 25,
  totalPages: 0,
});

const MOCK_RISK: UserRiskScore = {
  overallScore: 72,
  riskLevel: 'Medium',
  dimensions: { awareness: 70, behavior: 65, exposure: 80 },
  axes: { technical: 68, social: 75, physical: 72 },
  signals: [],
  calculatedAt: new Date().toISOString(),
};

describe('UserDetailComponent', () => {
  let fixture: ComponentFixture<UserDetailComponent>;
  let component: UserDetailComponent;
  let apiSpy: jasmine.SpyObj<UsersApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('UsersApiService', [
      'getByKey',
      'getUserDevices',
      'getUserAlerts',
      'getRiskScore',
      'update',
    ]);
    apiSpy.getByKey.and.returnValue(of(MOCK_USER));
    apiSpy.getUserDevices.and.returnValue(of(EMPTY_PAGED<Device>()));
    apiSpy.getUserAlerts.and.returnValue(of(EMPTY_PAGED<DeviceAlert>()));
    apiSpy.getRiskScore.and.returnValue(of(MOCK_RISK));

    await TestBed.configureTestingModule({
      imports: [UserDetailComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: UsersApiService, useValue: apiSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { params: { keyType: 'User', keyValue: 'u1' } },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UserDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should show loading state initially', () => {
    expect(component.loading()).toBeTrue();
  });

  it('should load user details on init', () => {
    fixture.detectChanges();
    expect(apiSpy.getByKey).toHaveBeenCalledWith('User', 'u1');
    expect(component.user()).toEqual(MOCK_USER);
    expect(component.loading()).toBeFalse();
  });

  it('should display user full name in header', () => {
    fixture.detectChanges();
    const header = fixture.nativeElement.querySelector('.user-name');
    expect(header?.textContent).toContain('Alice Smith');
  });

  it('should render 4 tabs', () => {
    fixture.detectChanges();
    const tabs = fixture.nativeElement.querySelectorAll('.mat-mdc-tab');
    expect(tabs.length).toBe(4);
  });

  it('should handle user load error gracefully', () => {
    apiSpy.getByKey.and.returnValue(throwError(() => new Error('Not found')));
    fixture = TestBed.createComponent(UserDetailComponent);
    component = fixture.componentInstance;
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.loading()).toBeFalse();
  });

  it('should show risk score after loading', () => {
    fixture.detectChanges();
    expect(component.riskScore()).toEqual(MOCK_RISK);
  });
});

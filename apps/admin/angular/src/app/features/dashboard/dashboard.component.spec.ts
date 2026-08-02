import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DashboardComponent } from './dashboard.component';
import { DashboardService, DashboardSummary } from './services/dashboard.service';
import { of, throwError } from 'rxjs';
import { PagedResult } from '@core/models/paging.model';
import { DeviceAlert } from '@core/models/alert.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

const MOCK_SUMMARY: DashboardSummary = {
  totalUsers: 42,
  totalDevices: 88,
  activeAlerts24h: 7,
  analysisResults: 123,
  systemStatus: 'ok',
};

const MOCK_ALERTS_RESULT: PagedResult<DeviceAlert> = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 5,
  totalPages: 0,
};

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let mockService: jasmine.SpyObj<DashboardService>;

  beforeEach(async () => {
    mockService = jasmine.createSpyObj('DashboardService', [
      'getSummary',
      'getRecentAlerts',
    ]);
    mockService.getSummary.and.returnValue(of(MOCK_SUMMARY));
    mockService.getRecentAlerts.and.returnValue(of(MOCK_ALERTS_RESULT));

    await TestBed.configureTestingModule({
      imports: [DashboardComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [{ provide: DashboardService, useValue: mockService }],
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render 4 KPI cards', () => {
    fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('app-kpi-card');
    expect(cards.length).toBe(4);
  });

  it('should show loading state before data arrives', () => {
    // Do NOT call detectChanges — loading should be true initially
    expect(component.loading()).toBeTrue();
  });

  it('should show system status indicator', () => {
    fixture.detectChanges();
    const status = fixture.nativeElement.querySelector('[data-testid="system-status"]');
    expect(status).toBeTruthy();
  });

  it('should show empty state when no recent alerts', () => {
    fixture.detectChanges();
    const empty = fixture.nativeElement.querySelector('app-empty-state');
    expect(empty).toBeTruthy();
  });

  it('should show recent alerts list when alerts exist', () => {
    const alertResult: PagedResult<DeviceAlert> = {
      items: [
        {
          key: { type: 'Alert', value: '1' },
          deviceKey: { type: 'Device', value: 'dev1' },
          userKey: { type: 'User', value: 'u1' },
          title: 'Test Alert',
          description: 'desc',
          severity: 'High',
          dateCreated: new Date().toISOString(),
          isRead: false,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    };
    mockService.getRecentAlerts.and.returnValue(of(alertResult));
    // Re-create component to pick up the new spy return value
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const items = fixture.nativeElement.querySelectorAll('.alert-item');
    expect(items.length).toBe(1);
  });

  it('should set loading to false after data loads', () => {
    fixture.detectChanges();
    expect(component.loading()).toBeFalse();
  });

  it('should handle getSummary error gracefully', () => {
    mockService.getSummary.and.returnValue(throwError(() => new Error('Server error')));
    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.loading()).toBeFalse();
  });
});

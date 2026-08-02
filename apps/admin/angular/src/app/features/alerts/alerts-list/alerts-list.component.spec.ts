import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlertsListComponent } from './alerts-list.component';
import { AlertsStateService, TimeRange } from '../services/alerts-state.service';
import { AlertBadgeService } from '@core/services/alert-badge.service';
import { signal } from '@angular/core';
import { AlertDto } from '@core/models/alert.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

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

describe('AlertsListComponent', () => {
  let fixture: ComponentFixture<AlertsListComponent>;
  let component: AlertsListComponent;

  const buildMockState = (overrides: Partial<{
    alerts: AlertDto[];
    loading: boolean;
    totalCount: number;
    timeRange: TimeRange;
  }> = {}) => ({
    alerts: signal<AlertDto[]>(overrides.alerts ?? []),
    loading: signal<boolean>(overrides.loading ?? false),
    totalCount: signal<number>(overrides.totalCount ?? 0),
    page: signal<number>(1),
    pageSize: signal<number>(25),
    error: signal<string | null>(null),
    timeRange: signal<TimeRange>(overrides.timeRange ?? 'all'),
    loadPage: jasmine.createSpy('loadPage'),
    setSearch: jasmine.createSpy('setSearch'),
    setSort: jasmine.createSpy('setSort'),
    setTimeRange: jasmine.createSpy('setTimeRange'),
  });

  const badgeSpy = jasmine.createSpyObj('AlertBadgeService', ['reset']);

  beforeEach(async () => {
    const mockState = buildMockState();

    await TestBed.configureTestingModule({
      imports: [AlertsListComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: AlertsStateService, useValue: mockState },
        { provide: AlertBadgeService, useValue: badgeSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AlertsListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page header with Device Alerts title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Device Alerts');
  });

  it('should render time-range buttons', () => {
    fixture.detectChanges();
    const buttons = fixture.nativeElement.querySelectorAll('[aria-label="Filter alerts by time range"] button');
    expect(buttons.length).toBe(4);
  });

  it('should reset badge on init', () => {
    fixture.detectChanges();
    expect(badgeSpy.reset).toHaveBeenCalled();
  });

  it('should load page 1 on init', () => {
    const state = TestBed.inject(AlertsStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setTimeRange when time range button clicked', () => {
    const state = TestBed.inject(AlertsStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onTimeRangeChange('24h');
    expect(state.setTimeRange).toHaveBeenCalledWith('24h');
  });

  it('should define 5 columns', () => {
    expect(component.columns.length).toBe(5);
  });
});

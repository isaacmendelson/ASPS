import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlertDetailComponent } from './alert-detail.component';
import { AlertsApiService } from '../services/alerts-api.service';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AlertDto } from '@core/models/alert.model';
import { AnalysisResult } from '@core/models/analysis.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

const MOCK_ALERT: AlertDto = {
  key: { type: 'Alert', value: 'a1' },
  alertType: 'PhishingUrl',
  priority: 'High',
  status: 'New',
  timestamp: '2025-01-01T12:00:00Z',
  deviceUid: 'uid-d1',
  deviceType: 'PersonalComputer',
  operatingSystem: 'Windows',
  deviceKey: { type: 'Device', value: 'd1' },
  userKey: { type: 'User', value: 'u1' },
  userName: 'Alice Smith',
};

const MOCK_ANALYSIS: AnalysisResult = {
  key: { type: 'Analysis', value: 'an1' },
  discriminator: 'UrlAnalysis',
  timestamp: '2025-01-01T12:01:00Z',
  isFromCache: false,
  hasError: false,
  url: 'https://phishing.example.com',
  jsonValue: '{"score":0.95}',
};

describe('AlertDetailComponent', () => {
  let fixture: ComponentFixture<AlertDetailComponent>;
  let component: AlertDetailComponent;
  let apiSpy: jasmine.SpyObj<AlertsApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('AlertsApiService', ['getByKey', 'getAnalysis']);
    apiSpy.getByKey.and.returnValue(of(MOCK_ALERT));
    apiSpy.getAnalysis.and.returnValue(of(MOCK_ANALYSIS));

    await TestBed.configureTestingModule({
      imports: [AlertDetailComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: AlertsApiService, useValue: apiSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { keyType: 'Alert', keyValue: 'a1' } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AlertDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should show loading state initially', () => {
    expect(component.loading()).toBeTrue();
  });

  it('should load alert details on init', () => {
    fixture.detectChanges();
    expect(apiSpy.getByKey).toHaveBeenCalledWith('Alert', 'a1');
    expect(component.alert()).toEqual(MOCK_ALERT);
    expect(component.loading()).toBeFalse();
  });

  it('should display alert type in header', () => {
    fixture.detectChanges();
    const header = fixture.nativeElement.querySelector('.page-title');
    expect(header?.textContent).toContain('PhishingUrl');
  });

  it('should load analysis after loading alert', () => {
    fixture.detectChanges();
    expect(apiSpy.getAnalysis).toHaveBeenCalledWith('Alert', 'a1');
    expect(component.analysis()).toEqual(MOCK_ANALYSIS);
  });

  it('should handle alert load error gracefully', () => {
    apiSpy.getByKey.and.returnValue(throwError(() => new Error('Not found')));
    fixture = TestBed.createComponent(AlertDetailComponent);
    component = fixture.componentInstance;
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(component.loading()).toBeFalse();
  });

  it('should handle null analysis gracefully', () => {
    apiSpy.getAnalysis.and.returnValue(of(null));
    fixture.detectChanges();
    expect(component.analysis()).toBeNull();
  });
});

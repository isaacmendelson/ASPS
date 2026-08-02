import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SystemComponent } from './system.component';
import { SystemApiService } from './services/system-api.service';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { SystemVersion, SystemHealth } from '@core/models/system.model';

const mockVersion: SystemVersion = {
  version: '1.2.3',
  isPrerelease: false,
  isPublicRelease: true,
};

const mockHealth: SystemHealth = {
  status: 'Healthy',
  timestamp: new Date().toISOString(),
};

describe('SystemComponent', () => {
  let fixture: ComponentFixture<SystemComponent>;
  let component: SystemComponent;
  let apiSpy: jasmine.SpyObj<SystemApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('SystemApiService', ['getVersion', 'getHealth', 'reinitializeAsView']);
    apiSpy.getVersion.and.returnValue(of(mockVersion));
    apiSpy.getHealth.and.returnValue(of(mockHealth));
    apiSpy.reinitializeAsView.and.returnValue(of({ message: 'Done' }));

    const confirmSpy = jasmine.createSpyObj('ConfirmDialogService', ['confirm']);
    confirmSpy.confirm.and.returnValue(of(false));

    const notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);

    await TestBed.configureTestingModule({
      imports: [SystemComponent, NoopAnimationsModule],
      providers: [
        { provide: SystemApiService, useValue: apiSpy },
        { provide: ConfirmDialogService, useValue: confirmSpy },
        { provide: NotificationService, useValue: notifySpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SystemComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render System page title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('System');
  });

  it('should call getVersion and getHealth on init', () => {
    fixture.detectChanges();
    expect(apiSpy.getVersion).toHaveBeenCalled();
    expect(apiSpy.getHealth).toHaveBeenCalled();
  });

  it('should display version after load', () => {
    fixture.detectChanges();
    const content = fixture.nativeElement.textContent;
    expect(content).toContain('1.2.3');
  });

  it('should render Re-Initialize ASView button', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="reinitialize-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should call refreshHealth when Refresh is clicked', () => {
    fixture.detectChanges();
    apiSpy.getHealth.calls.reset();
    component.refreshHealth();
    expect(apiSpy.getHealth).toHaveBeenCalled();
  });
});

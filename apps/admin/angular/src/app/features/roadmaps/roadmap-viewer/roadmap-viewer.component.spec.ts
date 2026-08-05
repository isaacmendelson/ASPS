import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RoadmapViewerComponent } from './roadmap-viewer.component';
import { RoadmapsApiService } from '../services/roadmaps-api.service';
import { RoadmapsStateService } from '../services/roadmaps-state.service';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { Roadmap } from '@core/models/roadmap.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

const MOCK_ROADMAP: Roadmap = {
  id: 7,
  name: 'ASPS Roadmap',
  description: 'Master product roadmap',
  data: '{"items":[],"categories":[],"slides":[]}',
  version: 3,
  isArchived: false,
  createdBy: 'isaac',
  lastUpdatedBy: 'isaac',
  dateCreated: '2025-01-01T12:00:00Z',
  lastUpdatedAt: '2025-01-02T12:00:00Z',
};

describe('RoadmapViewerComponent', () => {
  let fixture: ComponentFixture<RoadmapViewerComponent>;
  let component: RoadmapViewerComponent;
  let apiSpy: jasmine.SpyObj<RoadmapsApiService>;
  let stateSpy: jasmine.SpyObj<RoadmapsStateService>;
  let confirmSpy: jasmine.SpyObj<ConfirmDialogService>;
  let notifySpy: jasmine.SpyObj<NotificationService>;

  function setup(paramId: string | null = '7'): void {
    TestBed.configureTestingModule({
      imports: [RoadmapViewerComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: RoadmapsApiService, useValue: apiSpy },
        { provide: RoadmapsStateService, useValue: stateSpy },
        { provide: ConfirmDialogService, useValue: confirmSpy },
        { provide: NotificationService, useValue: notifySpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => paramId } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoadmapViewerComponent);
    component = fixture.componentInstance;
  }

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('RoadmapsApiService', ['getById']);
    apiSpy.getById.and.returnValue(of(MOCK_ROADMAP));

    stateSpy = jasmine.createSpyObj('RoadmapsStateService', ['archive']);
    stateSpy.archive.and.returnValue(of(void 0));

    confirmSpy = jasmine.createSpyObj('ConfirmDialogService', ['confirm']);
    confirmSpy.confirm.and.returnValue(of(true));

    notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);
  });

  it('should create', () => {
    setup();
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should show loading state initially', () => {
    setup();
    expect(component.loading()).toBeTrue();
  });

  it('should load roadmap details on init', () => {
    setup();
    fixture.detectChanges();
    expect(apiSpy.getById).toHaveBeenCalledWith(7);
    expect(component.roadmap()).toEqual(MOCK_ROADMAP);
    expect(component.loading()).toBeFalse();
  });

  it('should display roadmap name in header', () => {
    setup();
    fixture.detectChanges();
    const header = fixture.nativeElement.querySelector('.page-title');
    expect(header?.textContent).toContain('ASPS Roadmap');
  });

  it('should render the viewer iframe once loaded', () => {
    setup();
    fixture.detectChanges();
    const iframe = fixture.nativeElement.querySelector('iframe.viewer-iframe');
    expect(iframe).toBeTruthy();
  });

  it('should show an error state when the roadmap fails to load', () => {
    apiSpy.getById.and.returnValue(throwError(() => ({ error: { message: 'Not found' } })));
    setup();
    fixture.detectChanges();
    expect(component.loading()).toBeFalse();
    expect(component.error()).toBe('Not found');
    expect(fixture.nativeElement.querySelector('iframe')).toBeNull();
  });

  it('should show an error state for an invalid roadmap id without calling the API', () => {
    setup(null);
    fixture.detectChanges();
    expect(apiSpy.getById).not.toHaveBeenCalled();
    expect(component.error()).toBe('Invalid roadmap ID.');
  });

  it('should not post data to the iframe until it signals viewer-ready', () => {
    setup();
    fixture.detectChanges();
    const iframe: HTMLIFrameElement = fixture.nativeElement.querySelector('iframe.viewer-iframe');
    const postMessageSpy = jasmine.createSpy('postMessage');
    Object.defineProperty(iframe, 'contentWindow', { value: { postMessage: postMessageSpy } });

    expect(postMessageSpy).not.toHaveBeenCalled();

    window.dispatchEvent(new MessageEvent('message', { data: { type: 'viewer-ready' } }));

    expect(postMessageSpy).toHaveBeenCalledWith(
      { type: 'roadmap-data', roadmap: MOCK_ROADMAP },
      '*'
    );
  });

  it('should render an Archive button for a non-archived roadmap', () => {
    setup();
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[aria-label="Archive this roadmap"]');
    expect(btn).toBeTruthy();
  });

  it('should not render an Archive button for an already-archived roadmap', () => {
    apiSpy.getById.and.returnValue(of({ ...MOCK_ROADMAP, isArchived: true }));
    setup();
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[aria-label="Archive this roadmap"]');
    expect(btn).toBeFalsy();
  });

  it('should archive the roadmap after confirmation and update local state', () => {
    setup();
    fixture.detectChanges();
    component.onArchive();
    expect(confirmSpy.confirm).toHaveBeenCalled();
    expect(stateSpy.archive).toHaveBeenCalledWith(7);
    expect(component.roadmap()?.isArchived).toBeTrue();
    expect(notifySpy.success).toHaveBeenCalled();
  });

  it('should not archive when the user cancels the confirmation', () => {
    confirmSpy.confirm.and.returnValue(of(false));
    setup();
    fixture.detectChanges();
    component.onArchive();
    expect(stateSpy.archive).not.toHaveBeenCalled();
  });

  it('should surface an error notification when archiving fails', () => {
    stateSpy.archive.and.returnValue(throwError(() => ({ error: { message: 'boom' } })));
    setup();
    fixture.detectChanges();
    component.onArchive();
    expect(notifySpy.error).toHaveBeenCalled();
    expect(component.roadmap()?.isArchived).toBeFalse();
  });
});

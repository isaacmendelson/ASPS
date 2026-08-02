import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RoadmapsListComponent } from './roadmaps-list.component';
import { RoadmapsStateService } from '../services/roadmaps-state.service';
import { signal } from '@angular/core';
import { Roadmap } from '@core/models/roadmap.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { of } from 'rxjs';

const makeRoadmap = (id: number): Roadmap => ({
  id,
  name: `Roadmap ${id}`,
  isArchived: false,
  dateCreated: new Date().toISOString(),
});

describe('RoadmapsListComponent', () => {
  let fixture: ComponentFixture<RoadmapsListComponent>;
  let component: RoadmapsListComponent;

  const buildMockState = (overrides: Partial<{
    roadmaps: Roadmap[];
    loading: boolean;
    totalCount: number;
  }> = {}) => ({
    roadmaps: signal<Roadmap[]>(overrides.roadmaps ?? []),
    loading: signal<boolean>(overrides.loading ?? false),
    totalCount: signal<number>(overrides.totalCount ?? 0),
    page: signal<number>(1),
    pageSize: signal<number>(25),
    error: signal<string | null>(null),
    includeArchived: signal<boolean>(false),
    loadPage: jasmine.createSpy('loadPage'),
    setSearch: jasmine.createSpy('setSearch'),
    setIncludeArchived: jasmine.createSpy('setIncludeArchived'),
    archive: jasmine.createSpy('archive').and.returnValue(of(void 0)),
  });

  beforeEach(async () => {
    const mockState = buildMockState();
    const dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);
    const confirmSpy = jasmine.createSpyObj('ConfirmDialogService', ['confirm']);
    confirmSpy.confirm.and.returnValue(of(false));
    const notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);

    await TestBed.configureTestingModule({
      imports: [RoadmapsListComponent, NoopAnimationsModule],
      providers: [
        { provide: RoadmapsStateService, useValue: mockState },
        { provide: MatDialog, useValue: dialogSpy },
        { provide: ConfirmDialogService, useValue: confirmSpy },
        { provide: NotificationService, useValue: notifySpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RoadmapsListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page header with Roadmaps title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Roadmaps');
  });

  it('should render Create Roadmap button', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="create-roadmap-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should render paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should load page 1 on init', () => {
    const state = TestBed.inject(RoadmapsStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should toggle selected roadmap on row click', () => {
    const rm = makeRoadmap(1);
    fixture.detectChanges();
    component.onRowClick(rm);
    expect(component.selectedRoadmap()).toEqual(rm);
    component.onRowClick(rm);
    expect(component.selectedRoadmap()).toBeNull();
  });

  it('should call setIncludeArchived when toggle changes', () => {
    const state = TestBed.inject(RoadmapsStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onIncludeArchivedChange(true);
    expect(state.setIncludeArchived).toHaveBeenCalledWith(true);
  });
});

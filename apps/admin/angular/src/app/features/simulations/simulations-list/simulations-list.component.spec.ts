import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SimulationsListComponent } from './simulations-list.component';
import { SimulationsStateService } from '../services/simulations-state.service';
import { signal } from '@angular/core';
import { Simulation } from '@core/models/simulation.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { of } from 'rxjs';

const makeSim = (id: string): Simulation => ({
  key: { type: 'Simulation', value: id },
  keyField: `Simulation/${id}`,
  name: `Sim ${id}`,
  status: 'Draft',
  steps: [],
  dateCreated: new Date().toISOString(),
});

describe('SimulationsListComponent', () => {
  let fixture: ComponentFixture<SimulationsListComponent>;
  let component: SimulationsListComponent;

  const buildMockState = (overrides: Partial<{
    simulations: Simulation[];
    loading: boolean;
    totalCount: number;
  }> = {}) => ({
    simulations: signal<Simulation[]>(overrides.simulations ?? []),
    loading: signal<boolean>(overrides.loading ?? false),
    totalCount: signal<number>(overrides.totalCount ?? 0),
    page: signal<number>(1),
    pageSize: signal<number>(25),
    error: signal<string | null>(null),
    loadPage: jasmine.createSpy('loadPage'),
    setSearch: jasmine.createSpy('setSearch'),
    setSort: jasmine.createSpy('setSort'),
    delete: jasmine.createSpy('delete').and.returnValue(of(void 0)),
    run: jasmine.createSpy('run').and.returnValue(of({ message: 'OK' })),
  });

  beforeEach(async () => {
    const mockState = buildMockState();
    const confirmSpy = jasmine.createSpyObj('ConfirmDialogService', ['confirm']);
    confirmSpy.confirm.and.returnValue(of(false));
    const notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);

    await TestBed.configureTestingModule({
      imports: [SimulationsListComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: SimulationsStateService, useValue: mockState },
        { provide: ConfirmDialogService, useValue: confirmSpy },
        { provide: NotificationService, useValue: notifySpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SimulationsListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page header with Simulations title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Simulations');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should render Create Simulation button', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="create-simulation-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should load page 1 on init', () => {
    const state = TestBed.inject(SimulationsStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setSearch when search changes', () => {
    const state = TestBed.inject(SimulationsStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('test');
    expect(state.setSearch).toHaveBeenCalledWith('test');
  });

  it('should toggle selected simulation on row click', () => {
    const sim = makeSim('1');
    fixture.detectChanges();
    component.onRowClick(sim);
    expect(component.selectedSimulation()).toEqual(sim);
    // Clicking again deselects
    component.onRowClick(sim);
    expect(component.selectedSimulation()).toBeNull();
  });
});

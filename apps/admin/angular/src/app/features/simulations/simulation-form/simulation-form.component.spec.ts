import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SimulationFormComponent } from './simulation-form.component';
import { SimulationsStateService } from '../services/simulations-state.service';
import { SimulationsApiService } from '../services/simulations-api.service';
import { signal } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { NotificationService } from '@core/services/notification.service';
import { of } from 'rxjs';

describe('SimulationFormComponent — create mode', () => {
  let fixture: ComponentFixture<SimulationFormComponent>;
  let component: SimulationFormComponent;

  const mockState = {
    saving: signal(false),
    saveError: signal<string | null>(null),
    create: jasmine.createSpy('create').and.returnValue(of(void 0)),
    update: jasmine.createSpy('update').and.returnValue(of(void 0)),
  };

  const mockApi = jasmine.createSpyObj('SimulationsApiService', ['getByKey']);

  beforeEach(async () => {
    const notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);
    const routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    routerSpy.navigate.and.returnValue(Promise.resolve(true));

    await TestBed.configureTestingModule({
      imports: [SimulationFormComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: SimulationsStateService, useValue: mockState },
        { provide: SimulationsApiService, useValue: mockApi },
        { provide: NotificationService, useValue: notifySpy },
        { provide: Router, useValue: routerSpy },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => null } } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SimulationFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should be in create mode when no keyField param', () => {
    expect(component.isEdit).toBeFalse();
  });

  it('should render "New Simulation" title', () => {
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('New Simulation');
  });

  it('should have invalid form with empty name', () => {
    expect(component.form.get('name')?.valid).toBeFalse();
  });

  it('should add and remove steps', () => {
    expect(component.steps.length).toBe(0);
    component.addStep();
    expect(component.steps.length).toBe(1);
    component.removeStep(0);
    expect(component.steps.length).toBe(0);
  });

  it('should not call create when form is invalid', () => {
    mockState.create.calls.reset();
    component.submit();
    expect(mockState.create).not.toHaveBeenCalled();
  });

  it('should call create when form is valid', () => {
    mockState.create.calls.reset();
    component.form.patchValue({ name: 'Test Sim' });
    component.submit();
    expect(mockState.create).toHaveBeenCalled();
  });
});

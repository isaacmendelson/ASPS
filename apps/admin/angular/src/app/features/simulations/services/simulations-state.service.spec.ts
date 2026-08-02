import { TestBed } from '@angular/core/testing';
import { SimulationsStateService } from './simulations-state.service';
import { SimulationsApiService } from './simulations-api.service';
import { PagedResult } from '@core/models/paging.model';
import { Simulation } from '@core/models/simulation.model';
import { NEVER, of, throwError } from 'rxjs';

const makeSim = (id: string): Simulation => ({
  key: { type: 'Simulation', value: id },
  keyField: `Simulation/${id}`,
  name: `Sim ${id}`,
  status: 'Draft',
  steps: [],
  dateCreated: new Date().toISOString(),
});

const makePaged = (items: Simulation[]): PagedResult<Simulation> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 25,
  totalPages: 1,
});

describe('SimulationsStateService', () => {
  let service: SimulationsStateService;
  let apiSpy: jasmine.SpyObj<SimulationsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('SimulationsApiService', ['getAll', 'create', 'update', 'delete', 'run']);

    TestBed.configureTestingModule({
      providers: [
        SimulationsStateService,
        { provide: SimulationsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(SimulationsStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty simulations and loading=false', () => {
    expect(service.simulations()).toEqual([]);
    expect(service.loading()).toBeFalse();
  });

  it('should set loading=true while fetching', () => {
    apiSpy.getAll.and.returnValue(NEVER);
    service.loadPage(1);
    expect(service.loading()).toBeTrue();
  });

  it('should populate simulations on successful load', () => {
    const sims = [makeSim('1'), makeSim('2')];
    apiSpy.getAll.and.returnValue(of(makePaged(sims)));
    service.loadPage(1);
    expect(service.simulations().length).toBe(2);
    expect(service.loading()).toBeFalse();
    expect(service.totalCount()).toBe(2);
  });

  it('should set error and clear loading on failure', () => {
    apiSpy.getAll.and.returnValue(throwError(() => new Error('Network error')));
    service.loadPage(1);
    expect(service.loading()).toBeFalse();
    expect(service.error()).toBe('Network error');
  });

  it('should reset to page 1 when setSearch is called', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(3);
    service.setSearch('test');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.search).toBe('test');
  });

  it('isEmpty should be true when loading=false and no simulations', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(1);
    expect(service.isEmpty()).toBeTrue();
  });
});

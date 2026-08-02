import { TestBed } from '@angular/core/testing';
import { SimulationsApiService } from './simulations-api.service';
import { ApiService } from '@core/services/api.service';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';
import { CreateSimulationRequest, UpdateSimulationRequest } from '@core/models/simulation.model';

const pagedResult = { items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 };
const msgResult = { message: 'OK' };

describe('SimulationsApiService', () => {
  let service: SimulationsApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getPage', 'getOne', 'post', 'put', 'delete']);
    apiSpy.getPage.and.returnValue(of(pagedResult));
    apiSpy.getOne.and.returnValue(of({}));
    apiSpy.post.and.returnValue(of(msgResult));
    apiSpy.put.and.returnValue(of(msgResult));
    apiSpy.delete.and.returnValue(of(msgResult));

    TestBed.configureTestingModule({
      providers: [
        SimulationsApiService,
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(SimulationsApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call api.getPage with /api/simulations', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getAll(req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/simulations', req);
  });

  it('getByKey should call api.getOne with correct path', () => {
    service.getByKey('Simulation/abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/simulations/Simulation/abc');
  });

  it('create should call api.post with /api/simulations', () => {
    const req: CreateSimulationRequest = { name: 'Test', steps: [] };
    service.create(req).subscribe();
    expect(apiSpy.post).toHaveBeenCalledWith('/api/simulations', req);
  });

  it('update should call api.put with correct path', () => {
    const req: UpdateSimulationRequest = { name: 'Test', steps: [] };
    service.update('Simulation/abc', req).subscribe();
    expect(apiSpy.put).toHaveBeenCalledWith('/api/simulations/Simulation/abc', req);
  });

  it('delete should call api.delete with correct path', () => {
    service.delete('Simulation/abc').subscribe();
    expect(apiSpy.delete).toHaveBeenCalledWith('/api/simulations/Simulation/abc');
  });

  it('run should call api.post with run endpoint', () => {
    service.run('Simulation/abc').subscribe();
    expect(apiSpy.post).toHaveBeenCalledWith(
      '/api/simulations/Simulation/abc/run',
      { requestorKeyField: undefined }
    );
  });
});

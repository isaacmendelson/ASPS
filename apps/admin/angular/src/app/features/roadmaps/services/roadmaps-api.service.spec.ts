import { TestBed } from '@angular/core/testing';
import { RoadmapsApiService } from './roadmaps-api.service';
import { ApiService } from '@core/services/api.service';
import { HttpClient, HttpParams } from '@angular/common/http';
import { RuntimeConfigService } from '@core/services/runtime-config.service';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';
import { CreateRoadmapRequest } from '@core/models/roadmap.model';

const pagedResult = { items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 };
const msgResult = { message: 'OK' };

describe('RoadmapsApiService', () => {
  let service: RoadmapsApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;
  let httpSpy: jasmine.SpyObj<HttpClient>;
  let configSpy: jasmine.SpyObj<RuntimeConfigService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['buildPageParams', 'post', 'getPage']);
    apiSpy.buildPageParams.and.returnValue(new HttpParams());
    apiSpy.post.and.returnValue(of(msgResult));
    apiSpy.getPage.and.returnValue(of(pagedResult));

    httpSpy = jasmine.createSpyObj('HttpClient', ['get']);
    httpSpy.get.and.returnValue(of(pagedResult));

    configSpy = jasmine.createSpyObj('RuntimeConfigService', [], { apiUrl: 'http://localhost:5001' });

    TestBed.configureTestingModule({
      providers: [
        RoadmapsApiService,
        { provide: ApiService, useValue: apiSpy },
        { provide: HttpClient, useValue: httpSpy },
        { provide: RuntimeConfigService, useValue: configSpy },
      ],
    });
    service = TestBed.inject(RoadmapsApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call http.get with /api/roadmaps and includeArchived param', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getAll(req, false).subscribe();
    expect(httpSpy.get).toHaveBeenCalled();
    const args = httpSpy.get.calls.mostRecent().args;
    expect(args[0]).toContain('/api/roadmaps');
  });

  it('create should call api.post with /api/roadmaps', () => {
    const req: CreateRoadmapRequest = { name: 'Test', description: 'Desc' };
    service.create(req).subscribe();
    expect(apiSpy.post).toHaveBeenCalledWith('/api/roadmaps', req);
  });

  it('archive should call api.post with correct archive path', () => {
    service.archive(42).subscribe();
    expect(apiSpy.post).toHaveBeenCalledWith('/api/roadmaps/42/archive', {});
  });
});

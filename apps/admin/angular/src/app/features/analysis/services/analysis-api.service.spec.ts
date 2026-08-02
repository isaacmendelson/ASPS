import { TestBed } from '@angular/core/testing';
import { AnalysisApiService } from './analysis-api.service';
import { ApiService } from '@core/services/api.service';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';

describe('AnalysisApiService', () => {
  let service: AnalysisApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getPage', 'getOne']);
    apiSpy.getPage.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }));
    apiSpy.getOne.and.returnValue(of({}));

    TestBed.configureTestingModule({
      providers: [
        AnalysisApiService,
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(AnalysisApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call api.getPage with /api/analysis-results', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getAll(req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/analysis-results', req);
  });

  it('getByKey should call api.getOne with correct path', () => {
    service.getByKey('Analysis', 'abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/analysis-results/Analysis/abc');
  });
});

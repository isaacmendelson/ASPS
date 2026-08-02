import { TestBed } from '@angular/core/testing';
import {
  HttpClientTestingModule,
  HttpTestingController,
} from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { RuntimeConfigService } from './runtime-config.service';
import { PagedRequest } from '../models/paging.model';

describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;
  let configSpy: jasmine.SpyObj<RuntimeConfigService>;

  beforeEach(() => {
    configSpy = jasmine.createSpyObj('RuntimeConfigService', [], {
      apiUrl: 'http://localhost:5001',
    });

    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [
        ApiService,
        { provide: RuntimeConfigService, useValue: configSpy },
      ],
    });

    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('buildPageParams', () => {
    it('should build params with page and pageSize', () => {
      const request: PagedRequest = { page: 2, pageSize: 50 };
      const params = service.buildPageParams(request);
      expect(params.get('page')).toBe('2');
      expect(params.get('pageSize')).toBe('50');
    });

    it('should default page to 1 and pageSize to 25 when undefined', () => {
      const request: PagedRequest = {};
      const params = service.buildPageParams(request);
      expect(params.get('page')).toBe('1');
      expect(params.get('pageSize')).toBe('25');
    });

    it('should include search param when provided', () => {
      const request: PagedRequest = { page: 1, pageSize: 25, search: 'alice' };
      const params = service.buildPageParams(request);
      expect(params.get('search')).toBe('alice');
    });

    it('should not include search param when absent', () => {
      const request: PagedRequest = { page: 1, pageSize: 25 };
      const params = service.buildPageParams(request);
      expect(params.has('search')).toBeFalse();
    });

    it('should include sortBy and sortDirection when provided', () => {
      const request: PagedRequest = {
        page: 1,
        pageSize: 25,
        sortBy: 'lastName',
        sortDirection: 'desc',
      };
      const params = service.buildPageParams(request);
      expect(params.get('sortBy')).toBe('lastName');
      expect(params.get('sortDirection')).toBe('desc');
    });
  });

  describe('getOne', () => {
    it('should GET from the correct URL', () => {
      service.getOne<{ id: number }>('/api/users/1').subscribe();
      const req = httpMock.expectOne('http://localhost:5001/api/users/1');
      expect(req.request.method).toBe('GET');
      req.flush({ id: 1 });
    });
  });

  describe('post', () => {
    it('should POST to the correct URL with body', () => {
      const body = { name: 'Test' };
      service.post<typeof body, { id: number }>('/api/users', body).subscribe();
      const req = httpMock.expectOne('http://localhost:5001/api/users');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(body);
      req.flush({ id: 42 });
    });
  });

  describe('delete', () => {
    it('should DELETE from the correct URL', () => {
      service.delete<void>('/api/users/1').subscribe();
      const req = httpMock.expectOne('http://localhost:5001/api/users/1');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });
});

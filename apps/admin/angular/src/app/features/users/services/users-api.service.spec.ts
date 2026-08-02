import { TestBed } from '@angular/core/testing';
import { UsersApiService } from './users-api.service';
import { ApiService } from '@core/services/api.service';
import { of } from 'rxjs';
import { PagedRequest } from '@core/models/paging.model';
import { CreateUserRequest } from '@core/models/user.model';

describe('UsersApiService', () => {
  let service: UsersApiService;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getPage', 'getOne', 'post', 'put', 'delete']);
    apiSpy.getPage.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }));
    apiSpy.getOne.and.returnValue(of({}));
    apiSpy.post.and.returnValue(of({ key: { type: 'User', value: '1' }, message: 'Created' }));
    apiSpy.put.and.returnValue(of({ message: 'Updated' }));
    apiSpy.delete.and.returnValue(of({ message: 'Deleted' }));

    TestBed.configureTestingModule({
      providers: [
        UsersApiService,
        { provide: ApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(UsersApiService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('getAll should call api.getPage with /api/users', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getAll(req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/users', req);
  });

  it('getByKey should call api.getOne with correct path', () => {
    service.getByKey('User', 'abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/users/User/abc/details');
  });

  it('create should call api.post with /api/users', () => {
    const req: CreateUserRequest = {
      keycloakUserId: '',
      firstName: 'Alice',
      lastName: 'Smith',
      email: 'alice@example.com',
      role: 'Self',
    };
    service.create(req).subscribe();
    expect(apiSpy.post).toHaveBeenCalledWith('/api/users', req);
  });

  it('getRiskScore should call api.getOne with correct path', () => {
    service.getRiskScore('User', 'abc').subscribe();
    expect(apiSpy.getOne).toHaveBeenCalledWith('/api/users/User/abc/risk-score');
  });

  it('getUserDevices should call api.getPage with correct path', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getUserDevices('User', 'abc', req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/users/User/abc/devices', req);
  });

  it('getUserAlerts should call api.getPage with correct path', () => {
    const req: PagedRequest = { page: 1, pageSize: 25 };
    service.getUserAlerts('User', 'abc', req).subscribe();
    expect(apiSpy.getPage).toHaveBeenCalledWith('/api/users/User/abc/alerts', req);
  });

  it('delete should call api.delete with correct path', () => {
    service.delete('User', 'abc').subscribe();
    expect(apiSpy.delete).toHaveBeenCalledWith('/api/users/User/abc');
  });
});

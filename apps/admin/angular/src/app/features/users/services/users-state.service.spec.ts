import { TestBed } from '@angular/core/testing';
import { UsersStateService } from './users-state.service';
import { UsersApiService } from './users-api.service';
import { PagedResult } from '@core/models/paging.model';
import { UserWithDeviceCount } from '@core/models/user.model';
import { NEVER, of, throwError } from 'rxjs';

const makeUser = (id: string): UserWithDeviceCount => ({
  key: { type: 'User', value: id },
  keycloakUserId: `kc-${id}`,
  firstName: 'Alice',
  lastName: 'Smith',
  email: `alice${id}@example.com`,
  role: 'Self',
  dateCreated: new Date().toISOString(),
  deviceCount: 2,
});

const makePaged = (items: UserWithDeviceCount[]): PagedResult<UserWithDeviceCount> => ({
  items,
  totalCount: items.length,
  page: 1,
  pageSize: 25,
  totalPages: 1,
});

describe('UsersStateService', () => {
  let service: UsersStateService;
  let apiSpy: jasmine.SpyObj<UsersApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('UsersApiService', ['getAll', 'create']);

    TestBed.configureTestingModule({
      providers: [
        UsersStateService,
        { provide: UsersApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(UsersStateService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should start with empty users and loading=false', () => {
    expect(service.users()).toEqual([]);
    expect(service.loading()).toBeFalse();
  });

  it('should set loading=true while fetching', () => {
    apiSpy.getAll.and.returnValue(NEVER);
    service.loadPage(1);
    expect(service.loading()).toBeTrue();
  });

  it('should populate users on successful load', () => {
    const users = [makeUser('1'), makeUser('2')];
    apiSpy.getAll.and.returnValue(of(makePaged(users)));
    service.loadPage(1);
    expect(service.users().length).toBe(2);
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
    service.setSearch('alice');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.search).toBe('alice');
  });

  it('should pass sort params to API', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.setSort('lastName', 'desc');
    const lastCall = apiSpy.getAll.calls.mostRecent().args[0];
    expect(lastCall.sortBy).toBe('lastName');
    expect(lastCall.sortDirection).toBe('desc');
  });

  it('isEmpty should be true when loading=false and no users', () => {
    apiSpy.getAll.and.returnValue(of(makePaged([])));
    service.loadPage(1);
    expect(service.isEmpty()).toBeTrue();
  });

  it('totalPages computed from totalCount and pageSize', () => {
    apiSpy.getAll.and.returnValue(of({
      items: [],
      totalCount: 100,
      page: 1,
      pageSize: 25,
      totalPages: 4,
    }));
    service.loadPage(1);
    expect(service.totalPages()).toBe(4);
  });
});

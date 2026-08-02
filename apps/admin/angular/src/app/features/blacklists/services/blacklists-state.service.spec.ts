import { TestBed } from '@angular/core/testing';
import { of, throwError, NEVER } from 'rxjs';
import {
  PhishingStateService,
  PhonesStateService,
  BanksStateService,
  CategoriesStateService,
  DomainsStateService,
} from './blacklists-state.service';
import { BlacklistsApiService } from './blacklists-api.service';
import { PagedResult } from '@core/models/paging.model';
import {
  PhishingWebsite,
  BlacklistedPhoneNumber,
  BankWebsite,
  WebsiteCategory,
  TrackedDomain,
} from '@core/models/blacklist.model';

const emptyPaged = <T>(): PagedResult<T> => ({
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 25,
  totalPages: 0,
});

const makePhishing = (id: number): PhishingWebsite => ({
  id,
  url: `http://phish${id}.com`,
  domain: `phish${id}.com`,
  isActive: true,
  dateCreated: new Date().toISOString(),
});

const makePhone = (id: number): BlacklistedPhoneNumber => ({
  id,
  phoneNumber: `+1-555-000${id}`,
  isDeleted: false,
  dateCreated: new Date().toISOString(),
});

const makeBank = (id: number): BankWebsite => ({
  id,
  domain: `bank${id}.com`,
  bankName: `Bank ${id}`,
  isActive: true,
  dateCreated: new Date().toISOString(),
});

const makeCategory = (key: string): WebsiteCategory => ({
  key,
  name: key,
  isDeleted: false,
  dateCreated: new Date().toISOString(),
});

const makeDomain = (id: number): TrackedDomain => ({
  id,
  domain: `domain${id}.com`,
  category: 'Phishing',
  trackMode: 1,
  isActive: true,
  dateCreated: new Date().toISOString(),
  dateModified: new Date().toISOString(),
});

// ─── PhishingStateService ─────────────────────────────────────────────────────

describe('PhishingStateService', () => {
  let service: PhishingStateService;
  let apiSpy: jasmine.SpyObj<BlacklistsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('BlacklistsApiService', [
      'getPhishingWebsites',
    ]);
    TestBed.configureTestingModule({
      providers: [
        PhishingStateService,
        { provide: BlacklistsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(PhishingStateService);
  });

  it('should be created', () => expect(service).toBeTruthy());

  it('should start with empty items and loading=false', () => {
    expect(service.items()).toEqual([]);
    expect(service.loading()).toBeFalse();
  });

  it('should set loading=true while fetching', () => {
    apiSpy.getPhishingWebsites.and.returnValue(NEVER);
    service.loadPage(1);
    expect(service.loading()).toBeTrue();
  });

  it('should populate items on success', () => {
    const items = [makePhishing(1), makePhishing(2)];
    apiSpy.getPhishingWebsites.and.returnValue(
      of({ ...emptyPaged<PhishingWebsite>(), items, totalCount: 2 })
    );
    service.loadPage(1);
    expect(service.items().length).toBe(2);
    expect(service.loading()).toBeFalse();
  });

  it('should set error on failure', () => {
    apiSpy.getPhishingWebsites.and.returnValue(
      throwError(() => new Error('Network error'))
    );
    service.loadPage(1);
    expect(service.error()).toBe('Network error');
    expect(service.loading()).toBeFalse();
  });

  it('setSearch should reset to page 1', () => {
    apiSpy.getPhishingWebsites.and.returnValue(of(emptyPaged<PhishingWebsite>()));
    service.loadPage(3);
    service.setSearch('phish');
    const lastCall = apiSpy.getPhishingWebsites.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.search).toBe('phish');
  });

  it('isEmpty should be true when no items and not loading', () => {
    apiSpy.getPhishingWebsites.and.returnValue(of(emptyPaged<PhishingWebsite>()));
    service.loadPage(1);
    expect(service.isEmpty()).toBeTrue();
  });
});

// ─── PhonesStateService ───────────────────────────────────────────────────────

describe('PhonesStateService', () => {
  let service: PhonesStateService;
  let apiSpy: jasmine.SpyObj<BlacklistsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('BlacklistsApiService', ['getPhoneNumbers']);
    TestBed.configureTestingModule({
      providers: [
        PhonesStateService,
        { provide: BlacklistsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(PhonesStateService);
  });

  it('should be created', () => expect(service).toBeTruthy());

  it('should populate items on success', () => {
    const items = [makePhone(1)];
    apiSpy.getPhoneNumbers.and.returnValue(
      of({ ...emptyPaged<BlacklistedPhoneNumber>(), items, totalCount: 1 })
    );
    service.loadPage(1);
    expect(service.items().length).toBe(1);
  });
});

// ─── BanksStateService ────────────────────────────────────────────────────────

describe('BanksStateService', () => {
  let service: BanksStateService;
  let apiSpy: jasmine.SpyObj<BlacklistsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('BlacklistsApiService', ['getBankWebsites']);
    TestBed.configureTestingModule({
      providers: [
        BanksStateService,
        { provide: BlacklistsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(BanksStateService);
  });

  it('should be created', () => expect(service).toBeTruthy());

  it('should pass isActive filter to API', () => {
    apiSpy.getBankWebsites.and.returnValue(of(emptyPaged<BankWebsite>()));
    service.setIsActive(true);
    const lastCall = apiSpy.getBankWebsites.calls.mostRecent().args;
    expect(lastCall[1]).toBe(true);
  });

  it('setSearch should reset to page 1', () => {
    apiSpy.getBankWebsites.and.returnValue(of(emptyPaged<BankWebsite>()));
    service.loadPage(3);
    service.setSearch('bank');
    const lastCall = apiSpy.getBankWebsites.calls.mostRecent().args[0];
    expect(lastCall.page).toBe(1);
    expect(lastCall.search).toBe('bank');
  });
});

// ─── CategoriesStateService ───────────────────────────────────────────────────

describe('CategoriesStateService', () => {
  let service: CategoriesStateService;
  let apiSpy: jasmine.SpyObj<BlacklistsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('BlacklistsApiService', [
      'getWebsiteCategories',
      'getParentCategories',
      'createWebsiteCategory',
      'updateWebsiteCategory',
    ]);
    apiSpy.getWebsiteCategories.and.returnValue(of(emptyPaged<WebsiteCategory>()));
    apiSpy.getParentCategories.and.returnValue(of([]));
    apiSpy.createWebsiteCategory.and.returnValue(
      of({ message: 'Created', categoryId: 'cat-1' })
    );
    apiSpy.updateWebsiteCategory.and.returnValue(of({ message: 'Updated' }));

    TestBed.configureTestingModule({
      providers: [
        CategoriesStateService,
        { provide: BlacklistsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(CategoriesStateService);
  });

  it('should be created', () => expect(service).toBeTruthy());

  it('should populate items on load', () => {
    const items = [makeCategory('Social'), makeCategory('Banking')];
    apiSpy.getWebsiteCategories.and.returnValue(
      of({ ...emptyPaged<WebsiteCategory>(), items, totalCount: 2 })
    );
    service.loadPage(1);
    expect(service.items().length).toBe(2);
  });

  it('create should call api and reload', () => {
    service.create({ name: 'New Cat' }).subscribe();
    expect(apiSpy.createWebsiteCategory).toHaveBeenCalledWith({ name: 'New Cat' });
    expect(apiSpy.getWebsiteCategories).toHaveBeenCalled();
  });

  it('update should call api and reload', () => {
    service.update('Social', { source: 'Manual' }).subscribe();
    expect(apiSpy.updateWebsiteCategory).toHaveBeenCalledWith('Social', {
      source: 'Manual',
    });
    expect(apiSpy.getWebsiteCategories).toHaveBeenCalled();
  });

  it('create failure should set saveError', () => {
    apiSpy.createWebsiteCategory.and.returnValue(
      throwError(() => ({ message: 'Duplicate name' }))
    );
    service.create({ name: 'Dupe' }).subscribe({ error: () => {} });
    expect(service.saveError()).toBe('Duplicate name');
  });

  it('clearSaveError should reset saveError', () => {
    apiSpy.createWebsiteCategory.and.returnValue(
      throwError(() => ({ message: 'Err' }))
    );
    service.create({ name: 'X' }).subscribe({ error: () => {} });
    service.clearSaveError();
    expect(service.saveError()).toBeNull();
  });

  it('loadParentCategories should populate parentCategories', () => {
    apiSpy.getParentCategories.and.returnValue(
      of([makeCategory('Root')])
    );
    service.loadParentCategories();
    expect(service.parentCategories().length).toBe(1);
  });
});

// ─── DomainsStateService ──────────────────────────────────────────────────────

describe('DomainsStateService', () => {
  let service: DomainsStateService;
  let apiSpy: jasmine.SpyObj<BlacklistsApiService>;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('BlacklistsApiService', [
      'getTrackedDomains',
      'createTrackedDomain',
      'updateTrackedDomain',
      'deleteTrackedDomain',
      'notifyUser',
      'notifyAll',
    ]);
    apiSpy.getTrackedDomains.and.returnValue(of(emptyPaged<TrackedDomain>()));
    apiSpy.createTrackedDomain.and.returnValue(
      of({ message: 'Created', trackedDomainId: 1 })
    );
    apiSpy.updateTrackedDomain.and.returnValue(
      of({ message: 'Updated', trackedDomainId: 1 })
    );
    apiSpy.deleteTrackedDomain.and.returnValue(of({ message: 'Deleted' }));
    apiSpy.notifyUser.and.returnValue(of({ message: 'Notified' }));
    apiSpy.notifyAll.and.returnValue(of({ message: 'Notified all' }));

    TestBed.configureTestingModule({
      providers: [
        DomainsStateService,
        { provide: BlacklistsApiService, useValue: apiSpy },
      ],
    });
    service = TestBed.inject(DomainsStateService);
  });

  it('should be created', () => expect(service).toBeTruthy());

  it('should populate items on load', () => {
    const items = [makeDomain(1), makeDomain(2)];
    apiSpy.getTrackedDomains.and.returnValue(
      of({ ...emptyPaged<TrackedDomain>(), items, totalCount: 2 })
    );
    service.loadPage(1);
    expect(service.items().length).toBe(2);
  });

  it('setCategoryFilter should reset to page 1 and pass category', () => {
    apiSpy.getTrackedDomains.and.returnValue(of(emptyPaged<TrackedDomain>()));
    service.loadPage(3);
    service.setCategoryFilter('Phishing');
    const lastCall = apiSpy.getTrackedDomains.calls.mostRecent().args;
    expect(lastCall[0].page).toBe(1);
    expect(lastCall[1]).toBe('Phishing');
  });

  it('create should call api and reload', () => {
    service
      .create({ domain: 'bad.com', category: 'Phishing', trackMode: 1, notifyAllUsers: false })
      .subscribe();
    expect(apiSpy.createTrackedDomain).toHaveBeenCalled();
    expect(apiSpy.getTrackedDomains).toHaveBeenCalled();
  });

  it('delete should call api and reload', () => {
    service.delete(42).subscribe();
    expect(apiSpy.deleteTrackedDomain).toHaveBeenCalledWith(42);
    expect(apiSpy.getTrackedDomains).toHaveBeenCalled();
  });

  it('notifyUser should call api', () => {
    service.notifyUser(42, { userKeyField: 'key-1' }).subscribe();
    expect(apiSpy.notifyUser).toHaveBeenCalledWith(42, { userKeyField: 'key-1' });
  });

  it('notifyAll should call api', () => {
    service.notifyAll(42).subscribe();
    expect(apiSpy.notifyAll).toHaveBeenCalledWith(42);
  });

  it('delete failure should set saveError', () => {
    apiSpy.deleteTrackedDomain.and.returnValue(
      throwError(() => ({ message: 'Not found' }))
    );
    service.delete(99).subscribe({ error: () => {} });
    expect(service.saveError()).toBe('Not found');
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DomainsListComponent } from './domains-list.component';
import { DomainsStateService } from '../../services/blacklists-state.service';
import { BlacklistsApiService } from '../../services/blacklists-api.service';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { MatDialog } from '@angular/material/dialog';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { TrackedDomain } from '@core/models/blacklist.model';

const makeDomain = (id: number): TrackedDomain => ({
  id,
  domain: `domain${id}.com`,
  category: 'Phishing',
  trackMode: 1,
  isActive: true,
  dateCreated: new Date().toISOString(),
  dateModified: new Date().toISOString(),
});

const buildMockState = (items: TrackedDomain[] = []) => ({
  items: signal<TrackedDomain[]>(items),
  totalCount: signal<number>(items.length),
  loading: signal<boolean>(false),
  error: signal<string | null>(null),
  saving: signal<boolean>(false),
  saveError: signal<string | null>(null),
  page: signal<number>(1),
  pageSize: signal<number>(25),
  search: signal<string>(''),
  categoryFilter: signal<string | undefined>(undefined),
  loadPage: jasmine.createSpy('loadPage'),
  setSearch: jasmine.createSpy('setSearch'),
  setCategoryFilter: jasmine.createSpy('setCategoryFilter'),
  delete: jasmine.createSpy('delete').and.returnValue(of(undefined)),
  notifyUser: jasmine.createSpy('notifyUser').and.returnValue(of(undefined)),
  notifyAll: jasmine.createSpy('notifyAll').and.returnValue(of(undefined)),
  clearSaveError: jasmine.createSpy('clearSaveError'),
});

describe('DomainsListComponent', () => {
  let fixture: ComponentFixture<DomainsListComponent>;
  let component: DomainsListComponent;

  beforeEach(async () => {
    const mockState = buildMockState();

    const apiSpy = jasmine.createSpyObj('BlacklistsApiService', [
      'getParentCategories',
    ]);
    apiSpy.getParentCategories.and.returnValue(of([]));

    const confirmSpy = jasmine.createSpyObj('ConfirmDialogService', ['confirm']);
    confirmSpy.confirm.and.returnValue(of(true));

    const notifySpy = jasmine.createSpyObj('NotificationService', [
      'success',
      'error',
    ]);

    const dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);
    dialogSpy.open.and.returnValue({ afterClosed: () => of(false) });

    await TestBed.configureTestingModule({
      imports: [DomainsListComponent, NoopAnimationsModule],
      providers: [
        { provide: DomainsStateService, useValue: mockState },
        { provide: BlacklistsApiService, useValue: apiSpy },
        { provide: ConfirmDialogService, useValue: confirmSpy },
        { provide: NotificationService, useValue: notifySpy },
        { provide: MatDialog, useValue: dialogSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DomainsListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Tracked Domains');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should render Add Domain button', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="add-domain-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should call loadPage(1) on init', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setSearch on search change', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('scam');
    expect(state.setSearch).toHaveBeenCalledWith('scam');
  });

  it('should call setCategoryFilter on filter change', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onCategoryFilterChange('Phishing');
    expect(state.setCategoryFilter).toHaveBeenCalledWith('Phishing');
  });

  it('onDelete should call state.delete after confirm', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onDelete(makeDomain(1));
    expect(state.delete).toHaveBeenCalledWith(1);
  });

  it('onNotifyUser should call state.notifyUser after confirm', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onNotifyUser(makeDomain(2));
    expect(state.notifyUser).toHaveBeenCalledWith(
      2,
      jasmine.objectContaining({ userKeyField: '' })
    );
  });

  it('onNotifyAll should call state.notifyAll after confirm', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onNotifyAll(makeDomain(3));
    expect(state.notifyAll).toHaveBeenCalledWith(3);
  });
});

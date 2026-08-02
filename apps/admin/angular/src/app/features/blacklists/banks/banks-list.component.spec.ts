import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BanksListComponent } from './banks-list.component';
import { BanksStateService } from '../services/blacklists-state.service';
import { signal } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { BankWebsite } from '@core/models/blacklist.model';

const buildMockState = (items: BankWebsite[] = []) => ({
  items: signal<BankWebsite[]>(items),
  totalCount: signal<number>(items.length),
  loading: signal<boolean>(false),
  error: signal<string | null>(null),
  page: signal<number>(1),
  pageSize: signal<number>(25),
  search: signal<string>(''),
  isActive: signal<boolean | undefined>(undefined),
  loadPage: jasmine.createSpy('loadPage'),
  setSearch: jasmine.createSpy('setSearch'),
  setIsActive: jasmine.createSpy('setIsActive'),
});

describe('BanksListComponent', () => {
  let fixture: ComponentFixture<BanksListComponent>;
  let component: BanksListComponent;

  beforeEach(async () => {
    const mockState = buildMockState();

    await TestBed.configureTestingModule({
      imports: [BanksListComponent, NoopAnimationsModule],
      providers: [{ provide: BanksStateService, useValue: mockState }],
    }).compileComponents();

    fixture = TestBed.createComponent(BanksListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Bank Websites');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should call loadPage(1) on init', () => {
    const state = TestBed.inject(
      BanksStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setIsActive when filter changes', () => {
    const state = TestBed.inject(
      BanksStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onActiveFilterChange(true);
    expect(state.setIsActive).toHaveBeenCalledWith(true);
  });

  it('should call setSearch on search change', () => {
    const state = TestBed.inject(
      BanksStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('bank');
    expect(state.setSearch).toHaveBeenCalledWith('bank');
  });
});

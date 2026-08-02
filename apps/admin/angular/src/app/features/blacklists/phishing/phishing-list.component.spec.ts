import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PhishingListComponent } from './phishing-list.component';
import { PhishingStateService } from '../services/blacklists-state.service';
import { signal } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { PhishingWebsite } from '@core/models/blacklist.model';

const buildMockState = (items: PhishingWebsite[] = []) => ({
  items: signal<PhishingWebsite[]>(items),
  totalCount: signal<number>(items.length),
  loading: signal<boolean>(false),
  error: signal<string | null>(null),
  page: signal<number>(1),
  pageSize: signal<number>(25),
  search: signal<string>(''),
  loadPage: jasmine.createSpy('loadPage'),
  setSearch: jasmine.createSpy('setSearch'),
});

describe('PhishingListComponent', () => {
  let fixture: ComponentFixture<PhishingListComponent>;
  let component: PhishingListComponent;

  beforeEach(async () => {
    const mockState = buildMockState();

    await TestBed.configureTestingModule({
      imports: [PhishingListComponent, NoopAnimationsModule],
      providers: [{ provide: PhishingStateService, useValue: mockState }],
    }).compileComponents();

    fixture = TestBed.createComponent(PhishingListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Phishing Websites');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should call loadPage(1) on init', () => {
    const state = TestBed.inject(
      PhishingStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setSearch on search change', () => {
    const state = TestBed.inject(
      PhishingStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('phish');
    expect(state.setSearch).toHaveBeenCalledWith('phish');
  });

  it('should call loadPage with correct page on page change', () => {
    const state = TestBed.inject(
      PhishingStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onPageChange({ pageIndex: 1, pageSize: 25, length: 100 });
    expect(state.loadPage).toHaveBeenCalledWith(2, 25);
  });
});

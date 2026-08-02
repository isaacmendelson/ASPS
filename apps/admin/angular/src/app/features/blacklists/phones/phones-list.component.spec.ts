import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PhonesListComponent } from './phones-list.component';
import { PhonesStateService } from '../services/blacklists-state.service';
import { signal } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { BlacklistedPhoneNumber } from '@core/models/blacklist.model';

const buildMockState = (items: BlacklistedPhoneNumber[] = []) => ({
  items: signal<BlacklistedPhoneNumber[]>(items),
  totalCount: signal<number>(items.length),
  loading: signal<boolean>(false),
  error: signal<string | null>(null),
  page: signal<number>(1),
  pageSize: signal<number>(25),
  search: signal<string>(''),
  loadPage: jasmine.createSpy('loadPage'),
  setSearch: jasmine.createSpy('setSearch'),
});

describe('PhonesListComponent', () => {
  let fixture: ComponentFixture<PhonesListComponent>;
  let component: PhonesListComponent;

  beforeEach(async () => {
    const mockState = buildMockState();

    await TestBed.configureTestingModule({
      imports: [PhonesListComponent, NoopAnimationsModule],
      providers: [{ provide: PhonesStateService, useValue: mockState }],
    }).compileComponents();

    fixture = TestBed.createComponent(PhonesListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Blacklisted Phone Numbers');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should call loadPage(1) on init', () => {
    const state = TestBed.inject(
      PhonesStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setSearch on search change', () => {
    const state = TestBed.inject(
      PhonesStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('+1-555');
    expect(state.setSearch).toHaveBeenCalledWith('+1-555');
  });
});

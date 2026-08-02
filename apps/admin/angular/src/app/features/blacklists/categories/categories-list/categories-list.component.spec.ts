import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CategoriesListComponent } from './categories-list.component';
import { CategoriesStateService } from '../../services/blacklists-state.service';
import { signal } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { WebsiteCategory } from '@core/models/blacklist.model';
import { Router } from '@angular/router';

const buildMockState = (items: WebsiteCategory[] = []) => ({
  items: signal<WebsiteCategory[]>(items),
  totalCount: signal<number>(items.length),
  loading: signal<boolean>(false),
  error: signal<string | null>(null),
  page: signal<number>(1),
  pageSize: signal<number>(25),
  search: signal<string>(''),
  loadPage: jasmine.createSpy('loadPage'),
  setSearch: jasmine.createSpy('setSearch'),
});

describe('CategoriesListComponent', () => {
  let fixture: ComponentFixture<CategoriesListComponent>;
  let component: CategoriesListComponent;

  beforeEach(async () => {
    const mockState = buildMockState();

    await TestBed.configureTestingModule({
      imports: [CategoriesListComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [{ provide: CategoriesStateService, useValue: mockState }],
    }).compileComponents();

    fixture = TestBed.createComponent(CategoriesListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Website Categories');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should render Add Category button', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="add-category-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should call loadPage(1) on init', () => {
    const state = TestBed.inject(
      CategoriesStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should navigate to new route on openCreateForm', () => {
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    fixture.detectChanges();
    component.openCreateForm();
    expect(navSpy).toHaveBeenCalledWith(['/blacklists/categories/new']);
  });

  it('should navigate to edit route on row click', () => {
    const router = TestBed.inject(Router);
    const navSpy = spyOn(router, 'navigate');
    fixture.detectChanges();
    component.onRowClick({ name: 'Social Media' } as WebsiteCategory);
    expect(navSpy).toHaveBeenCalledWith([
      '/blacklists/categories',
      'Social Media',
      'edit',
    ]);
  });
});

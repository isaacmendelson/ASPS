import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AnalysisListComponent } from './analysis-list.component';
import { AnalysisApiService } from '../services/analysis-api.service';
import { of } from 'rxjs';
import { AnalysisResult } from '@core/models/analysis.model';
import { PagedResult } from '@core/models/paging.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

const makeResult = (id: string): AnalysisResult => ({
  key: { type: 'Analysis', value: id },
  discriminator: 'UrlAnalysis',
  timestamp: new Date().toISOString(),
  isFromCache: false,
  hasError: false,
  url: `https://example.com/${id}`,
});

const EMPTY_PAGED: PagedResult<AnalysisResult> = {
  items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0,
};

describe('AnalysisListComponent', () => {
  let fixture: ComponentFixture<AnalysisListComponent>;
  let component: AnalysisListComponent;
  let apiSpy: jasmine.SpyObj<AnalysisApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('AnalysisApiService', ['getAll']);
    apiSpy.getAll.and.returnValue(of(EMPTY_PAGED));

    await TestBed.configureTestingModule({
      imports: [AnalysisListComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: AnalysisApiService, useValue: apiSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AnalysisListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page header with Analysis Results title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Analysis Results');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should call getAll on init', () => {
    fixture.detectChanges();
    expect(apiSpy.getAll).toHaveBeenCalledWith(jasmine.objectContaining({ page: 1 }));
  });

  it('should populate items on successful load', () => {
    const results = [makeResult('1'), makeResult('2')];
    apiSpy.getAll.and.returnValue(of({ items: results, totalCount: 2, page: 1, pageSize: 25, totalPages: 1 }));
    fixture.detectChanges();
    expect(component.items().length).toBe(2);
    expect(component.totalCount()).toBe(2);
  });

  it('should define 4 columns', () => {
    expect(component.columns.length).toBe(4);
  });
});

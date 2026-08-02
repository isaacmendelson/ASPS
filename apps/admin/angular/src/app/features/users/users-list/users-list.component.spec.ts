import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UsersListComponent } from './users-list.component';
import { UsersStateService } from '../services/users-state.service';
import { signal } from '@angular/core';
import { UserWithDeviceCount } from '@core/models/user.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { MatDialog } from '@angular/material/dialog';

const makeUser = (id: string): UserWithDeviceCount => ({
  key: { type: 'User', value: id },
  keycloakUserId: `kc-${id}`,
  firstName: 'Alice',
  lastName: 'Smith',
  email: `alice${id}@example.com`,
  role: 'Self',
  dateCreated: new Date().toISOString(),
  deviceCount: 3,
});

describe('UsersListComponent', () => {
  let fixture: ComponentFixture<UsersListComponent>;
  let component: UsersListComponent;

  const buildMockState = (overrides: Partial<{
    users: UserWithDeviceCount[];
    loading: boolean;
    totalCount: number;
  }> = {}) => ({
    users: signal<UserWithDeviceCount[]>(overrides.users ?? []),
    loading: signal<boolean>(overrides.loading ?? false),
    totalCount: signal<number>(overrides.totalCount ?? 0),
    page: signal<number>(1),
    pageSize: signal<number>(25),
    error: signal<string | null>(null),
    loadPage: jasmine.createSpy('loadPage'),
    setSearch: jasmine.createSpy('setSearch'),
    setSort: jasmine.createSpy('setSort'),
  });

  beforeEach(async () => {
    const mockState = buildMockState();
    const dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [UsersListComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: UsersStateService, useValue: mockState },
        { provide: MatDialog, useValue: dialogSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(UsersListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page header with Users title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Users');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should render Add User button', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('[data-testid="add-user-btn"]');
    expect(btn).toBeTruthy();
  });

  it('should load page 1 on init', () => {
    const state = TestBed.inject(UsersStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setSearch when search changes', () => {
    const state = TestBed.inject(UsersStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('alice');
    expect(state.setSearch).toHaveBeenCalledWith('alice');
  });
});

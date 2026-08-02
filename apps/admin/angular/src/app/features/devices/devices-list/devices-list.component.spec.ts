import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DevicesListComponent } from './devices-list.component';
import { DevicesStateService } from '../services/devices-state.service';
import { signal } from '@angular/core';
import { DeviceDto } from '@core/models/device.model';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';

const makeDevice = (id: string): DeviceDto => ({
  key: { type: 'Device', value: id },
  deviceUid: `uid-${id}`,
  deviceType: 'PersonalComputer',
  operatingSystem: 'Windows',
  monitoringStatus: 'Enabled',
  dateCreated: new Date().toISOString(),
  userKey: { type: 'User', value: 'u1' },
  userName: 'Alice Smith',
});

describe('DevicesListComponent', () => {
  let fixture: ComponentFixture<DevicesListComponent>;
  let component: DevicesListComponent;

  const buildMockState = (overrides: Partial<{
    devices: DeviceDto[];
    loading: boolean;
    totalCount: number;
  }> = {}) => ({
    devices: signal<DeviceDto[]>(overrides.devices ?? []),
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

    await TestBed.configureTestingModule({
      imports: [DevicesListComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: DevicesStateService, useValue: mockState },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DevicesListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should render page header with Devices title', () => {
    fixture.detectChanges();
    const title = fixture.nativeElement.querySelector('.page-title');
    expect(title?.textContent).toContain('Devices');
  });

  it('should render the paged table', () => {
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('app-paged-table');
    expect(table).toBeTruthy();
  });

  it('should load page 1 on init', () => {
    const state = TestBed.inject(DevicesStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    expect(state.loadPage).toHaveBeenCalledWith(1);
  });

  it('should call setSearch when search changes', () => {
    const state = TestBed.inject(DevicesStateService) as unknown as ReturnType<typeof buildMockState>;
    fixture.detectChanges();
    component.onSearchChange('uid-1');
    expect(state.setSearch).toHaveBeenCalledWith('uid-1');
  });

  it('should define 6 columns', () => {
    expect(component.columns.length).toBe(6);
  });
});

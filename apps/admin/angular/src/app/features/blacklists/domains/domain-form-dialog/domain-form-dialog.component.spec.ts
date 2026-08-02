import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DomainFormDialogComponent } from './domain-form-dialog.component';
import { DomainsStateService } from '../../services/blacklists-state.service';
import { BlacklistsApiService } from '../../services/blacklists-api.service';
import { NotificationService } from '@core/services/notification.service';
import {
  MAT_DIALOG_DATA,
  MatDialogRef,
} from '@angular/material/dialog';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { TrackedDomain } from '@core/models/blacklist.model';

const makeDomain = (): TrackedDomain => ({
  id: 1,
  domain: 'bad.com',
  category: 'Phishing',
  trackMode: 1,
  isActive: true,
  dateCreated: new Date().toISOString(),
  dateModified: new Date().toISOString(),
});

const buildMockState = () => ({
  saving: signal<boolean>(false),
  saveError: signal<string | null>(null),
  create: jasmine.createSpy('create').and.returnValue(of(undefined)),
  update: jasmine.createSpy('update').and.returnValue(of(undefined)),
  clearSaveError: jasmine.createSpy('clearSaveError'),
});

const configure = async (domain?: TrackedDomain) => {
  const mockState = buildMockState();
  const apiSpy = jasmine.createSpyObj('BlacklistsApiService', [
    'getParentCategories',
  ]);
  apiSpy.getParentCategories.and.returnValue(of([]));

  const notifySpy = jasmine.createSpyObj('NotificationService', [
    'success',
    'error',
  ]);
  const dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

  await TestBed.configureTestingModule({
    imports: [DomainFormDialogComponent, NoopAnimationsModule],
    providers: [
      { provide: DomainsStateService, useValue: mockState },
      { provide: BlacklistsApiService, useValue: apiSpy },
      { provide: NotificationService, useValue: notifySpy },
      { provide: MatDialogRef, useValue: dialogRefSpy },
      { provide: MAT_DIALOG_DATA, useValue: domain ? { domain } : {} },
    ],
  }).compileComponents();

  return TestBed.createComponent(DomainFormDialogComponent);
};

describe('DomainFormDialogComponent — create mode', () => {
  let fixture: ComponentFixture<DomainFormDialogComponent>;
  let component: DomainFormDialogComponent;

  beforeEach(async () => {
    fixture = await configure();
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('should have isEdit=false', () => expect(component.isEdit).toBeFalse());

  it('form should be invalid when empty', () => {
    expect(component.form.invalid).toBeTrue();
  });

  it('form should be valid with domain and category', () => {
    component.form.patchValue({ domain: 'scam.com', category: 'Phishing' });
    expect(component.form.valid).toBeTrue();
  });

  it('submit should call state.create', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    component.form.patchValue({ domain: 'scam.com', category: 'Phishing' });
    component.submit();
    expect(state.create).toHaveBeenCalledWith(
      jasmine.objectContaining({ domain: 'scam.com', category: 'Phishing' })
    );
  });

  it('cancel should close dialog', () => {
    const ref = TestBed.inject(MatDialogRef);
    component.cancel();
    expect(ref.close).toHaveBeenCalledWith(false);
  });
});

describe('DomainFormDialogComponent — edit mode', () => {
  let fixture: ComponentFixture<DomainFormDialogComponent>;
  let component: DomainFormDialogComponent;

  beforeEach(async () => {
    fixture = await configure(makeDomain());
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should have isEdit=true', () => expect(component.isEdit).toBeTrue());

  it('form should be pre-filled with domain data', () => {
    expect(component.form.get('domain')?.value).toBe('bad.com');
    expect(component.form.get('category')?.value).toBe('Phishing');
  });

  it('submit should call state.update', () => {
    const state = TestBed.inject(
      DomainsStateService
    ) as unknown as ReturnType<typeof buildMockState>;
    component.submit();
    expect(state.update).toHaveBeenCalledWith(
      1,
      jasmine.objectContaining({ domain: 'bad.com' })
    );
  });
});

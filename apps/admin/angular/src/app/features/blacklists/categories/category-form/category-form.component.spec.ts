import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CategoryFormComponent } from './category-form.component';
import { CategoriesStateService } from '../../services/blacklists-state.service';
import { BlacklistsApiService } from '../../services/blacklists-api.service';
import { NotificationService } from '@core/services/notification.service';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute, Router } from '@angular/router';

const buildMockState = () => ({
  saving: signal<boolean>(false),
  saveError: signal<string | null>(null),
  create: jasmine.createSpy('create').and.returnValue(of(undefined)),
  update: jasmine.createSpy('update').and.returnValue(of(undefined)),
  clearSaveError: jasmine.createSpy('clearSaveError'),
});

describe('CategoryFormComponent', () => {
  let fixture: ComponentFixture<CategoryFormComponent>;
  let component: CategoryFormComponent;
  let apiSpy: jasmine.SpyObj<BlacklistsApiService>;
  let notifySpy: jasmine.SpyObj<NotificationService>;

  const configureFor = async (name?: string) => {
    const mockState = buildMockState();

    apiSpy = jasmine.createSpyObj('BlacklistsApiService', [
      'getParentCategories',
      'getWebsiteCategoryByName',
    ]);
    apiSpy.getParentCategories.and.returnValue(of([]));
    if (name) {
      apiSpy.getWebsiteCategoryByName.and.returnValue(
        of({
          key: name,
          name,
          isDeleted: false,
          dateCreated: new Date().toISOString(),
        })
      );
    }

    notifySpy = jasmine.createSpyObj('NotificationService', ['success', 'error']);

    const routeStub = {
      snapshot: { paramMap: { get: () => name ?? null } },
    };

    await TestBed.configureTestingModule({
      imports: [CategoryFormComponent, NoopAnimationsModule, RouterTestingModule],
      providers: [
        { provide: CategoriesStateService, useValue: mockState },
        { provide: BlacklistsApiService, useValue: apiSpy },
        { provide: NotificationService, useValue: notifySpy },
        { provide: ActivatedRoute, useValue: routeStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CategoryFormComponent);
    component = fixture.componentInstance;
  };

  describe('create mode', () => {
    beforeEach(async () => configureFor());

    it('should create', () => {
      fixture.detectChanges();
      expect(component).toBeTruthy();
    });

    it('should render "New Category" title', () => {
      fixture.detectChanges();
      const title = fixture.nativeElement.querySelector('.page-title');
      expect(title?.textContent).toContain('New Category');
    });

    it('should have isEdit=false', () => {
      fixture.detectChanges();
      expect(component.isEdit).toBeFalse();
    });

    it('form should be invalid when name is empty', () => {
      fixture.detectChanges();
      expect(component.form.invalid).toBeTrue();
    });

    it('form should be valid with a name', () => {
      fixture.detectChanges();
      component.form.patchValue({ name: 'TestCat' });
      expect(component.form.valid).toBeTrue();
    });

    it('submit should call state.create', () => {
      fixture.detectChanges();
      const state = TestBed.inject(
        CategoriesStateService
      ) as unknown as ReturnType<typeof buildMockState>;
      component.form.patchValue({ name: 'MyCat' });
      component.submit();
      expect(state.create).toHaveBeenCalledWith(
        jasmine.objectContaining({ name: 'MyCat' })
      );
    });

    it('submit should navigate to categories on success', () => {
      fixture.detectChanges();
      const router = TestBed.inject(Router);
      const navSpy = spyOn(router, 'navigate');
      component.form.patchValue({ name: 'MyCat' });
      component.submit();
      expect(navSpy).toHaveBeenCalledWith(['/blacklists/categories']);
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => configureFor('Social Media'));

    it('should render "Edit Category" title', () => {
      fixture.detectChanges();
      const title = fixture.nativeElement.querySelector('.page-title');
      expect(title?.textContent).toContain('Edit Category');
    });

    it('should have isEdit=true', () => {
      fixture.detectChanges();
      expect(component.isEdit).toBeTrue();
    });

    it('submit should call state.update', () => {
      fixture.detectChanges();
      const state = TestBed.inject(
        CategoriesStateService
      ) as unknown as ReturnType<typeof buildMockState>;
      component.submit();
      expect(state.update).toHaveBeenCalled();
    });
  });
});

import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { CategoriesStateService } from '../../services/blacklists-state.service';
import { BlacklistsApiService } from '../../services/blacklists-api.service';
import { WebsiteCategory } from '@core/models/blacklist.model';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-category-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
    MatCardModule,
  ],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">{{ isEdit ? 'Edit Category' : 'New Category' }}</h1>
          <p class="page-subtitle">
            {{ isEdit ? 'Update an existing website category.' : 'Create a new website category.' }}
          </p>
        </div>
        <button mat-button (click)="cancel()" aria-label="Cancel and return to categories list">
          <mat-icon aria-hidden="true">arrow_back</mat-icon>
          Back to Categories
        </button>
      </div>
    </div>

    <mat-card class="form-card">
      <mat-card-content>
        @if (loadError) {
          <p class="form-error" role="alert">{{ loadError }}</p>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" id="category-form">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Name</mat-label>
            <input
              matInput
              formControlName="name"
              aria-required="true"
              placeholder="e.g. Social Media"
              [readonly]="isEdit" />
            @if (form.get('name')?.invalid && form.get('name')?.touched) {
              <mat-error>Name is required.</mat-error>
            }
            @if (isEdit) {
              <mat-hint>Name cannot be changed after creation.</mat-hint>
            }
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Parent Category (optional)</mat-label>
            <mat-select formControlName="parentId">
              <mat-option [value]="null">— None —</mat-option>
              @for (cat of parentCategories; track cat.key) {
                <mat-option [value]="cat.key">{{ cat.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Source (optional)</mat-label>
            <input matInput formControlName="source" placeholder="e.g. Manual, Import" />
          </mat-form-field>

          @if (state.saveError()) {
            <p class="form-error" role="alert">{{ state.saveError() }}</p>
          }

          <div class="form-actions">
            <button mat-button type="button" (click)="cancel()" [disabled]="state.saving()">
              Cancel
            </button>
            <button
              mat-flat-button
              color="primary"
              type="submit"
              [disabled]="state.saving() || form.invalid">
              @if (state.saving()) {
                <mat-spinner diameter="18"></mat-spinner>
              } @else {
                {{ isEdit ? 'Save Changes' : 'Create Category' }}
              }
            </button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    .page-header-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }
    .form-card { max-width: 600px; margin-top: 24px; }
    .full-width { width: 100%; margin-bottom: 8px; }
    .form-error { color: var(--danger, #c00); font-size: 13px; margin: 4px 0; }
    .form-actions { display: flex; gap: 8px; justify-content: flex-end; margin-top: 16px; }
    mat-spinner { display: inline-block; }
  `],
})
export class CategoryFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private blacklistsApi = inject(BlacklistsApiService);
  private notification = inject(NotificationService);
  readonly state = inject(CategoriesStateService);

  isEdit = false;
  editName = '';
  loadError: string | null = null;
  parentCategories: WebsiteCategory[] = [];

  form: FormGroup = this.fb.group({
    name: ['', [Validators.required]],
    parentId: [null],
    source: [''],
  });

  ngOnInit(): void {
    this.editName = this.route.snapshot.paramMap.get('name') ?? '';
    this.isEdit = !!this.editName;

    this.blacklistsApi.getParentCategories().subscribe({
      next: (cats) => (this.parentCategories = cats),
      error: () => (this.parentCategories = []),
    });

    if (this.isEdit) {
      this.blacklistsApi.getWebsiteCategoryByName(this.editName).subscribe({
        next: (cat) => {
          this.form.patchValue({
            name: cat.name,
            parentId: cat.parentId ?? null,
            source: cat.source ?? '',
          });
          this.form.get('name')?.disable();
        },
        error: () => {
          this.loadError = `Category "${this.editName}" could not be loaded.`;
        },
      });
    }

    this.state.clearSaveError();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, parentId, source } = this.form.getRawValue() as {
      name: string;
      parentId: string | null;
      source: string;
    };

    if (this.isEdit) {
      this.state
        .update(this.editName, {
          parentId: parentId ?? undefined,
          source: source || undefined,
        })
        .subscribe({
          next: () => {
            this.notification.success(
              'Category Updated',
              `"${this.editName}" has been updated.`
            );
            this.router.navigate(['/blacklists/categories']);
          },
          error: () => {
            // error displayed in state.saveError()
          },
        });
    } else {
      this.state
        .create({
          name: name.trim(),
          parentId: parentId ?? undefined,
          source: source || undefined,
        })
        .subscribe({
          next: () => {
            this.notification.success(
              'Category Created',
              `"${name}" has been created.`
            );
            this.router.navigate(['/blacklists/categories']);
          },
          error: () => {
            // error displayed in state.saveError()
          },
        });
    }
  }

  cancel(): void {
    this.router.navigate(['/blacklists/categories']);
  }
}

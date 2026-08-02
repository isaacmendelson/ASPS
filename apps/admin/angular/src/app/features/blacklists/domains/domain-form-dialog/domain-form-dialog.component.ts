import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DomainsStateService } from '../../services/blacklists-state.service';
import { BlacklistsApiService } from '../../services/blacklists-api.service';
import { WebsiteCategory, TrackedDomain } from '@core/models/blacklist.model';
import { NotificationService } from '@core/services/notification.service';

export interface DomainFormDialogData {
  domain?: TrackedDomain;
}

@Component({
  selector: 'app-domain-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit Tracked Domain' : 'Add Tracked Domain' }}</h2>

    <mat-dialog-content>
      <form [formGroup]="form" id="domain-form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Domain</mat-label>
          <input
            matInput
            formControlName="domain"
            placeholder="e.g. example-scam.com"
            aria-required="true" />
          @if (form.get('domain')?.invalid && form.get('domain')?.touched) {
            <mat-error>Domain is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Category</mat-label>
          <mat-select formControlName="category" aria-required="true">
            @for (cat of categories; track cat.key) {
              <mat-option [value]="cat.name">{{ cat.name }}</mat-option>
            }
            @if (categories.length === 0) {
              <mat-option value="Manual">Manual</mat-option>
            }
          </mat-select>
          @if (form.get('category')?.invalid && form.get('category')?.touched) {
            <mat-error>Category is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Track Mode</mat-label>
          <mat-select formControlName="trackMode" aria-required="true">
            <mat-option [value]="0">None</mat-option>
            <mat-option [value]="1">Surf</mat-option>
            <mat-option [value]="2">Click</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Reason (optional)</mat-label>
          <input matInput formControlName="reason" placeholder="Why is this domain tracked?" />
        </mat-form-field>

        @if (state.saveError()) {
          <p class="form-error" role="alert">{{ state.saveError() }}</p>
        }
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="cancel()" [disabled]="state.saving()">
        Cancel
      </button>
      <button
        mat-flat-button
        color="primary"
        form="domain-form"
        type="submit"
        [disabled]="state.saving() || form.invalid">
        @if (state.saving()) {
          <mat-spinner diameter="18"></mat-spinner>
        } @else {
          {{ isEdit ? 'Save Changes' : 'Add Domain' }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { min-width: 440px; padding-top: 8px; }
    .full-width { width: 100%; margin-bottom: 4px; }
    .form-error { color: var(--danger, #c00); font-size: 13px; margin: 4px 0 0; }
    mat-dialog-actions { padding: 8px 0 0; gap: 8px; }
    mat-spinner { display: inline-block; }
    @media (max-width: 480px) {
      mat-dialog-content { min-width: unset; }
    }
  `],
})
export class DomainFormDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<DomainFormDialogComponent>);
  private blacklistsApi = inject(BlacklistsApiService);
  private notification = inject(NotificationService);
  readonly state = inject(DomainsStateService);

  categories: WebsiteCategory[] = [];

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: DomainFormDialogData
  ) {}

  isEdit = false;

  form: FormGroup = this.fb.group({
    domain: ['', [Validators.required]],
    category: ['', [Validators.required]],
    trackMode: [1, [Validators.required]],
    reason: [''],
  });

  ngOnInit(): void {
    this.isEdit = !!this.data?.domain;

    this.blacklistsApi.getParentCategories().subscribe({
      next: (cats) => (this.categories = cats),
      error: () => (this.categories = []),
    });

    if (this.isEdit && this.data.domain) {
      const d = this.data.domain;
      this.form.patchValue({
        domain: d.domain,
        category: d.category,
        trackMode: d.trackMode,
        reason: d.reason ?? '',
      });
    }

    this.state.clearSaveError();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { domain, category, trackMode, reason } = this.form.value as {
      domain: string;
      category: string;
      trackMode: number;
      reason: string;
    };

    if (this.isEdit && this.data.domain) {
      this.state
        .update(this.data.domain.id, {
          domain,
          category,
          trackMode,
          isActive: this.data.domain.isActive,
          reason: reason || undefined,
        })
        .subscribe({
          next: () => {
            this.notification.success('Domain Updated', `"${domain}" has been updated.`);
            this.dialogRef.close(true);
          },
          error: () => {},
        });
    } else {
      this.state
        .create({
          domain,
          category,
          trackMode,
          reason: reason || undefined,
          notifyAllUsers: false,
        })
        .subscribe({
          next: () => {
            this.notification.success('Domain Added', `"${domain}" is now being tracked.`);
            this.dialogRef.close(true);
          },
          error: () => {},
        });
    }
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}

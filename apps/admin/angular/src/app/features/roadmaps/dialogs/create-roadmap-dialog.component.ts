import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RoadmapsStateService } from '../services/roadmaps-state.service';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-create-roadmap-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>Create Roadmap</h2>

    <mat-dialog-content>
      <form [formGroup]="form" id="create-roadmap-form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" aria-required="true" />
          @if (form.get('name')?.invalid && form.get('name')?.touched) {
            <mat-error>Name is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Description (optional)</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
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
        form="create-roadmap-form"
        type="submit"
        [disabled]="state.saving() || form.invalid">
        @if (state.saving()) {
          <mat-spinner diameter="18"></mat-spinner>
        } @else {
          Create Roadmap
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { min-width: 420px; padding-top: 8px; }
    .full-width { width: 100%; margin-bottom: 4px; }
    .form-error { color: var(--danger, #d32f2f); font-size: 13px; margin: 4px 0 0; }
    mat-dialog-actions { padding: 8px 0 0; gap: 8px; }
    mat-spinner { display: inline-block; }
    @media (max-width: 480px) {
      mat-dialog-content { min-width: unset; }
    }
  `],
})
export class CreateRoadmapDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<CreateRoadmapDialogComponent>);
  private notification = inject(NotificationService);
  readonly state = inject(RoadmapsStateService);

  form: FormGroup = this.fb.group({
    name: ['', [Validators.required]],
    description: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, description } = this.form.value as {
      name: string;
      description: string;
    };

    this.state.create({ name, description: description || undefined }).subscribe({
      next: () => {
        this.notification.success('Roadmap Created', `"${name}" has been added.`);
        this.dialogRef.close(true);
      },
      error: () => {
        // saveError shown in template
      },
    });
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}

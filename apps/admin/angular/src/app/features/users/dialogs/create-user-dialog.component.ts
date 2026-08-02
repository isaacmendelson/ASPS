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
import { MatIconModule } from '@angular/material/icon';
import { UsersStateService } from '../services/users-state.service';
import { CreateUserRequest } from '@core/models/user.model';
import { NotificationService } from '@core/services/notification.service';

@Component({
  selector: 'app-create-user-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>Add User</h2>

    <mat-dialog-content>
      <form [formGroup]="form" id="create-user-form" (ngSubmit)="submit()">
        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>First Name</mat-label>
            <input matInput formControlName="firstName" autocomplete="given-name"
                   aria-required="true" />
            @if (form.get('firstName')?.invalid && form.get('firstName')?.touched) {
              <mat-error>First name is required.</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Last Name</mat-label>
            <input matInput formControlName="lastName" autocomplete="family-name"
                   aria-required="true" />
            @if (form.get('lastName')?.invalid && form.get('lastName')?.touched) {
              <mat-error>Last name is required.</mat-error>
            }
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email</mat-label>
          <input matInput formControlName="email" type="email"
                 autocomplete="email" aria-required="true" />
          @if (form.get('email')?.errors?.['required'] && form.get('email')?.touched) {
            <mat-error>Email is required.</mat-error>
          }
          @if (form.get('email')?.errors?.['email'] && form.get('email')?.touched) {
            <mat-error>Please enter a valid email address.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Phone (optional)</mat-label>
          <input matInput formControlName="phone" type="tel"
                 autocomplete="tel" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Address (optional)</mat-label>
          <input matInput formControlName="address" autocomplete="street-address" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>City (optional)</mat-label>
          <input matInput formControlName="city" autocomplete="address-level2" />
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
        form="create-user-form"
        type="submit"
        [disabled]="state.saving() || form.invalid"
        (click)="submit()">
        @if (state.saving()) {
          <mat-spinner diameter="18"></mat-spinner>
        } @else {
          Add User
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { min-width: 440px; padding-top: 8px; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .full-width { width: 100%; }
    mat-form-field { width: 100%; margin-bottom: 4px; }
    .form-error { color: var(--danger); font-size: 13px; margin: 4px 0 0; }
    mat-dialog-actions { padding: 8px 0 0; gap: 8px; }
    mat-spinner { display: inline-block; }
    @media (max-width: 480px) {
      mat-dialog-content { min-width: unset; }
      .form-row { grid-template-columns: 1fr; }
    }
  `],
})
export class CreateUserDialogComponent {
  private fb = inject(FormBuilder);
  private dialogRef = inject(MatDialogRef<CreateUserDialogComponent>);
  private notification = inject(NotificationService);
  readonly state = inject(UsersStateService);

  form: FormGroup = this.fb.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    address: [''],
    city: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { firstName, lastName, email, address, city } = this.form.value as {
      firstName: string;
      lastName: string;
      email: string;
      phone: string;
      address: string;
      city: string;
    };

    const request: CreateUserRequest = {
      keycloakUserId: '',  // Will be assigned by backend / Keycloak provisioning
      firstName,
      lastName,
      email,
      role: 'Self',
      address: address || undefined,
      city: city || undefined,
    };

    this.state.createUser(request).subscribe({
      next: () => {
        this.notification.success('User Created', `${firstName} ${lastName} has been added.`);
        this.dialogRef.close(true);
      },
      error: () => {
        // Error already set in state.saveError() — displayed in template
      },
    });
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}

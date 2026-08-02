import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  FormArray,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { SimulationsApiService } from '../services/simulations-api.service';
import { SimulationsStateService } from '../services/simulations-state.service';
import { NotificationService } from '@core/services/notification.service';
import {
  CreateSimulationRequest,
  UpdateSimulationRequest,
  SimulationStep,
} from '@core/models/simulation.model';

@Component({
  selector: 'app-simulation-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatCardModule,
    MatDividerModule,
  ],
  template: `
    <div class="page-header">
      <div class="page-header-row">
        <div>
          <h1 class="page-title">{{ isEdit ? 'Edit Simulation' : 'New Simulation' }}</h1>
          <p class="page-subtitle">{{ isEdit ? 'Update simulation details and steps.' : 'Define a new phishing simulation.' }}</p>
        </div>
      </div>
    </div>

    @if (loadError) {
      <p class="form-error" role="alert">{{ loadError }}</p>
    }

    <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
      <mat-card class="form-card">
        <mat-card-content>
          <h2 class="section-title">Simulation Details</h2>

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
        </mat-card-content>
      </mat-card>

      <mat-card class="form-card steps-card">
        <mat-card-content>
          <div class="section-header">
            <h2 class="section-title">Steps</h2>
            <button
              mat-stroked-button
              type="button"
              color="primary"
              (click)="addStep()"
              aria-label="Add a simulation step">
              <mat-icon aria-hidden="true">add</mat-icon>
              Add Step
            </button>
          </div>

          @if (steps.length === 0) {
            <p class="empty-steps" role="status">No steps yet. Add a step to define the simulation flow.</p>
          }

          <ng-container formArrayName="steps">
          @for (step of steps.controls; track $index) {
            <div [formGroupName]="$index" class="step-form">
              <div class="step-header">
                <span class="step-number" [attr.aria-label]="'Step ' + ($index + 1)">Step {{ $index + 1 }}</span>
                <button
                  mat-icon-button
                  type="button"
                  color="warn"
                  (click)="removeStep($index)"
                  [attr.aria-label]="'Remove step ' + ($index + 1)">
                  <mat-icon aria-hidden="true">delete</mat-icon>
                </button>
              </div>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Title</mat-label>
                <input matInput formControlName="title" aria-required="true" />
                @if (step.get('title')?.invalid && step.get('title')?.touched) {
                  <mat-error>Step title is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Description</mat-label>
                <textarea matInput formControlName="description" rows="2" aria-required="true"></textarea>
                @if (step.get('description')?.invalid && step.get('description')?.touched) {
                  <mat-error>Step description is required.</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>URL (optional)</mat-label>
                <input matInput formControlName="url" type="url" />
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>Expected Action (optional)</mat-label>
                <input matInput formControlName="expectedAction" />
              </mat-form-field>

              @if ($index < steps.length - 1) {
                <mat-divider></mat-divider>
              }
            </div>
          }
          </ng-container>
        </mat-card-content>
      </mat-card>

      @if (state.saveError()) {
        <p class="form-error" role="alert">{{ state.saveError() }}</p>
      }

      <div class="form-actions">
        <button
          mat-button
          type="button"
          (click)="cancel()"
          [disabled]="state.saving()">
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
            {{ isEdit ? 'Save Changes' : 'Create Simulation' }}
          }
        </button>
      </div>
    </form>
  `,
  styles: [`
    .page-header-row {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }
    .form-card {
      margin-bottom: 16px;
    }
    .section-title {
      font-size: 16px;
      font-weight: 600;
      margin: 0 0 16px;
    }
    .section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }
    .section-header .section-title { margin: 0; }
    .full-width { width: 100%; margin-bottom: 4px; }
    .step-form { padding: 12px 0; }
    .step-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 8px;
    }
    .step-number { font-weight: 600; font-size: 14px; }
    .empty-steps { color: var(--text-secondary, #666); font-size: 14px; }
    .form-actions {
      display: flex;
      gap: 8px;
      justify-content: flex-end;
      margin-top: 8px;
    }
    .form-error { color: var(--danger, #d32f2f); font-size: 13px; margin: 8px 0; }
    mat-spinner { display: inline-block; }
  `],
})
export class SimulationFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private simulationsApi = inject(SimulationsApiService);
  readonly state = inject(SimulationsStateService);
  private notification = inject(NotificationService);

  form!: FormGroup;
  isEdit = false;
  keyField: string | null = null;
  loadError: string | null = null;

  get steps(): FormArray {
    return this.form.get('steps') as FormArray;
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      steps: this.fb.array([]),
    });

    this.keyField = this.route.snapshot.paramMap.get('keyField');
    this.isEdit = !!this.keyField;

    if (this.isEdit && this.keyField) {
      this.simulationsApi.getByKey(this.keyField).subscribe({
        next: (simulation) => {
          this.form.patchValue({
            name: simulation.name,
            description: simulation.description ?? '',
          });
          (simulation.steps ?? []).forEach((s) => this.addStepWithValues(s));
        },
        error: (err: { error?: { message?: string }; message?: string }) => {
          this.loadError =
            err?.error?.message ?? err?.message ?? 'Failed to load simulation.';
        },
      });
    }
  }

  addStep(): void {
    this.steps.push(this.buildStepGroup());
  }

  removeStep(index: number): void {
    this.steps.removeAt(index);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, description, steps } = this.form.value as {
      name: string;
      description: string;
      steps: SimulationStep[];
    };

    const stepsWithOrder: SimulationStep[] = steps.map((s, i) => ({
      ...s,
      order: i + 1,
    }));

    if (this.isEdit && this.keyField) {
      const req: UpdateSimulationRequest = {
        name,
        description: description || undefined,
        steps: stepsWithOrder,
      };
      this.state.update(this.keyField, req).subscribe({
        next: () => {
          this.notification.success('Simulation Updated', `"${name}" has been saved.`);
          this.router.navigate(['/simulations']);
        },
        error: () => {
          // saveError shown in template
        },
      });
    } else {
      const req: CreateSimulationRequest = {
        name,
        description: description || undefined,
        steps: stepsWithOrder,
      };
      this.state.create(req).subscribe({
        next: () => {
          this.notification.success('Simulation Created', `"${name}" has been created.`);
          this.router.navigate(['/simulations']);
        },
        error: () => {
          // saveError shown in template
        },
      });
    }
  }

  cancel(): void {
    this.router.navigate(['/simulations']);
  }

  private buildStepGroup(step?: Partial<SimulationStep>): FormGroup {
    return this.fb.group({
      order: [step?.order ?? 0],
      title: [step?.title ?? '', [Validators.required]],
      description: [step?.description ?? '', [Validators.required]],
      url: [step?.url ?? ''],
      expectedAction: [step?.expectedAction ?? ''],
    });
  }

  private addStepWithValues(step: SimulationStep): void {
    this.steps.push(this.buildStepGroup(step));
  }
}

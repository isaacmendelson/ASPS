import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-access-denied',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <div class="access-denied-page" role="main">
      <mat-icon class="access-icon" aria-hidden="true">lock</mat-icon>
      <h1>Access Denied</h1>
      <p>You do not have permission to access this page.</p>
      <p>Administrator role is required.</p>
      <a mat-raised-button color="primary" routerLink="/dashboard">
        Return to Dashboard
      </a>
    </div>
  `,
  styles: [`
    .access-denied-page {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      gap: 16px;
      text-align: center;
      padding: 24px;
    }
    .access-icon {
      font-size: 72px;
      width: 72px;
      height: 72px;
      color: var(--danger, #ef4444);
    }
    h1 {
      font-family: 'Syne', sans-serif;
      font-size: 32px;
      font-weight: 800;
      color: var(--navy, #1A2255);
      margin: 0;
    }
    p {
      color: #6b7280;
      margin: 0;
    }
  `],
})
export class AccessDeniedComponent {}

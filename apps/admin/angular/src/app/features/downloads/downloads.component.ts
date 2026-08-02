import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';

interface DownloadCard {
  title: string;
  description: string;
  icon: string;
  fileName: string;
  downloadPath: string;
  platform: string;
}

@Component({
  selector: 'app-downloads',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Downloads</h1>
      <p class="page-subtitle">Download ASPS components for installation on protected devices.</p>
    </div>

    <div class="downloads-grid">
      @for (card of downloadCards; track card.fileName) {
        <mat-card class="download-card">
          <mat-card-header>
            <mat-icon mat-card-avatar aria-hidden="true" class="card-icon">{{ card.icon }}</mat-icon>
            <mat-card-title>{{ card.title }}</mat-card-title>
            <mat-card-subtitle>{{ card.platform }}</mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <p class="card-description">{{ card.description }}</p>
          </mat-card-content>
          <mat-card-actions>
            <a
              mat-flat-button
              color="primary"
              [href]="card.downloadPath"
              [download]="card.fileName"
              [attr.aria-label]="'Download ' + card.title">
              <mat-icon aria-hidden="true">download</mat-icon>
              Download
            </a>
          </mat-card-actions>
        </mat-card>
      }
    </div>
  `,
  styles: [`
    .downloads-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 20px;
    }
    .download-card { height: 100%; }
    .card-icon { font-size: 28px; height: 28px; width: 28px; }
    .card-description {
      font-size: 14px;
      color: var(--text-secondary, #666);
      line-height: 1.5;
      margin: 8px 0 0;
    }
    mat-card-actions { padding: 8px 8px 12px; }
    a[mat-flat-button] { text-decoration: none; }
  `],
})
export class DownloadsComponent {
  readonly downloadCards: DownloadCard[] = [
    {
      title: 'Desktop Agent',
      description:
        'The ASPS Desktop Agent monitors the protected user\'s Windows PC in real time and sends alerts to the ASPS backend. Install this on every device you want to protect.',
      icon: 'computer',
      fileName: 'asps-desktop-agent-setup.exe',
      downloadPath: '/downloads/asps-desktop-agent-setup.exe',
      platform: 'Windows 10 / 11',
    },
    {
      title: 'Browser Extension',
      description:
        'The ASPS Browser Extension for Chrome detects phishing sites and suspicious links in real time as the user browses. Install from the Chrome Web Store or sideload the CRX.',
      icon: 'extension',
      fileName: 'asps-extension.crx',
      downloadPath: '/downloads/asps-extension.crx',
      platform: 'Google Chrome / Chromium',
    },
  ];
}

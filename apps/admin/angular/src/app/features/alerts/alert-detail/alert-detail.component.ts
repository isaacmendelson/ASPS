import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { SeverityBadgePipe } from '@shared/pipes/severity-badge.pipe';
import { AlertsApiService } from '../services/alerts-api.service';
import { AlertDto } from '@core/models/alert.model';
import { AnalysisResult } from '@core/models/analysis.model';

@Component({
  selector: 'app-alert-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatProgressBarModule,
    EmptyStateComponent,
    SeverityBadgePipe,
  ],
  templateUrl: './alert-detail.component.html',
  styleUrls: ['./alert-detail.component.scss'],
})
export class AlertDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(AlertsApiService);

  // ── Route params ────────────────────────────────────────────────────────────
  private keyType = '';
  private keyValue = '';

  // ── State signals ────────────────────────────────────────────────────────────
  loading = signal(true);
  analysisLoading = signal(false);
  alert = signal<AlertDto | null>(null);
  analysis = signal<AnalysisResult | null>(null);

  ngOnInit(): void {
    const params = this.route.snapshot.params;
    this.keyType = params['keyType'] as string;
    this.keyValue = params['keyValue'] as string;
    this.loadAlert();
  }

  private loadAlert(): void {
    this.loading.set(true);
    this.api.getByKey(this.keyType, this.keyValue).subscribe({
      next: (alert) => {
        this.alert.set(alert);
        this.loading.set(false);
        this.loadAnalysis();
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  private loadAnalysis(): void {
    this.analysisLoading.set(true);
    this.api.getAnalysis(this.keyType, this.keyValue).subscribe({
      next: (result) => {
        this.analysis.set(result);
        this.analysisLoading.set(false);
      },
      error: () => {
        this.analysisLoading.set(false);
      },
    });
  }

  severityClass(priority: string | undefined): string {
    switch ((priority ?? '').toLowerCase()) {
      case 'critical':
      case 'high': return 'sev-badge high';
      case 'medium': return 'sev-badge medium';
      case 'low': return 'sev-badge low';
      default: return 'sev-badge';
    }
  }
}

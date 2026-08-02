import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { AnalysisApiService } from '../services/analysis-api.service';
import { AnalysisResult } from '@core/models/analysis.model';

@Component({
  selector: 'app-analysis-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './analysis-detail.component.html',
  styleUrls: ['./analysis-detail.component.scss'],
})
export class AnalysisDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(AnalysisApiService);

  // ── State signals ────────────────────────────────────────────────────────────
  loading = signal(true);
  result = signal<AnalysisResult | null>(null);

  /** Pretty-printed JSON, null if jsonValue is empty or unparseable. */
  prettyJson = signal<string | null>(null);

  ngOnInit(): void {
    const params = this.route.snapshot.params;
    const keyType = params['keyType'] as string;
    const keyValue = params['keyValue'] as string;

    this.api.getByKey(keyType, keyValue).subscribe({
      next: (res) => {
        this.result.set(res);
        this.loading.set(false);
        if (res.jsonValue) {
          try {
            this.prettyJson.set(JSON.stringify(JSON.parse(res.jsonValue), null, 2));
          } catch {
            this.prettyJson.set(res.jsonValue);
          }
        }
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}

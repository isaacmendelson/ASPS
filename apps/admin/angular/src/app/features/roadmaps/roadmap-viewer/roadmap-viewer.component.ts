import { Component, OnInit, OnDestroy, ViewChild, ElementRef, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { ConfirmDialogService } from '@shared/components/confirm-dialog/confirm-dialog.service';
import { NotificationService } from '@core/services/notification.service';
import { RoadmapsApiService } from '../services/roadmaps-api.service';
import { RoadmapsStateService } from '../services/roadmaps-state.service';
import { Roadmap } from '@core/models/roadmap.model';

/**
 * Read-only, full-page viewer for a single roadmap.
 *
 * Embeds the existing vanilla-JS roadmap SPA (journey map, matrix, statistics —
 * same bundle used by the Razor Pages admin) via an iframe. Roadmap data is
 * fetched here and handed to the iframe with postMessage once both the HTTP
 * call and the iframe's own "viewer-ready" signal have completed; the iframe
 * never talks to the API directly and save()/markDirty() inside it are no-ops.
 */
@Component({
  selector: 'app-roadmap-viewer',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatIconModule,
    MatButtonModule,
    MatProgressBarModule,
    EmptyStateComponent,
  ],
  templateUrl: './roadmap-viewer.component.html',
  styleUrls: ['./roadmap-viewer.component.scss'],
})
export class RoadmapViewerComponent implements OnInit, OnDestroy {
  @ViewChild('viewerFrame') viewerFrame?: ElementRef<HTMLIFrameElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(RoadmapsApiService);
  private readonly state = inject(RoadmapsStateService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notification = inject(NotificationService);

  readonly iframeSrc: SafeResourceUrl =
    this.sanitizer.bypassSecurityTrustResourceUrl('assets/roadmap/viewer.html');

  // ── State signals ────────────────────────────────────────────────────────────
  loading = signal(true);
  roadmap = signal<Roadmap | null>(null);
  error = signal<string | null>(null);
  archiving = signal(false);

  private viewerReady = false;

  private readonly onMessage = (event: MessageEvent): void => {
    if (event.data?.type !== 'viewer-ready') return;
    this.viewerReady = true;
    this.postDataToViewer();
  };

  ngOnInit(): void {
    window.addEventListener('message', this.onMessage);

    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (!id) {
      this.loading.set(false);
      this.error.set('Invalid roadmap ID.');
      return;
    }

    this.api.getById(id).subscribe({
      next: (roadmap) => {
        this.roadmap.set(roadmap);
        this.loading.set(false);
        this.postDataToViewer();
      },
      error: (err: { error?: { message?: string }; message?: string }) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Failed to load roadmap.');
      },
    });
  }

  ngOnDestroy(): void {
    window.removeEventListener('message', this.onMessage);
  }

  /** Fired on every iframe (re)load, including back/forward cache restores. */
  onIframeLoad(): void {
    this.postDataToViewer();
  }

  onArchive(): void {
    const roadmap = this.roadmap();
    if (!roadmap) return;

    this.confirmDialog
      .confirm({
        title: 'Archive Roadmap',
        message: `Archive roadmap "${roadmap.name}"? It will be hidden from the default view.`,
        confirmText: 'Archive',
        confirmColor: 'warn',
      })
      .subscribe((confirmed) => {
        if (!confirmed) return;
        this.archiving.set(true);
        this.state.archive(roadmap.id).subscribe({
          next: () => {
            this.archiving.set(false);
            this.roadmap.update((r) => (r ? { ...r, isArchived: true } : r));
            this.notification.success(
              'Roadmap Archived',
              `"${roadmap.name}" has been archived.`
            );
          },
          error: (err: { error?: { message?: string }; message?: string }) => {
            this.archiving.set(false);
            this.notification.error(
              'Archive Failed',
              err?.error?.message ?? err?.message ?? 'Could not archive roadmap.'
            );
          },
        });
      });
  }

  private postDataToViewer(): void {
    const roadmap = this.roadmap();
    const iframeWindow = this.viewerFrame?.nativeElement.contentWindow;
    if (!roadmap || !iframeWindow || !this.viewerReady) return;
    iframeWindow.postMessage({ type: 'roadmap-data', roadmap }, '*');
  }
}

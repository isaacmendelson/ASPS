import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AlertBadgeService } from '../../../core/services/alert-badge.service';

@Component({
  selector: 'app-alerts-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <h1 class="page-title">Device Alerts</h1>
    </div>
  `,
})
export class AlertsListComponent implements OnInit {
  private alertBadge = inject(AlertBadgeService);

  ngOnInit(): void {
    // Clear badge when user navigates to this page
    this.alertBadge.reset();
  }
}

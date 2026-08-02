import { Injectable, signal } from '@angular/core';

/**
 * Cross-component signal for the live alert count badge in the sidebar.
 * Incremented by SignalRService on DeviceAlert notifications.
 * Reset by AlertsListComponent on navigation.
 */
@Injectable({ providedIn: 'root' })
export class AlertBadgeService {
  private readonly _newAlertCount = signal(0);
  readonly newAlertCount = this._newAlertCount.asReadonly();

  increment(): void {
    this._newAlertCount.update(n => n + 1);
  }

  reset(): void {
    this._newAlertCount.set(0);
  }
}

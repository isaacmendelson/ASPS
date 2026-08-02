import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private snackBar = inject(MatSnackBar);

  private show(
    message: string,
    panelClass: string,
    duration = 4000
  ): void {
    const config: MatSnackBarConfig = {
      duration,
      panelClass: [panelClass],
      horizontalPosition: 'right',
      verticalPosition: 'bottom',
    };
    this.snackBar.open(message, 'Dismiss', config);
  }

  success(title: string, message: string): void {
    this.show(`${title}: ${message}`, 'snack-success');
  }

  error(title: string, message: string): void {
    this.show(`${title}: ${message}`, 'snack-error', 6000);
  }

  warn(title: string, message: string): void {
    this.show(`${title}: ${message}`, 'snack-warn', 5000);
  }

  info(title: string, message: string): void {
    this.show(`${title}: ${message}`, 'snack-info');
  }
}

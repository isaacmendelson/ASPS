import { Pipe, PipeTransform } from '@angular/core';

export interface SeverityBadge {
  label: string;
  cssClass: string;
}

/**
 * Transforms a severity string to a { label, cssClass } object for display.
 * CSS classes match the .sev-badge styles from the existing Razor admin.
 */
@Pipe({ name: 'severityBadge', standalone: true, pure: true })
export class SeverityBadgePipe implements PipeTransform {
  transform(value: string | null | undefined): SeverityBadge {
    const level = (value ?? '').toLowerCase();
    switch (level) {
      case 'critical':
      case 'high':
        return { label: value ?? 'High', cssClass: 'sev-badge high' };
      case 'medium':
        return { label: value ?? 'Medium', cssClass: 'sev-badge medium' };
      case 'low':
        return { label: value ?? 'Low', cssClass: 'sev-badge low' };
      default:
        return { label: value || 'Unknown', cssClass: 'sev-badge' };
    }
  }
}

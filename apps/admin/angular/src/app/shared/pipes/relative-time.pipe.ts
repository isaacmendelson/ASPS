import { Pipe, PipeTransform } from '@angular/core';

/**
 * Transforms an ISO 8601 date string into a human-readable relative time,
 * e.g. "2 hours ago", "3 days ago", "just now".
 */
@Pipe({ name: 'relativeTime', standalone: true, pure: true })
export class RelativeTimePipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) {
      return '';
    }

    const date = value instanceof Date ? value : new Date(value);
    if (isNaN(date.getTime())) {
      return '';
    }

    const now = Date.now();
    const diffMs = now - date.getTime();
    const diffSeconds = Math.floor(diffMs / 1000);

    if (diffSeconds < 30) {
      return 'just now';
    }
    if (diffSeconds < 60) {
      return `${diffSeconds} seconds ago`;
    }

    const diffMinutes = Math.floor(diffSeconds / 60);
    if (diffMinutes < 60) {
      return diffMinutes === 1 ? '1 minute ago' : `${diffMinutes} minutes ago`;
    }

    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) {
      return diffHours === 1 ? '1 hour ago' : `${diffHours} hours ago`;
    }

    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 30) {
      return diffDays === 1 ? '1 day ago' : `${diffDays} days ago`;
    }

    const diffMonths = Math.floor(diffDays / 30);
    if (diffMonths < 12) {
      return diffMonths === 1 ? '1 month ago' : `${diffMonths} months ago`;
    }

    const diffYears = Math.floor(diffMonths / 12);
    return diffYears === 1 ? '1 year ago' : `${diffYears} years ago`;
  }
}

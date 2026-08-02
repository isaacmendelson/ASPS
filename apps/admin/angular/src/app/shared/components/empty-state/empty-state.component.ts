import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './empty-state.component.html',
})
export class EmptyStateComponent {
  @Input() icon = 'inbox';
  @Input() title = 'No data';
  @Input() message = 'Nothing to display here yet.';
}

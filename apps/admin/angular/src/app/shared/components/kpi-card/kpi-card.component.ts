import { Component, Input, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

export type KpiColor = 'blue' | 'green' | 'amber' | 'red';

@Component({
  selector: 'app-kpi-card',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './kpi-card.component.html',
  styleUrls: ['./kpi-card.component.scss'],
})
export class KpiCardComponent {
  @Input() label = '';
  @Input() value!: Signal<number | string>;
  @Input() meta = '';
  @Input() icon = 'info';
  @Input() color: KpiColor = 'blue';
  @Input() routerLink: string | null = null;
  @Input() loading!: Signal<boolean>;
}

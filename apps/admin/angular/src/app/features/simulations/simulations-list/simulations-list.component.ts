import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-simulations-list',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="page-header"><h1 class="page-title">Simulations</h1></div>`,
})
export class SimulationsListComponent {}

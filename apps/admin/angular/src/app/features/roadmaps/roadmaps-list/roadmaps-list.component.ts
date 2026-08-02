import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-roadmaps-list',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="page-header"><h1 class="page-title">Roadmaps</h1></div>`,
})
export class RoadmapsListComponent {}

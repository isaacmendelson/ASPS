import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-system',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="page-header"><h1 class="page-title">System Settings</h1></div>`,
})
export class SystemComponent {}

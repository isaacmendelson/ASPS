import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-domains-list',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="page-header"><h1 class="page-title">Tracked Domains</h1></div>`,
})
export class DomainsListComponent {}

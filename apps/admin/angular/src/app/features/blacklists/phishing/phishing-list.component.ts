import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-phishing-list',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="page-header"><h1 class="page-title">Phishing Websites</h1></div>`,
})
export class PhishingListComponent {}

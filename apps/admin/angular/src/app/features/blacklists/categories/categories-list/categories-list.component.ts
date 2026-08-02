import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-categories-list',
  standalone: true,
  imports: [CommonModule],
  template: `<div class="page-header"><h1 class="page-title">Website Categories</h1></div>`,
})
export class CategoriesListComponent {}

import { Routes } from '@angular/router';

export const SYSTEM_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./system.component').then(c => c.SystemComponent),
  },
];

import { Routes } from '@angular/router';

export const SIMULATIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./simulations-list/simulations-list.component').then(
        c => c.SimulationsListComponent
      ),
  },
];

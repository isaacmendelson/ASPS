import { Routes } from '@angular/router';

export const SIMULATIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./simulations-list/simulations-list.component').then(
        c => c.SimulationsListComponent
      ),
  },
  {
    path: 'new',
    loadComponent: () =>
      import('./simulation-form/simulation-form.component').then(
        c => c.SimulationFormComponent
      ),
  },
  {
    path: ':keyField/edit',
    loadComponent: () =>
      import('./simulation-form/simulation-form.component').then(
        c => c.SimulationFormComponent
      ),
  },
];

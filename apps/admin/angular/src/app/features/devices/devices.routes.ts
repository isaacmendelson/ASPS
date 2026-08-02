import { Routes } from '@angular/router';

export const DEVICES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./devices-list/devices-list.component').then(
        c => c.DevicesListComponent
      ),
  },
];

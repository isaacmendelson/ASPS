import { Routes } from '@angular/router';

export const DEVICES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./devices-list/devices-list.component').then(
        c => c.DevicesListComponent
      ),
  },
  {
    path: ':keyType/:keyValue',
    loadComponent: () =>
      import('./device-detail/device-detail.component').then(
        c => c.DeviceDetailComponent
      ),
  },
];

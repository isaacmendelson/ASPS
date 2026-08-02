import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then(
            m => m.DASHBOARD_ROUTES
          ),
      },
      {
        path: 'users',
        loadChildren: () =>
          import('./features/users/users.routes').then(m => m.USERS_ROUTES),
      },
      {
        path: 'devices',
        loadChildren: () =>
          import('./features/devices/devices.routes').then(
            m => m.DEVICES_ROUTES
          ),
      },
      {
        path: 'alerts',
        loadChildren: () =>
          import('./features/alerts/alerts.routes').then(
            m => m.ALERTS_ROUTES
          ),
      },
      {
        path: 'analysis',
        loadChildren: () =>
          import('./features/analysis/analysis.routes').then(
            m => m.ANALYSIS_ROUTES
          ),
      },
      {
        path: 'blacklists',
        loadChildren: () =>
          import('./features/blacklists/blacklists.routes').then(
            m => m.BLACKLISTS_ROUTES
          ),
      },
      {
        path: 'roadmaps',
        loadChildren: () =>
          import('./features/roadmaps/roadmaps.routes').then(
            m => m.ROADMAPS_ROUTES
          ),
      },
      {
        path: 'simulations',
        loadChildren: () =>
          import('./features/simulations/simulations.routes').then(
            m => m.SIMULATIONS_ROUTES
          ),
      },
      {
        path: 'system',
        loadChildren: () =>
          import('./features/system/system.routes').then(
            m => m.SYSTEM_ROUTES
          ),
      },
      {
        path: 'downloads',
        loadComponent: () =>
          import('./features/downloads/downloads.component').then(
            c => c.DownloadsComponent
          ),
      },
    ],
  },
  {
    path: 'access-denied',
    loadComponent: () =>
      import('./features/access-denied/access-denied.component').then(
        c => c.AccessDeniedComponent
      ),
  },
  { path: '**', redirectTo: 'dashboard' },
];

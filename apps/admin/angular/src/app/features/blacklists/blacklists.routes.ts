import { Routes } from '@angular/router';

export const BLACKLISTS_ROUTES: Routes = [
  { path: '', redirectTo: 'phishing', pathMatch: 'full' },
  {
    path: 'phishing',
    loadComponent: () =>
      import('./phishing/phishing-list.component').then(
        c => c.PhishingListComponent
      ),
  },
  {
    path: 'phones',
    loadComponent: () =>
      import('./phones/phones-list.component').then(
        c => c.PhonesListComponent
      ),
  },
  {
    path: 'banks',
    loadComponent: () =>
      import('./banks/banks-list.component').then(c => c.BanksListComponent),
  },
  {
    path: 'categories',
    loadComponent: () =>
      import('./categories/categories-list/categories-list.component').then(
        c => c.CategoriesListComponent
      ),
  },
  {
    path: 'domains',
    loadComponent: () =>
      import('./domains/domains-list/domains-list.component').then(
        c => c.DomainsListComponent
      ),
  },
];

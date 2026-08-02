import { Routes } from '@angular/router';

export const USERS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./users-list/users-list.component').then(
        c => c.UsersListComponent
      ),
  },
  {
    path: ':keyType/:keyValue',
    loadComponent: () =>
      import('./user-detail/user-detail.component').then(
        c => c.UserDetailComponent
      ),
  },
];

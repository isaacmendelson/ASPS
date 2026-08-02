import { Routes } from '@angular/router';

export const ANALYSIS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./analysis-list/analysis-list.component').then(
        c => c.AnalysisListComponent
      ),
  },
];

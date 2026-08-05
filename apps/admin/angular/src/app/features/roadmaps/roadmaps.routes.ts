import { Routes } from '@angular/router';

export const ROADMAPS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./roadmaps-list/roadmaps-list.component').then(
        c => c.RoadmapsListComponent
      ),
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./roadmap-viewer/roadmap-viewer.component').then(
        c => c.RoadmapViewerComponent
      ),
  },
];

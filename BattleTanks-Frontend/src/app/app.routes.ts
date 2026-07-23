import { Routes } from '@angular/router';

export const routes: Routes = [
  { 
    path: '', 
    loadComponent: () => import('./components/waiting-room/waiting-room').then(m => m.WaitingRoom) 
  },
  { 
    path: 'game', 
    loadComponent: () => import('./components/game-canvas/game-canvas').then(m => m.GameCanvas) 
  },
];

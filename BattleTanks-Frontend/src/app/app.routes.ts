import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { 
    path: 'login', 
    loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) 
  },
  { 
    path: 'register', 
    loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent) 
  },
  { 
    path: 'room', 
    loadComponent: () => import('./components/waiting-room/waiting-room').then(m => m.WaitingRoom),
    canActivate: [authGuard]
  },
  { 
    path: 'game', 
    loadComponent: () => import('./components/game/game.component').then(m => m.GameComponent),
    canActivate: [authGuard]
  },
];

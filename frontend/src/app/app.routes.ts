import { Routes } from '@angular/router';

import { roleGuard } from './core/auth/auth.guard';
import { Role } from './core/auth/auth.models';
import { DeviceSetup } from './features/auth/device-setup/device-setup';
import { Login } from './features/auth/login/login';
import { StaffLogin } from './features/auth/staff-login/staff-login';

export const routes: Routes = [
  { path: 'login', component: Login },
  { path: 'device-setup', component: DeviceSetup },
  { path: 'staff-login', component: StaffLogin },
  {
    path: 'admin',
    canActivate: [roleGuard(Role.BranchManager)],
    loadComponent: () => import('./features/admin/admin-placeholder').then((m) => m.AdminPlaceholder),
  },
  {
    path: 'pos',
    canActivate: [roleGuard(Role.Staff)],
    loadComponent: () => import('./features/pos/pos-placeholder').then((m) => m.PosPlaceholder),
  },
];

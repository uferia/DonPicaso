import { Routes } from '@angular/router';

import { branchSessionGuard, roleGuard } from './core/auth/auth.guard';
import { Role } from './core/auth/auth.models';
import { DeviceSetup } from './features/auth/device-setup/device-setup';
import { Login } from './features/auth/login/login';
import { StaffLogin } from './features/auth/staff-login/staff-login';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'device-setup', component: DeviceSetup },
  { path: 'staff-login', component: StaffLogin },
  {
    path: 'admin',
    canActivate: [roleGuard(Role.BranchManager)],
    loadComponent: () => import('./features/admin/admin-shell/admin-shell').then((m) => m.AdminShell),
    children: [
      {
        path: '',
        loadComponent: () => import('./features/admin/admin-redirect/admin-redirect').then((m) => m.AdminRedirect),
      },
      {
        path: 'brands',
        loadComponent: () => import('./features/admin/brands/brands-list/brands-list').then((m) => m.BrandsList),
      },
      {
        path: 'brands/new',
        loadComponent: () => import('./features/admin/brands/brand-form/brand-form').then((m) => m.BrandForm),
      },
      {
        path: 'brands/:brandId',
        loadComponent: () => import('./features/admin/brands/brand-form/brand-form').then((m) => m.BrandForm),
      },
      {
        path: 'brands/:brandId/branches',
        loadComponent: () => import('./features/admin/branches/branches-list/branches-list').then((m) => m.BranchesList),
      },
      {
        path: 'brands/:brandId/branches/new',
        loadComponent: () => import('./features/admin/branches/branch-form/branch-form').then((m) => m.BranchForm),
      },
      {
        path: 'brands/:brandId/branches/:branchId',
        loadComponent: () => import('./features/admin/branches/branch-form/branch-form').then((m) => m.BranchForm),
      },
      {
        path: 'branches/:branchId/users',
        loadComponent: () => import('./features/admin/users/users-list/users-list').then((m) => m.UsersList),
      },
      {
        path: 'branches/:branchId/users/new',
        loadComponent: () => import('./features/admin/users/user-form/user-form').then((m) => m.UserForm),
      },
      {
        path: 'branches/:branchId/users/:userId',
        loadComponent: () => import('./features/admin/users/user-form/user-form').then((m) => m.UserForm),
      },
    ],
  },
  {
    path: 'pos',
    canActivate: [branchSessionGuard],
    loadComponent: () => import('./features/pos/pos-shell/pos-shell').then((m) => m.PosShell),
  },
];

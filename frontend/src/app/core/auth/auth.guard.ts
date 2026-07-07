import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { Role } from './auth.models';
import { AuthService } from './auth.service';

const ROLE_RANK: Record<Role, number> = {
  [Role.Corporate]: 0,
  [Role.BrandOwner]: 1,
  [Role.BranchManager]: 2,
  [Role.Staff]: 3,
};

export function roleGuard(minimumRole: Role): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    const user = authService.currentUser();
    if (user && ROLE_RANK[user.role] <= ROLE_RANK[minimumRole]) {
      return true;
    }

    return router.parseUrl('/login');
  };
}

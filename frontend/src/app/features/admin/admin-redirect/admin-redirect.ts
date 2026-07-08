import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';

import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-admin-redirect',
  template: '',
})
export class AdminRedirect implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    const user = this.authService.currentUser();
    if (!user) {
      void this.router.navigateByUrl('/login');
      return;
    }

    switch (user.role) {
      case Role.Corporate:
        void this.router.navigateByUrl('/admin/brands');
        break;
      case Role.BrandOwner:
        void this.router.navigateByUrl(`/admin/brands/${user.brandId}/branches`);
        break;
      default:
        void this.router.navigateByUrl(`/admin/branches/${user.branchId}/users?brandId=${user.brandId}`);
        break;
    }
  }
}

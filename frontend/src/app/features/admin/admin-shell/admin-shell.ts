import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';

import { Role } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-admin-shell',
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ButtonModule,
    ConfirmDialogModule,
    TagModule,
    ToastModule,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './admin-shell.html',
  styleUrl: './admin-shell.scss',
})
export class AdminShell {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly currentUser = this.authService.currentUser;
  protected readonly Role = Role;

  protected async logout(): Promise<void> {
    await this.authService.logout();
    // Not awaited: in tests (and any router config missing '/login')
    // navigateByUrl's promise rejects on no-match, which would otherwise
    // propagate out of this method. Matches the existing pattern in
    // staff-login.ts and pos-shell.ts for the same reason.
    void this.router.navigateByUrl('/login');
  }
}

import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { Role } from '../../../../core/auth/auth.models';
import { UsersService } from '../../../../core/admin/users.service';

@Component({
  selector: 'app-user-form',
  imports: [FormsModule],
  templateUrl: './user-form.html',
  styleUrl: './user-form.scss',
})
export class UserForm implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly returnBranchId = this.route.snapshot.paramMap.get('branchId')!;

  protected readonly Role = Role;
  protected readonly userId = signal<string | null>(null);

  protected displayName = '';
  protected role: Role = Role.Staff;
  protected email = '';
  protected brandId = this.route.snapshot.queryParamMap.get('brandId') ?? '';
  protected branchId = this.returnBranchId;
  protected password = '';
  protected pin = '';

  protected newPassword = '';
  protected newPin = '';

  protected readonly errorMessage = signal<string | null>(null);
  protected readonly credentialResetMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  protected readonly isStaff = computed(() => this.role === Role.Staff);

  async ngOnInit(): Promise<void> {
    const userId = this.route.snapshot.paramMap.get('userId');
    if (!userId) {
      return;
    }

    this.userId.set(userId);
    const user = await this.usersService.get(userId);
    this.displayName = user.displayName;
    this.role = user.role;
    this.email = user.email ?? '';
    this.brandId = user.brandId ?? '';
    this.branchId = user.branchId ?? '';
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    const scopesToABranch = this.role === Role.BranchManager || this.role === Role.Staff;

    try {
      const userId = this.userId();
      if (userId) {
        await this.usersService.update(userId, {
          displayName: this.displayName,
          role: this.role,
          brandId: this.role === Role.Corporate ? null : this.brandId || null,
          branchId: scopesToABranch ? this.branchId || null : null,
          email: this.isStaff() ? null : this.email || null,
          newPassword: this.newPassword || null,
          newPin: this.newPin || null,
        });
      } else {
        await this.usersService.create({
          email: this.isStaff() ? null : this.email || null,
          displayName: this.displayName,
          role: this.role,
          brandId: this.role === Role.Corporate ? null : this.brandId || null,
          branchId: scopesToABranch ? this.branchId || null : null,
          password: this.isStaff() ? null : this.password || null,
          pin: this.isStaff() ? this.pin || null : null,
        });
      }

      await this.router.navigateByUrl(`/admin/branches/${this.returnBranchId}/users`);
    } catch {
      this.errorMessage.set('Could not save this user.');
    } finally {
      this.isSubmitting.set(false);
    }
  }

  async resetCredential(): Promise<void> {
    const userId = this.userId();
    if (!userId) {
      return;
    }

    this.credentialResetMessage.set(null);

    try {
      await this.usersService.resetCredential(userId, {
        newPassword: this.isStaff() ? null : this.newPassword || null,
        newPin: this.isStaff() ? this.newPin || null : null,
      });
      this.credentialResetMessage.set('Credential updated.');
      this.newPassword = '';
      this.newPin = '';
    } catch {
      this.credentialResetMessage.set('Could not reset the credential.');
    }
  }
}

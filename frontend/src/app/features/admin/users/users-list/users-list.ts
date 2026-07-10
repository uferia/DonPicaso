import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { AdminUser } from '../../../../core/admin/admin.models';
import { UsersService } from '../../../../core/admin/users.service';

@Component({
  selector: 'app-users-list',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, TableModule, TagModule],
  templateUrl: './users-list.html',
  styleUrl: './users-list.scss',
})
export class UsersList implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  protected readonly branchId = this.route.snapshot.paramMap.get('branchId')!;
  protected readonly brandIdQueryParam = this.route.snapshot.queryParamMap.get('brandId');
  protected readonly users = signal<AdminUser[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly search = signal('');

  protected readonly filteredUsers = computed(() => {
    const term = this.search().trim().toLowerCase();
    const users = this.users();
    if (!term) {
      return users;
    }
    return users.filter(
      (user) =>
        user.displayName.toLowerCase().includes(term) ||
        user.role.toLowerCase().includes(term) ||
        (user.email ?? '').toLowerCase().includes(term),
    );
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set(false);
    try {
      this.users.set(await this.usersService.list({ branchId: this.branchId }));
    } catch {
      this.loadError.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  confirmToggle(user: AdminUser): void {
    const confirmation: Confirmation = {
      header: user.isActive ? 'Deactivate user' : 'Reactivate user',
      message: user.isActive
        ? `Deactivate ${user.displayName}? They won't be able to sign in.`
        : `Reactivate ${user.displayName}?`,
      acceptButtonProps: {
        label: user.isActive ? 'Deactivate' : 'Reactivate',
        severity: user.isActive ? 'danger' : 'primary',
      },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.toggleActive(user),
    };
    this.confirmationService.confirm(confirmation);
  }

  async toggleActive(user: AdminUser): Promise<void> {
    try {
      const updated = user.isActive
        ? await this.usersService.deactivate(user.id)
        : await this.usersService.reactivate(user.id);

      this.users.update((users) => users.map((u) => (u.id === updated.id ? updated : u)));
      this.messageService.add({
        severity: 'success',
        summary: updated.isActive ? 'User reactivated' : 'User deactivated',
      });
    } catch {
      this.messageService.add({ severity: 'error', summary: "Couldn't update the user" });
    }
  }
}

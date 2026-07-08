import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AdminUser } from '../../../../core/admin/admin.models';
import { UsersService } from '../../../../core/admin/users.service';

@Component({
  selector: 'app-users-list',
  imports: [RouterLink],
  templateUrl: './users-list.html',
  styleUrl: './users-list.scss',
})
export class UsersList implements OnInit {
  private readonly usersService = inject(UsersService);
  private readonly route = inject(ActivatedRoute);

  protected readonly branchId = this.route.snapshot.paramMap.get('branchId')!;
  protected readonly brandIdQueryParam = this.route.snapshot.queryParamMap.get('brandId');
  protected readonly users = signal<AdminUser[]>([]);

  async ngOnInit(): Promise<void> {
    this.users.set(await this.usersService.list({ branchId: this.branchId }));
  }

  async toggleActive(user: AdminUser): Promise<void> {
    const updated = user.isActive
      ? await this.usersService.deactivate(user.id)
      : await this.usersService.reactivate(user.id);

    this.users.update((users) => users.map((u) => (u.id === updated.id ? updated : u)));
  }
}

import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { Branch } from '../../../../core/admin/admin.models';
import { BranchesService } from '../../../../core/admin/branches.service';

@Component({
  selector: 'app-branches-list',
  imports: [RouterLink],
  templateUrl: './branches-list.html',
  styleUrl: './branches-list.scss',
})
export class BranchesList implements OnInit {
  private readonly branchesService = inject(BranchesService);
  private readonly route = inject(ActivatedRoute);

  protected readonly brandId = this.route.snapshot.paramMap.get('brandId')!;
  protected readonly branches = signal<Branch[]>([]);

  async ngOnInit(): Promise<void> {
    this.branches.set(await this.branchesService.list(this.brandId));
  }

  async toggleActive(branch: Branch): Promise<void> {
    const updated = branch.isActive
      ? await this.branchesService.deactivate(this.brandId, branch.id)
      : await this.branchesService.reactivate(this.brandId, branch.id);

    this.branches.update((branches) => branches.map((b) => (b.id === updated.id ? updated : b)));
  }
}

import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { Branch } from '../../../../core/admin/admin.models';
import { BranchesService } from '../../../../core/admin/branches.service';

@Component({
  selector: 'app-branches-list',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, TableModule, TagModule],
  templateUrl: './branches-list.html',
  styleUrl: './branches-list.scss',
})
export class BranchesList implements OnInit {
  private readonly branchesService = inject(BranchesService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);

  protected readonly brandId = this.route.snapshot.paramMap.get('brandId')!;
  protected readonly branches = signal<Branch[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly search = signal('');

  protected readonly filteredBranches = computed(() => {
    const term = this.search().trim().toLowerCase();
    const branches = this.branches();
    return term ? branches.filter((branch) => branch.name.toLowerCase().includes(term)) : branches;
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set(false);
    try {
      this.branches.set(await this.branchesService.list(this.brandId));
    } catch {
      this.loadError.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  confirmToggle(branch: Branch): void {
    const confirmation: Confirmation = {
      header: branch.isActive ? 'Deactivate branch' : 'Reactivate branch',
      message: branch.isActive
        ? `Deactivate ${branch.name}? Staff there won't be able to sign in.`
        : `Reactivate ${branch.name}?`,
      acceptButtonProps: {
        label: branch.isActive ? 'Deactivate' : 'Reactivate',
        severity: branch.isActive ? 'danger' : 'primary',
      },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.toggleActive(branch),
    };
    this.confirmationService.confirm(confirmation);
  }

  async toggleActive(branch: Branch): Promise<void> {
    try {
      const updated = branch.isActive
        ? await this.branchesService.deactivate(this.brandId, branch.id)
        : await this.branchesService.reactivate(this.brandId, branch.id);

      this.branches.update((branches) => branches.map((b) => (b.id === updated.id ? updated : b)));
      this.messageService.add({
        severity: 'success',
        summary: updated.isActive ? 'Branch reactivated' : 'Branch deactivated',
      });
    } catch {
      this.messageService.add({ severity: 'error', summary: "Couldn't update the branch" });
    }
  }
}

import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';

import { BranchesService } from '../../../../core/admin/branches.service';

@Component({
  selector: 'app-branch-form',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule],
  templateUrl: './branch-form.html',
  styleUrl: './branch-form.scss',
})
export class BranchForm implements OnInit {
  private readonly branchesService = inject(BranchesService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly brandId = this.route.snapshot.paramMap.get('brandId')!;
  protected readonly branchId = signal<string | null>(null);
  protected name = '';
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  async ngOnInit(): Promise<void> {
    const branchId = this.route.snapshot.paramMap.get('branchId');
    if (!branchId) {
      return;
    }

    this.branchId.set(branchId);
    const branch = await this.branchesService.get(this.brandId, branchId);
    this.name = branch.name;
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      const branchId = this.branchId();
      if (branchId) {
        await this.branchesService.update(this.brandId, branchId, this.name);
      } else {
        await this.branchesService.create(this.brandId, this.name);
      }
      this.messageService.add({
        severity: 'success',
        summary: branchId ? 'Branch updated' : 'Branch created',
      });
      await this.router.navigateByUrl(`/admin/brands/${this.brandId}/branches`);
    } catch {
      this.errorMessage.set('Could not save this branch.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}

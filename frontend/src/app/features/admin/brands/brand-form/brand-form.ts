import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';

import { BrandsService } from '../../../../core/admin/brands.service';

@Component({
  selector: 'app-brand-form',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule],
  templateUrl: './brand-form.html',
  styleUrl: './brand-form.scss',
})
export class BrandForm implements OnInit {
  private readonly brandsService = inject(BrandsService);
  private readonly messageService = inject(MessageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly brandId = signal<string | null>(null);
  protected name = '';
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly isSubmitting = signal(false);

  async ngOnInit(): Promise<void> {
    const brandId = this.route.snapshot.paramMap.get('brandId');
    if (!brandId) {
      return;
    }

    this.brandId.set(brandId);
    const brand = await this.brandsService.get(brandId);
    this.name = brand.name;
  }

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);

    try {
      const brandId = this.brandId();
      if (brandId) {
        await this.brandsService.update(brandId, this.name);
      } else {
        await this.brandsService.create(this.name);
      }
      this.messageService.add({
        severity: 'success',
        summary: brandId ? 'Brand updated' : 'Brand created',
      });
      await this.router.navigateByUrl('/admin/brands');
    } catch {
      this.errorMessage.set('Could not save this brand.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}

import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { Brand } from '../../../../core/admin/admin.models';
import { BrandsService } from '../../../../core/admin/brands.service';

@Component({
  selector: 'app-brands-list',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, MessageModule, TableModule, TagModule],
  templateUrl: './brands-list.html',
  styleUrl: './brands-list.scss',
})
export class BrandsList implements OnInit {
  private readonly brandsService = inject(BrandsService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly brands = signal<Brand[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly search = signal('');

  protected readonly filteredBrands = computed(() => {
    const term = this.search().trim().toLowerCase();
    const brands = this.brands();
    return term ? brands.filter((brand) => brand.name.toLowerCase().includes(term)) : brands;
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected async load(): Promise<void> {
    this.isLoading.set(true);
    this.loadError.set(false);
    try {
      this.brands.set(await this.brandsService.list());
    } catch {
      this.loadError.set(true);
    } finally {
      this.isLoading.set(false);
    }
  }

  confirmToggle(brand: Brand): void {
    const confirmation: Confirmation = {
      header: brand.isActive ? 'Deactivate brand' : 'Reactivate brand',
      message: brand.isActive
        ? `Deactivate ${brand.name}? Its branches and staff won't be able to sign in.`
        : `Reactivate ${brand.name}?`,
      acceptButtonProps: {
        label: brand.isActive ? 'Deactivate' : 'Reactivate',
        severity: brand.isActive ? 'danger' : 'primary',
      },
      rejectButtonProps: { label: 'Cancel', outlined: true },
      accept: () => void this.toggleActive(brand),
    };
    this.confirmationService.confirm(confirmation);
  }

  async toggleActive(brand: Brand): Promise<void> {
    try {
      const updated = brand.isActive
        ? await this.brandsService.deactivate(brand.id)
        : await this.brandsService.reactivate(brand.id);

      this.brands.update((brands) => brands.map((b) => (b.id === updated.id ? updated : b)));
      this.messageService.add({
        severity: 'success',
        summary: updated.isActive ? 'Brand reactivated' : 'Brand deactivated',
      });
    } catch {
      this.messageService.add({ severity: 'error', summary: "Couldn't update the brand" });
    }
  }
}

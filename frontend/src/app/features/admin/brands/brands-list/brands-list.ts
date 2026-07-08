import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Brand } from '../../../../core/admin/admin.models';
import { BrandsService } from '../../../../core/admin/brands.service';

@Component({
  selector: 'app-brands-list',
  imports: [RouterLink],
  templateUrl: './brands-list.html',
  styleUrl: './brands-list.scss',
})
export class BrandsList implements OnInit {
  private readonly brandsService = inject(BrandsService);

  protected readonly brands = signal<Brand[]>([]);

  async ngOnInit(): Promise<void> {
    this.brands.set(await this.brandsService.list());
  }

  async toggleActive(brand: Brand): Promise<void> {
    const updated = brand.isActive
      ? await this.brandsService.deactivate(brand.id)
      : await this.brandsService.reactivate(brand.id);

    this.brands.update((brands) => brands.map((b) => (b.id === updated.id ? updated : b)));
  }
}

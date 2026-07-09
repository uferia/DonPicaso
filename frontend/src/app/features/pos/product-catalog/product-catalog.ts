import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';

import { MenuCategory } from '../../../core/menu/menu.models';
import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';

@Component({
  selector: 'app-product-catalog',
  imports: [CurrencyPipe, FormsModule, InputTextModule],
  templateUrl: './product-catalog.html',
  styleUrl: './product-catalog.scss',
})
export class ProductCatalog {
  protected readonly menu = inject(MenuService);
  protected readonly cart = inject(CartService);

  protected readonly searchTerm = signal('');
  protected readonly selectedCategoryId = signal<string | null>(null);

  protected readonly selectedCategory = computed<MenuCategory | null>(() => {
    const categories = this.menu.categories();
    return categories.find((c) => c.id === this.selectedCategoryId()) ?? categories[0] ?? null;
  });

  protected readonly filteredProducts = computed(() => {
    const category = this.selectedCategory();
    if (!category) {
      return [];
    }
    const term = this.searchTerm().trim().toLowerCase();
    return term
      ? category.products.filter((p) => p.name.toLowerCase().includes(term))
      : category.products;
  });

  /** Placeholder tile art until real product images exist (deferred). */
  protected initials(name: string): string {
    return name
      .split(' ')
      .slice(0, 2)
      .map((word) => word[0])
      .join('')
      .toUpperCase();
  }
}

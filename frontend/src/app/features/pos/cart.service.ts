import { Injectable, computed, inject, signal } from '@angular/core';

import { MenuProduct } from '../../core/menu/menu.models';
import { MenuService } from '../../core/menu/menu.service';

export interface CartLine {
  product: MenuProduct;
  quantity: number;
}

/**
 * Half-up to 2 decimals — must stay in lockstep with RoundMoney() in
 * CreateOrderCommandValidator, which re-derives and rejects drifted math.
 */
export function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

/**
 * The active order being built. Provided by PosShell (not root) so a fresh
 * cart exists per POS session and tests get isolated instances.
 */
@Injectable()
export class CartService {
  private readonly menu = inject(MenuService);

  readonly lines = signal<CartLine[]>([]);
  readonly discountPercent = signal(0);

  readonly subtotal = computed(() =>
    roundMoney(this.lines().reduce((sum, line) => sum + line.quantity * line.product.price, 0)),
  );

  readonly discountAmount = computed(() =>
    roundMoney((this.subtotal() * this.discountPercent()) / 100),
  );

  readonly taxAmount = computed(() =>
    roundMoney(((this.subtotal() - this.discountAmount()) * this.menu.taxRatePercent()) / 100),
  );

  readonly total = computed(() =>
    roundMoney(this.subtotal() - this.discountAmount() + this.taxAmount()),
  );

  add(product: MenuProduct): void {
    const existing = this.lines().find((line) => line.product.id === product.id);
    if (existing) {
      this.increment(product.id);
      return;
    }
    this.lines.update((lines) => [...lines, { product, quantity: 1 }]);
  }

  increment(productId: string): void {
    this.lines.update((lines) =>
      lines.map((line) =>
        line.product.id === productId ? { ...line, quantity: line.quantity + 1 } : line,
      ),
    );
  }

  decrement(productId: string): void {
    this.lines.update((lines) =>
      lines
        .map((line) =>
          line.product.id === productId ? { ...line, quantity: line.quantity - 1 } : line,
        )
        .filter((line) => line.quantity > 0),
    );
  }

  remove(productId: string): void {
    this.lines.update((lines) => lines.filter((line) => line.product.id !== productId));
  }

  setDiscountPercent(value: number): void {
    this.discountPercent.set(Math.min(100, Math.max(0, value)));
  }

  clear(): void {
    this.lines.set([]);
    this.discountPercent.set(0);
  }
}

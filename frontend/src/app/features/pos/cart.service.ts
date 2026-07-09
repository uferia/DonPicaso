import { Injectable, computed, inject, signal } from '@angular/core';

import { MenuProduct } from '../../core/menu/menu.models';
import { MenuService } from '../../core/menu/menu.service';

export interface CartLine {
  product: MenuProduct;
  quantity: number;
}

/**
 * Half-up (away from zero) to 2 decimals — must stay in lockstep with
 * RoundMoney() in CreateOrderCommandValidator, which re-derives and rejects
 * drifted math. Money inputs are 2-dp amounts times 2-dp percents (at most
 * 6 decimal places), so any true non-midpoint value sits >= 1e-6 from a
 * half-cent boundary while binary representation error is far below 1e-9 at
 * POS magnitudes. The absolute 1e-9 nudge therefore lifts exact midpoints
 * (which floats store slightly low, e.g. 2.175 -> 2.17499999999999982)
 * without ever crossing a boundary from a genuinely lower value.
 * (Number.EPSILON was previously used here; it is relative to 1.0 and far
 * too small to bridge float error at dollar magnitudes.)
 */
export function roundMoney(value: number): number {
  return (Math.sign(value) * Math.round((Math.abs(value) + 1e-9) * 100)) / 100;
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

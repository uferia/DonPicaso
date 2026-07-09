import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectButtonModule } from 'primeng/selectbutton';

import { AuthService } from '../../../core/auth/auth.service';
import { MenuService } from '../../../core/menu/menu.service';
import { PaymentMethod } from '../../../core/offline/offline-order-db';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { CartService, roundMoney } from '../cart.service';

@Component({
  selector: 'app-payment-dialog',
  imports: [
    CurrencyPipe,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    SelectButtonModule,
  ],
  templateUrl: './payment-dialog.html',
  styleUrl: './payment-dialog.scss',
})
export class PaymentDialog {
  protected readonly cart = inject(CartService);
  private readonly menu = inject(MenuService);
  private readonly authService = inject(AuthService);
  private readonly orderSync = inject(OrderSyncService);
  private readonly messageService = inject(MessageService);

  readonly visible = model(false);

  protected readonly methodOptions: { label: string; value: PaymentMethod }[] = [
    { label: 'Cash', value: 'Cash' },
    { label: 'Card', value: 'Card' },
  ];

  protected readonly method = signal<PaymentMethod>('Cash');
  protected readonly cashTendered = signal<number | null>(null);
  protected readonly isSubmitting = signal(false);

  protected readonly changeDue = computed(() => {
    const tendered = this.cashTendered();
    return tendered === null ? null : roundMoney(tendered - this.cart.total());
  });

  protected readonly canConfirm = computed(
    () => this.method() === 'Card' || (this.changeDue() !== null && this.changeDue()! >= 0),
  );

  protected async confirm(): Promise<void> {
    if (!this.canConfirm() || this.isSubmitting()) {
      return;
    }

    const user = this.authService.currentUser();
    if (!user?.branchId || !user.brandId) {
      // Belt-and-braces behind branchSessionGuard on the /pos route: if a
      // session without branch claims still reaches this dialog, say so
      // instead of swallowing the tap.
      this.messageService.add({
        severity: 'error',
        summary: 'Staff sign-in required',
        detail: 'Sign in at this register with your PIN to place orders.',
      });
      return;
    }

    this.isSubmitting.set(true);
    try {
      const isCash = this.method() === 'Cash';
      const result = await this.orderSync.placeOrder({
        branchId: user.branchId,
        brandId: user.brandId,
        subtotal: this.cart.subtotal(),
        discountPercent: this.cart.discountPercent(),
        discountAmount: this.cart.discountAmount(),
        taxRatePercent: this.menu.taxRatePercent(),
        taxAmount: this.cart.taxAmount(),
        totalAmount: this.cart.total(),
        paymentMethod: this.method(),
        cashTendered: isCash ? this.cashTendered() : null,
        changeDue: isCash ? this.changeDue() : null,
        items: this.cart.lines().map((line) => ({
          productId: line.product.id,
          productName: line.product.name,
          quantity: line.quantity,
          unitPrice: line.product.price,
        })),
      });

      this.messageService.add(
        result.status === 'sent'
          ? { severity: 'success', summary: 'Order placed' }
          : {
              severity: 'warn',
              summary: 'Order queued — offline',
              detail: 'It will sync when the connection returns.',
            },
      );
      this.cart.clear();
      this.reset();
      this.visible.set(false);
    } catch {
      // 4xx/5xx from the backend — the money math should make this
      // impossible from a correct client, so treat it as a bug surface:
      // keep the cart so nothing is lost.
      this.messageService.add({
        severity: 'error',
        summary: "Couldn't place order",
        detail: 'The order was kept — try again.',
      });
    } finally {
      this.isSubmitting.set(false);
    }
  }

  protected close(): void {
    this.reset();
    this.visible.set(false);
  }

  private reset(): void {
    this.method.set('Cash');
    this.cashTendered.set(null);
  }
}

import { CurrencyPipe } from '@angular/common';
import { Component, inject, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';

import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';

@Component({
  selector: 'app-cart-panel',
  imports: [CurrencyPipe, FormsModule, ButtonModule, InputNumberModule],
  templateUrl: './cart-panel.html',
  styleUrl: './cart-panel.scss',
})
export class CartPanel {
  protected readonly cart = inject(CartService);
  protected readonly menu = inject(MenuService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly pay = output<void>();

  protected cancelOrder(): void {
    this.confirmationService.confirm({
      header: 'Cancel Order',
      message: 'Clear all items from this order?',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.cart.clear(),
    });
  }
}

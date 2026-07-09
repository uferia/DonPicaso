import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';

import { AuthService } from '../../../core/auth/auth.service';
import { MenuService } from '../../../core/menu/menu.service';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { CartService } from '../cart.service';
import { CartPanel } from '../cart-panel/cart-panel';
import { PaymentDialog } from '../payment-dialog/payment-dialog';
import { ProductCatalog } from '../product-catalog/product-catalog';

@Component({
  selector: 'app-pos-shell',
  imports: [ButtonModule, ConfirmDialogModule, ToastModule, CartPanel, PaymentDialog, ProductCatalog],
  providers: [CartService, MessageService, ConfirmationService],
  templateUrl: './pos-shell.html',
  styleUrl: './pos-shell.scss',
})
export class PosShell implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly menu = inject(MenuService);
  protected readonly cart = inject(CartService);
  protected readonly orderSync = inject(OrderSyncService);

  protected readonly paymentVisible = signal(false);

  ngOnInit(): void {
    void this.menu.loadMenu();
  }

  protected retry(): void {
    void this.menu.loadMenu();
  }

  protected logout(): void {
    if (this.cart.lines().length > 0) {
      this.confirmationService.confirm({
        header: 'Log out',
        message: 'The current order will be discarded. Log out?',
        icon: 'pi pi-exclamation-triangle',
        accept: () => void this.doLogout(),
      });
      return;
    }
    void this.doLogout();
  }

  private async doLogout(): Promise<void> {
    await this.authService.logout();
    // Device branch binding is untouched — the tablet lands on the PIN
    // screen ready for the next staff member.
    // Not awaited: in tests (and any router config missing '/staff-login')
    // navigateByUrl's promise rejects on no-match, which would otherwise
    // propagate out of this method. Matches the existing pattern in
    // staff-login.ts for the same reason.
    void this.router.navigateByUrl('/staff-login');
  }
}

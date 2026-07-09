import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MessageService } from 'primeng/api';

import { AuthService } from '../../../core/auth/auth.service';
import { Role } from '../../../core/auth/auth.models';
import { MenuService } from '../../../core/menu/menu.service';
import { NewOrder } from '../../../core/offline/offline-order-db';
import { OrderSyncService } from '../../../core/offline/order-sync.service';
import { CartService } from '../cart.service';
import { PaymentDialog } from './payment-dialog';

const espresso = { id: 'p-1', name: 'Espresso', price: 7.99, imageUrl: null };
const latte = { id: 'p-2', name: 'Caffe Latte', price: 5.54, imageUrl: null };

describe('PaymentDialog', () => {
  let cart: CartService;
  let placeOrderMock: ReturnType<typeof vi.fn>;
  let messageAddSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    placeOrderMock = vi.fn().mockResolvedValue({ status: 'sent', orderId: 'order-1' });

    await TestBed.configureTestingModule({
      imports: [PaymentDialog],
      providers: [
        CartService,
        MessageService,
        { provide: OrderSyncService, useValue: { placeOrder: placeOrderMock } },
        { provide: MenuService, useValue: { taxRatePercent: signal(1.5) } },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(AuthService).currentUser.set({
      userId: 'user-1',
      role: Role.Staff,
      brandId: 'brand-1',
      branchId: 'branch-1',
    });
    messageAddSpy = vi.spyOn(TestBed.inject(MessageService), 'add');

    cart = TestBed.inject(CartService);
    // 2 × 7.99 + 1 × 5.54 = 21.52; 20% discount = 4.30; tax 0.26; total 17.48.
    cart.add(espresso);
    cart.add(espresso);
    cart.add(latte);
    cart.setDiscountPercent(20);
  });

  it('computes change due and blocks confirm while tendered is below the total', () => {
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;

    component['cashTendered'].set(10);
    expect(component['changeDue']()).toBe(-7.48);
    expect(component['canConfirm']()).toBe(false);

    component['cashTendered'].set(20);
    expect(component['changeDue']()).toBe(2.52);
    expect(component['canConfirm']()).toBe(true);
  });

  it('submits the full cash payload, resets the cart, and closes', async () => {
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['cashTendered'].set(20);

    await component['confirm']();

    const payload = placeOrderMock.mock.calls[0][0] as NewOrder;
    expect(payload).toEqual({
      branchId: 'branch-1',
      brandId: 'brand-1',
      subtotal: 21.52,
      discountPercent: 20,
      discountAmount: 4.3,
      taxRatePercent: 1.5,
      taxAmount: 0.26,
      totalAmount: 17.48,
      paymentMethod: 'Cash',
      cashTendered: 20,
      changeDue: 2.52,
      items: [
        { productId: 'p-1', productName: 'Espresso', quantity: 2, unitPrice: 7.99 },
        { productId: 'p-2', productName: 'Caffe Latte', quantity: 1, unitPrice: 5.54 },
      ],
    });
    expect(cart.lines()).toEqual([]);
    expect(component.visible()).toBe(false);
    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }));
  });

  it('submits card payments with null cash fields and no tendered requirement', async () => {
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    expect(component['canConfirm']()).toBe(true);

    await component['confirm']();

    const payload = placeOrderMock.mock.calls[0][0] as NewOrder;
    expect(payload.paymentMethod).toBe('Card');
    expect(payload.cashTendered).toBeNull();
    expect(payload.changeDue).toBeNull();
  });

  it('warns instead of celebrating when the order was queued offline', async () => {
    placeOrderMock.mockResolvedValue({ status: 'queued-offline' });
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    await component['confirm']();

    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'warn' }));
    expect(cart.lines()).toEqual([]);
  });

  it('shows an error instead of silently ignoring confirm when the session has no branch', async () => {
    TestBed.inject(AuthService).currentUser.set({
      userId: 'user-1',
      role: Role.BrandOwner,
      brandId: 'brand-1',
      branchId: null,
    });
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    await component['confirm']();

    expect(placeOrderMock).not.toHaveBeenCalled();
    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error' }));
    expect(component.visible()).toBe(true);
  });

  it('keeps the cart intact when the backend rejects the order', async () => {
    placeOrderMock.mockRejectedValue(new Error('400'));
    const fixture = TestBed.createComponent(PaymentDialog);
    const component = fixture.componentInstance;
    component.visible.set(true);
    component['method'].set('Card');

    await component['confirm']();

    expect(messageAddSpy).toHaveBeenCalledWith(expect.objectContaining({ severity: 'error' }));
    expect(cart.lines()).toHaveLength(2);
    expect(component.visible()).toBe(true);
  });
});

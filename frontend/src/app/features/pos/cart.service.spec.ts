import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { MenuProduct } from '../../core/menu/menu.models';
import { MenuService } from '../../core/menu/menu.service';
import { CartService, roundMoney } from './cart.service';

const espresso: MenuProduct = { id: 'p-1', name: 'Espresso', price: 7.99, imageUrl: null };
const latte: MenuProduct = { id: 'p-2', name: 'Caffe Latte', price: 5.54, imageUrl: null };

describe('roundMoney', () => {
  it('rounds half-up to 2 decimals', () => {
    expect(roundMoney(4.304)).toBe(4.3);
    expect(roundMoney(0.015)).toBe(0.02);
    expect(roundMoney(1.235)).toBe(1.24);
    expect(roundMoney(23.970000000000002)).toBe(23.97);
  });

  it('matches the server on exact-decimal midpoints that float representation stores low', () => {
    // These vectors previously diverged from the server's decimal
    // Math.Round(value, 2, MidpointRounding.AwayFromZero): Number.EPSILON
    // (relative to 1.0) could not bridge float error at dollar magnitudes,
    // so the client rounded down while the server rounded up.
    expect(roundMoney(145 * 0.015)).toBe(2.18);
    expect(roundMoney(4.27 * 0.5)).toBe(2.14);
    expect(roundMoney(2.175)).toBe(2.18);
    expect(roundMoney(1.225)).toBe(1.23);
    expect(roundMoney(1.235)).toBe(1.24);
    expect(roundMoney(0)).toBe(0);
    expect(roundMoney(-2.175)).toBe(-2.18);
  });

  it('does not nudge a genuine non-midpoint value across a half-cent boundary', () => {
    expect(roundMoney(2.174985)).toBe(2.17);
  });
});

describe('CartService', () => {
  let cart: CartService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CartService,
        { provide: MenuService, useValue: { taxRatePercent: signal(1.5) } },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    cart = TestBed.inject(CartService);
  });

  it('adds products and merges repeat adds into one line', () => {
    cart.add(espresso);
    cart.add(espresso);
    cart.add(latte);

    expect(cart.lines()).toEqual([
      { product: espresso, quantity: 2 },
      { product: latte, quantity: 1 },
    ]);
  });

  it('computes subtotal, discount, tax, and total with half-up rounding', () => {
    // 2 × 7.99 + 1 × 5.54 = 21.52; 20% discount = 4.304 -> 4.30;
    // 1.5% tax on 17.22 = 0.2583 -> 0.26; total 17.48.
    cart.add(espresso);
    cart.add(espresso);
    cart.add(latte);
    cart.setDiscountPercent(20);

    expect(cart.subtotal()).toBe(21.52);
    expect(cart.discountAmount()).toBe(4.3);
    expect(cart.taxAmount()).toBe(0.26);
    expect(cart.total()).toBe(17.48);
  });

  it('increments, decrements, and removes a line when quantity hits zero', () => {
    cart.add(espresso);
    cart.increment('p-1');
    expect(cart.lines()[0].quantity).toBe(2);

    cart.decrement('p-1');
    cart.decrement('p-1');
    expect(cart.lines()).toEqual([]);
  });

  it('removes a line directly', () => {
    cart.add(espresso);
    cart.add(latte);
    cart.remove('p-1');

    expect(cart.lines().map((l) => l.product.id)).toEqual(['p-2']);
  });

  it('clamps discount percent to 0..100', () => {
    cart.setDiscountPercent(150);
    expect(cart.discountPercent()).toBe(100);

    cart.setDiscountPercent(-5);
    expect(cart.discountPercent()).toBe(0);
  });

  it('clear resets lines and discount', () => {
    cart.add(espresso);
    cart.setDiscountPercent(10);

    cart.clear();

    expect(cart.lines()).toEqual([]);
    expect(cart.discountPercent()).toBe(0);
    expect(cart.total()).toBe(0);
  });
});

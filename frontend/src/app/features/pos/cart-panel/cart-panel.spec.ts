import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService } from 'primeng/api';

import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';
import { CartPanel } from './cart-panel';

const espresso = { id: 'p-1', name: 'Espresso', price: 2.5, imageUrl: null };

describe('CartPanel', () => {
  let cart: CartService;
  let confirmationService: ConfirmationService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CartPanel],
      providers: [
        CartService,
        ConfirmationService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(MenuService).taxRatePercent.set(1.5);
    cart = TestBed.inject(CartService);
    confirmationService = TestBed.inject(ConfirmationService);
  });

  it('renders cart lines with quantities and totals', () => {
    cart.add(espresso);
    cart.add(espresso);

    const fixture = TestBed.createComponent(CartPanel);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Espresso');
    expect(fixture.nativeElement.querySelector('.line-quantity')!.textContent).toContain('2');
    expect(fixture.nativeElement.textContent).toContain('$5.00');
  });

  it('disables Pay when the cart is empty and emits pay when pressed with items', () => {
    const fixture = TestBed.createComponent(CartPanel);
    fixture.detectChanges();

    const payButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.pay-button button');
    expect(payButton!.disabled).toBe(true);

    cart.add(espresso);
    fixture.detectChanges();

    let emitted = false;
    fixture.componentInstance.pay.subscribe(() => (emitted = true));
    payButton!.click();

    expect(emitted).toBe(true);
  });

  it('cancel order asks for confirmation and clears the cart on accept', () => {
    cart.add(espresso);
    const fixture = TestBed.createComponent(CartPanel);
    fixture.detectChanges();

    let captured: Confirmation | undefined;
    vi.spyOn(confirmationService, 'confirm').mockImplementation((confirmation: Confirmation) => {
      captured = confirmation;
      return confirmationService;
    });

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.cancel-button button')!.click();
    expect(captured).toBeDefined();

    captured!.accept!();
    expect(cart.lines()).toEqual([]);
  });
});

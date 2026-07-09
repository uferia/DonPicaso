import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MenuCategory } from '../../../core/menu/menu.models';
import { MenuService } from '../../../core/menu/menu.service';
import { CartService } from '../cart.service';
import { ProductCatalog } from './product-catalog';

const categories: MenuCategory[] = [
  {
    id: 'cat-coffee',
    name: 'Coffee',
    products: [
      { id: 'p-espresso', name: 'Espresso', price: 2.5, imageUrl: null },
      { id: 'p-latte', name: 'Caffe Latte', price: 4.25, imageUrl: null },
    ],
  },
  {
    id: 'cat-snacks',
    name: 'Snacks',
    products: [{ id: 'p-fries', name: 'French Fries', price: 3.25, imageUrl: null }],
  },
];

describe('ProductCatalog', () => {
  let cart: CartService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductCatalog],
      providers: [CartService, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    TestBed.inject(MenuService).categories.set(categories);
    cart = TestBed.inject(CartService);
  });

  it('renders the first category by default with one tab per category', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    const tiles = fixture.nativeElement.querySelectorAll('.product-tile');
    const tabs = fixture.nativeElement.querySelectorAll('.category-tab');

    expect(tabs.length).toBe(2);
    expect(tiles.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('Espresso');
  });

  it('switches products when a category tab is clicked', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    const snacksTab = Array.from(
      fixture.nativeElement.querySelectorAll('.category-tab') as NodeListOf<HTMLButtonElement>,
    ).find((tab) => tab.textContent!.includes('Snacks'))!;
    snacksTab.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('French Fries');
    expect(fixture.nativeElement.textContent).not.toContain('Espresso');
  });

  it('filters products by the search term within the active category', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    fixture.componentInstance['searchTerm'].set('latte');
    fixture.detectChanges();

    const tiles = fixture.nativeElement.querySelectorAll('.product-tile');
    expect(tiles.length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Caffe Latte');
  });

  it('adds the product to the cart when a tile is tapped', () => {
    const fixture = TestBed.createComponent(ProductCatalog);
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('.product-tile') as HTMLButtonElement)!.click();

    expect(cart.lines()).toEqual([
      { product: categories[0].products[0], quantity: 1 },
    ]);
  });
});

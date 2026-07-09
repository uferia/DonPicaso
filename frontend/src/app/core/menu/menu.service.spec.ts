import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MenuResponse } from './menu.models';
import { MENU_CACHE_KEY, MenuService } from './menu.service';

const sampleMenu: MenuResponse = {
  categories: [
    {
      id: 'cat-1',
      name: 'Coffee',
      products: [{ id: 'prod-1', name: 'Espresso', price: 2.5, imageUrl: null }],
    },
  ],
  taxRatePercent: 1.5,
};

describe('MenuService', () => {
  let service: MenuService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MenuService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads the menu from the network and caches it', async () => {
    const loadPromise = service.loadMenu();
    httpMock.expectOne('/api/v1/menu').flush(sampleMenu);
    await loadPromise;

    expect(service.source()).toBe('network');
    expect(service.taxRatePercent()).toBe(1.5);
    expect(service.categories()).toEqual(sampleMenu.categories);
    expect(JSON.parse(localStorage.getItem(MENU_CACHE_KEY)!)).toEqual(sampleMenu);
  });

  it('falls back to the cached menu when the network fails', async () => {
    localStorage.setItem(MENU_CACHE_KEY, JSON.stringify(sampleMenu));

    const loadPromise = service.loadMenu();
    httpMock.expectOne('/api/v1/menu').error(new ProgressEvent('offline'));
    await loadPromise;

    expect(service.source()).toBe('cache');
    expect(service.categories()).toEqual(sampleMenu.categories);
    expect(service.taxRatePercent()).toBe(1.5);
  });

  it('reports unavailable when there is no network and no cache', async () => {
    const loadPromise = service.loadMenu();
    httpMock.expectOne('/api/v1/menu').error(new ProgressEvent('offline'));
    await loadPromise;

    expect(service.source()).toBe('unavailable');
    expect(service.categories()).toEqual([]);
  });
});

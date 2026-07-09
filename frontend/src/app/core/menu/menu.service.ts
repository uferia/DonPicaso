import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { MenuCategory, MenuResponse } from './menu.models';

const MENU_URL = '/api/v1/menu';
export const MENU_CACHE_KEY = 'donpicaso.menuCache';

export type MenuSource = 'network' | 'cache' | 'unavailable';

/**
 * Menu read model for the POS. The last successful fetch is cached in
 * localStorage so a tablet that goes offline can keep taking orders
 * (submission already queues through OrderSyncService).
 */
@Injectable({ providedIn: 'root' })
export class MenuService {
  private readonly http = inject(HttpClient);

  readonly categories = signal<MenuCategory[]>([]);
  readonly taxRatePercent = signal(0);
  readonly source = signal<MenuSource>('unavailable');

  async loadMenu(): Promise<void> {
    try {
      const menu = await firstValueFrom(this.http.get<MenuResponse>(MENU_URL));
      localStorage.setItem(MENU_CACHE_KEY, JSON.stringify(menu));
      this.apply(menu, 'network');
    } catch {
      const cached = localStorage.getItem(MENU_CACHE_KEY);
      if (cached) {
        this.apply(JSON.parse(cached) as MenuResponse, 'cache');
      } else {
        this.source.set('unavailable');
      }
    }
  }

  private apply(menu: MenuResponse, source: MenuSource): void {
    this.categories.set(menu.categories);
    this.taxRatePercent.set(menu.taxRatePercent);
    this.source.set(source);
  }
}

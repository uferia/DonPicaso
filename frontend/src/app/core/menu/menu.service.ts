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

  private readonly _categories = signal<MenuCategory[]>([]);
  private readonly _taxRatePercent = signal(0);
  private readonly _currencyCode = signal('PHP');
  private readonly _source = signal<MenuSource>('unavailable');

  readonly categories = this._categories.asReadonly();
  readonly taxRatePercent = this._taxRatePercent.asReadonly();
  readonly currencyCode = this._currencyCode.asReadonly();
  readonly source = this._source.asReadonly();

  async loadMenu(): Promise<void> {
    try {
      const menu = await firstValueFrom(this.http.get<MenuResponse>(MENU_URL));
      try {
        localStorage.setItem(MENU_CACHE_KEY, JSON.stringify(menu));
      } catch {
        // Cache write failed (e.g. quota exceeded) — the fetch itself
        // still succeeded, so the source must remain 'network', not be
        // mislabeled by a storage-layer problem.
      }
      this.apply(menu, 'network');
    } catch {
      const cached = localStorage.getItem(MENU_CACHE_KEY);
      if (cached) {
        try {
          this.apply(JSON.parse(cached) as MenuResponse, 'cache');
        } catch {
          // Corrupted cache entry — fall through to the unavailable/retry
          // UI instead of throwing out of loadMenu() (called as
          // `void this.menu.loadMenu()`, which would otherwise surface as
          // an unhandled rejection).
          this._source.set('unavailable');
        }
      } else {
        this._source.set('unavailable');
      }
    }
  }

  private apply(menu: MenuResponse, source: MenuSource): void {
    this._categories.set(menu.categories);
    this._taxRatePercent.set(menu.taxRatePercent);
    // Stale caches written before currency support have no currencyCode.
    this._currencyCode.set(menu.currencyCode ?? 'PHP');
    this._source.set(source);
  }
}

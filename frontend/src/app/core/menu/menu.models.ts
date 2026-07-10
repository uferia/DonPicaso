/** Mirrors the backend GetMenu contract (GET /api/v1/menu — Modules.Menu). */
export interface MenuProduct {
  id: string;
  name: string;
  price: number;
  imageUrl: string | null;
}

export interface MenuCategory {
  id: string;
  name: string;
  products: MenuProduct[];
}

export interface MenuResponse {
  categories: MenuCategory[];
  taxRatePercent: number;
  /** ISO 4217 code (e.g. 'PHP') — set by Menu:CurrencyCode in the API's config. */
  currencyCode: string;
}

import Dexie, { Table } from 'dexie';

/**
 * Mirrors the backend `CreateOrderCommand` contract
 * (POST /api/v1/orders — Modules.Sales/Features/Orders/CreateOrder).
 */
export interface OrderItemPayload {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export interface CreateOrderPayload {
  branchId: string;
  brandId: string;
  totalAmount: number;
  items: OrderItemPayload[];
}

/** An order captured while offline, awaiting replay to the backend. */
export interface PendingOrder {
  /** Auto-incremented by Dexie; also gives us stable FIFO replay order. */
  id?: number;
  payload: CreateOrderPayload;
  queuedAtUtc: string;
}

/**
 * IndexedDB store (Dexie pattern). Inside a Capacitor shell this lives in the
 * native app's WebView storage, which — combined with the persistent-storage
 * grant requested by OrderSyncService — protects queued orders from the
 * cache eviction Safari applies to plain browser tabs on iOS.
 */
export class OfflineOrderDb extends Dexie {
  pendingOrders!: Table<PendingOrder, number>;

  constructor() {
    super('don-picaso-pos');

    this.version(1).stores({
      // '++id' -> auto-increment primary key; queuedAtUtc indexed for diagnostics.
      pendingOrders: '++id, queuedAtUtc',
    });
  }
}

export const offlineOrderDb = new OfflineOrderDb();

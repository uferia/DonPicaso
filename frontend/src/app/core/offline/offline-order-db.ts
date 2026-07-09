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

export type PaymentMethod = 'Cash' | 'Card';

export interface CreateOrderPayload {
  /**
   * Device-generated idempotency key (UUID). Assigned once when the order is
   * placed and reused verbatim on every replay, so the backend can detect an
   * order it already received even if the original response was lost.
   */
  clientOrderId: string;
  branchId: string;
  brandId: string;
  subtotal: number;
  discountPercent: number;
  discountAmount: number;
  taxRatePercent: number;
  taxAmount: number;
  totalAmount: number;
  paymentMethod: PaymentMethod;
  cashTendered: number | null;
  changeDue: number | null;
  items: OrderItemPayload[];
}

/** What the UI hands to OrderSyncService; the sync layer assigns the key. */
export type NewOrder = Omit<CreateOrderPayload, 'clientOrderId'>;

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

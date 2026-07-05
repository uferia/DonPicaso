import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, NgZone, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  CreateOrderPayload,
  NewOrder,
  OfflineOrderDb,
  offlineOrderDb,
} from './offline-order-db';

export type OrderPlacementResult =
  | { status: 'sent'; orderId: string }
  | { status: 'queued-offline' };

interface CreateOrderResponse {
  orderId: string;
}

const ORDERS_API_URL = '/api/v1/orders';

/**
 * Offline-first order pipeline for the POS tablet:
 *
 *  - online  -> POST the order straight to the backend.
 *  - offline -> persist it to IndexedDB (safe native storage under Capacitor).
 *  - on reconnect -> replay pending orders sequentially (FIFO) and delete
 *    each local record only after the backend confirms success.
 */
@Injectable({ providedIn: 'root' })
export class OrderSyncService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);
  private readonly destroyRef = inject(DestroyRef);
  private readonly db: OfflineOrderDb = offlineOrderDb;

  /** Network state, driven by navigator.onLine + window online/offline events. */
  readonly isOnline = signal<boolean>(navigator.onLine);

  /** Number of orders waiting in IndexedDB for replay. */
  readonly pendingCount = signal<number>(0);

  /** True while a replay loop is running. */
  readonly isSyncing = signal<boolean>(false);

  private readonly onOnline = () => this.zone.run(() => this.handleReconnected());
  private readonly onOffline = () => this.zone.run(() => this.isOnline.set(false));

  constructor() {
    window.addEventListener('online', this.onOnline);
    window.addEventListener('offline', this.onOffline);
    this.destroyRef.onDestroy(() => {
      window.removeEventListener('online', this.onOnline);
      window.removeEventListener('offline', this.onOffline);
    });

    // Ask the WebView to exempt IndexedDB from storage-pressure eviction.
    // Best-effort: Capacitor WebViews generally grant this silently.
    void navigator.storage?.persist?.();

    void this.refreshPendingCount();

    // Reconnection may have happened while the app was suspended; if we start
    // online with a backlog, drain it immediately.
    if (navigator.onLine) {
      void this.syncPendingOrders();
    }
  }

  /**
   * Entry point used by the ordering UI. Never rejects because of
   * connectivity: a network-level failure downgrades to an offline queue.
   *
   * The idempotency key is minted here — before any network attempt — so the
   * exact same key is reused whether the order goes straight through, falls
   * back to the queue, or is replayed later.
   */
  async placeOrder(order: NewOrder): Promise<OrderPlacementResult> {
    const payload: CreateOrderPayload = {
      clientOrderId: crypto.randomUUID(),
      ...order,
    };

    if (!this.isOnline()) {
      await this.queueLocally(payload);
      return { status: 'queued-offline' };
    }

    try {
      const response = await firstValueFrom(
        this.http.post<CreateOrderResponse>(ORDERS_API_URL, payload),
      );
      return { status: 'sent', orderId: response.orderId };
    } catch (error) {
      if (this.isConnectivityError(error)) {
        // navigator.onLine lied (captive portal, dead Wi-Fi, kitchen basement).
        this.isOnline.set(false);
        await this.queueLocally(payload);
        return { status: 'queued-offline' };
      }
      // 4xx/5xx means the backend rejected the order — surface it to the UI,
      // do not queue it for a retry that will fail identically.
      throw error;
    }
  }

  /**
   * Replays the IndexedDB backlog sequentially so orders reach the backend
   * in the sequence they were taken. Each record is deleted from the local
   * cache only after the API responds with success (2xx). On the first
   * failure the loop stops and the remainder is retried on the next
   * reconnect, preserving FIFO order.
   */
  async syncPendingOrders(): Promise<void> {
    if (this.isSyncing() || !navigator.onLine) {
      return;
    }
    this.isSyncing.set(true);

    try {
      const pending = await this.db.pendingOrders.orderBy(':id').toArray();

      for (const record of pending) {
        try {
          await firstValueFrom(
            this.http.post<CreateOrderResponse>(ORDERS_API_URL, record.payload),
          );
        } catch (error) {
          if (this.isConnectivityError(error)) {
            this.isOnline.set(false);
            return; // connection dropped mid-replay; keep the rest queued
          }
          console.error('[OrderSync] Backend rejected queued order; keeping for manual review.', {
            pendingOrderId: record.id,
            error,
          });
          return;
        }

        await this.db.pendingOrders.delete(record.id!);
        await this.refreshPendingCount();
      }
    } finally {
      this.isSyncing.set(false);
      await this.refreshPendingCount();
    }
  }

  private handleReconnected(): void {
    this.isOnline.set(true);
    void this.syncPendingOrders();
  }

  private async queueLocally(payload: CreateOrderPayload): Promise<void> {
    await this.db.pendingOrders.add({
      payload,
      queuedAtUtc: new Date().toISOString(),
    });
    await this.refreshPendingCount();
  }

  private async refreshPendingCount(): Promise<void> {
    this.pendingCount.set(await this.db.pendingOrders.count());
  }

  /** status 0 = request never reached the server (offline, DNS, timeout). */
  private isConnectivityError(error: unknown): boolean {
    return error instanceof HttpErrorResponse && error.status === 0;
  }
}

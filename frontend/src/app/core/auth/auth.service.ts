import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AuthTokens, LoginRequest, Role, StaffLoginRequest } from './auth.models';

export interface CurrentUser {
  userId: string;
  role: Role;
  brandId: string | null;
  branchId: string | null;
}

const LOGIN_URL = '/api/v1/auth/login';
const STAFF_LOGIN_URL = '/api/v1/auth/staff-login';
const REFRESH_URL = '/api/v1/auth/refresh';
const LOGOUT_URL = '/api/v1/auth/logout';
const REFRESH_TOKEN_STORAGE_KEY = 'donpicaso.refreshToken';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private accessToken: string | null = null;

  readonly currentUser = signal<CurrentUser | null>(null);

  async login(request: LoginRequest): Promise<void> {
    const tokens = await firstValueFrom(this.http.post<AuthTokens>(LOGIN_URL, request));
    this.applyTokens(tokens);
  }

  async staffLogin(request: StaffLoginRequest): Promise<void> {
    const tokens = await firstValueFrom(this.http.post<AuthTokens>(STAFF_LOGIN_URL, request));
    this.applyTokens(tokens);
  }

  /**
   * Exchanges the stored refresh token for a new access token. Used on app
   * start (no access token yet after a reload) and by the auth interceptor
   * after a 401.
   */
  async refresh(): Promise<boolean> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
    if (!refreshToken) {
      return false;
    }

    try {
      const tokens = await firstValueFrom(this.http.post<AuthTokens>(REFRESH_URL, { refreshToken }));
      this.applyTokens(tokens);
      return true;
    } catch {
      this.clearSession();
      return false;
    }
  }

  async logout(): Promise<void> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
    if (refreshToken) {
      await firstValueFrom(this.http.post(LOGOUT_URL, { refreshToken }));
    }
    this.clearSession();
  }

  getAccessToken(): string | null {
    return this.accessToken;
  }

  private applyTokens(tokens: AuthTokens): void {
    this.accessToken = tokens.accessToken;
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, tokens.refreshToken);
    this.currentUser.set(decodeCurrentUser(tokens.accessToken));
  }

  private clearSession(): void {
    this.accessToken = null;
    localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
    this.currentUser.set(null);
  }
}

/**
 * Decodes the JWT payload to read claims client-side. Never used for
 * security decisions (the backend re-validates on every request) - only
 * to populate UI state like the current user's role.
 */
function decodeCurrentUser(accessToken: string): CurrentUser {
  const payloadSegment = accessToken.split('.')[1];
  const base64 = payloadSegment.replace(/-/g, '+').replace(/_/g, '/');
  const payload = JSON.parse(atob(base64)) as Record<string, string>;

  return {
    userId: payload['sub'],
    role: payload['role'] as Role,
    brandId: payload['brandId'] ?? null,
    branchId: payload['branchId'] ?? null,
  };
}

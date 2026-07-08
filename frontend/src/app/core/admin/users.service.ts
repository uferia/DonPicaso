import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AdminUser, CreateUserRequest, ResetCredentialRequest, UpdateUserRequest } from './admin.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly http = inject(HttpClient);

  list(filters: { brandId?: string; branchId?: string } = {}): Promise<AdminUser[]> {
    let params = new HttpParams();
    if (filters.brandId) {
      params = params.set('brandId', filters.brandId);
    }
    if (filters.branchId) {
      params = params.set('branchId', filters.branchId);
    }

    return firstValueFrom(this.http.get<AdminUser[]>('/api/v1/users', { params }));
  }

  get(userId: string): Promise<AdminUser> {
    return firstValueFrom(this.http.get<AdminUser>(`/api/v1/users/${userId}`));
  }

  create(request: CreateUserRequest): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>('/api/v1/users', request));
  }

  update(userId: string, request: UpdateUserRequest): Promise<AdminUser> {
    return firstValueFrom(this.http.put<AdminUser>(`/api/v1/users/${userId}`, request));
  }

  resetCredential(userId: string, request: ResetCredentialRequest): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>(`/api/v1/users/${userId}/reset-credential`, request));
  }

  deactivate(userId: string): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>(`/api/v1/users/${userId}/deactivate`, {}));
  }

  reactivate(userId: string): Promise<AdminUser> {
    return firstValueFrom(this.http.post<AdminUser>(`/api/v1/users/${userId}/reactivate`, {}));
  }
}

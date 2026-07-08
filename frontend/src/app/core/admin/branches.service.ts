import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Branch } from './admin.models';

@Injectable({ providedIn: 'root' })
export class BranchesService {
  private readonly http = inject(HttpClient);

  list(brandId: string): Promise<Branch[]> {
    return firstValueFrom(this.http.get<Branch[]>(`/api/v1/brands/${brandId}/branches`));
  }

  get(brandId: string, branchId: string): Promise<Branch> {
    return firstValueFrom(this.http.get<Branch>(`/api/v1/brands/${brandId}/branches/${branchId}`));
  }

  create(brandId: string, name: string): Promise<Branch> {
    return firstValueFrom(this.http.post<Branch>(`/api/v1/brands/${brandId}/branches`, { name }));
  }

  update(brandId: string, branchId: string, name: string): Promise<Branch> {
    return firstValueFrom(this.http.put<Branch>(`/api/v1/brands/${brandId}/branches/${branchId}`, { name }));
  }

  deactivate(brandId: string, branchId: string): Promise<Branch> {
    return firstValueFrom(this.http.post<Branch>(`/api/v1/brands/${brandId}/branches/${branchId}/deactivate`, {}));
  }

  reactivate(brandId: string, branchId: string): Promise<Branch> {
    return firstValueFrom(this.http.post<Branch>(`/api/v1/brands/${brandId}/branches/${branchId}/reactivate`, {}));
  }
}

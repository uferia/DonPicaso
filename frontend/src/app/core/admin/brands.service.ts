import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Brand } from './admin.models';

@Injectable({ providedIn: 'root' })
export class BrandsService {
  private readonly http = inject(HttpClient);

  list(): Promise<Brand[]> {
    return firstValueFrom(this.http.get<Brand[]>('/api/v1/brands'));
  }

  get(brandId: string): Promise<Brand> {
    return firstValueFrom(this.http.get<Brand>(`/api/v1/brands/${brandId}`));
  }

  create(name: string): Promise<Brand> {
    return firstValueFrom(this.http.post<Brand>('/api/v1/brands', { name }));
  }

  update(brandId: string, name: string): Promise<Brand> {
    return firstValueFrom(this.http.put<Brand>(`/api/v1/brands/${brandId}`, { name }));
  }

  deactivate(brandId: string): Promise<Brand> {
    return firstValueFrom(this.http.post<Brand>(`/api/v1/brands/${brandId}/deactivate`, {}));
  }

  reactivate(brandId: string): Promise<Brand> {
    return firstValueFrom(this.http.post<Brand>(`/api/v1/brands/${brandId}/reactivate`, {}));
  }
}

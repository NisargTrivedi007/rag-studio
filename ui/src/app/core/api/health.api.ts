import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class HealthApi {
  private http = inject(HttpClient);

  check() {
    return this.http.get<{ status: string }>('/api/health');
  }
}

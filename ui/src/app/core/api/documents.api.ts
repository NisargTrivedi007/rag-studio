import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { DocumentSummary } from './models';

@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private http = inject(HttpClient);
  private base = '/api/documents';

  list() {
    return this.http.get<DocumentSummary[]>(`${this.base}/`);
  }

  upload(file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<DocumentSummary>(`${this.base}/upload`, form);
  }

  delete(id: string) {
    return this.http.delete(`${this.base}/${id}`);
  }
}

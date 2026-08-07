import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { ChatResponse, SessionDetail, SessionSummary } from './models';

@Injectable({ providedIn: 'root' })
export class SessionsApi {
  private http = inject(HttpClient);
  private base = '/api/sessions';

  list() {
    return this.http.get<SessionSummary[]>(`${this.base}/`);
  }

  create() {
    return this.http.post<SessionSummary>(`${this.base}/`, null);
  }

  get(id: string) {
    return this.http.get<SessionDetail>(`${this.base}/${id}`);
  }

  delete(id: string) {
    return this.http.delete(`${this.base}/${id}`);
  }

  upload(id: string, file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<{ id: string; filename: string; fileType: string }>(
      `${this.base}/${id}/upload`,
      form,
    );
  }

  chat(id: string, question: string, libraryDocumentIds: string[] = []) {
    return this.http.post<ChatResponse>(`${this.base}/${id}/chat`, {
      question,
      libraryDocumentIds,
    });
  }
}

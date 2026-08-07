import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { ChatResponse } from './models';

@Injectable({ providedIn: 'root' })
export class ChatApi {
  private http = inject(HttpClient);

  query(question: string, documentIds: string[]) {
    return this.http.post<ChatResponse>('/api/chat/', { question, documentIds });
  }
}

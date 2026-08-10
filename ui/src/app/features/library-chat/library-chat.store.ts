import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom, of } from 'rxjs';
import { SessionsApi } from '../../core/api/sessions.api';
import type { SessionDetail } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class LibraryChatStore {
  private api = inject(SessionsApi);
  private readonly LS_KEY = 'rag_library_session_id';

  readonly sessionId = signal<string | null>(localStorage.getItem(this.LS_KEY));
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  private pending = signal<{ id: string; role: 'user' | 'assistant'; content: string; createdAt: string }[]>([]);

  private sessionResource = rxResource<SessionDetail | null, string | null>({
    params: () => this.sessionId(),
    stream: ({ params: id }) => id ? this.api.get(id) : of(null),
    defaultValue: null,
  });

  readonly session = this.sessionResource.value;
  readonly loading = this.sessionResource.isLoading;

  readonly messages = computed(() => {
    const server = this.session()?.messages ?? [];
    const pending = this.pending();
    if (pending.length === 0) return server;
    const serverSet = new Set(server.map(m => `${m.role}:${m.content}`));
    return [...server, ...pending.filter(p => !serverSet.has(`${p.role}:${p.content}`))];
  });

  async sendMessage(question: string) {
    if (this.sending()) return;
    this.error.set(null);
    this.sending.set(true);

    // Track the optimistic message ID so we can remove it on failure
    const optimisticId = crypto.randomUUID();

    try {
      let id = this.sessionId();
      if (!id) {
        const s = await firstValueFrom(this.api.create());
        id = s.id;
        this.sessionId.set(id);
        localStorage.setItem(this.LS_KEY, id);
      }

      this.pending.update(l => [...l, {
        id: optimisticId,
        role: 'user' as const,
        content: question,
        createdAt: new Date().toISOString(),
      }]);

      const res = await firstValueFrom(this.api.libraryChat(id, question));

      this.pending.update(l => [...l, {
        id: crypto.randomUUID(),
        role: 'assistant' as const,
        content: res.answer,
        createdAt: new Date().toISOString(),
      }]);

      this.sessionResource.reload();
      setTimeout(() => this.pending.set([]), 500);
    } catch (err) {
      // Remove the stuck optimistic user message
      this.pending.update(l => l.filter(m => m.id !== optimisticId));
      const message =
        err instanceof HttpErrorResponse && typeof err.error === 'string'
          ? err.error
          : 'Something went wrong. Please try again.';
      this.error.set(message);
    } finally {
      this.sending.set(false);
    }
  }

  clearChat() {
    localStorage.removeItem(this.LS_KEY);
    this.error.set(null);
    this.pending.set([]);
    // Set sessionId last — triggers rxResource to switch to of(null), clearing session() reactively
    this.sessionId.set(null);
  }
}

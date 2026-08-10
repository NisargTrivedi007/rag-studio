import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom, of } from 'rxjs';
import { SessionsApi } from '../../core/api/sessions.api';
import type { SessionDetail, SessionSummary } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class ChatStore {
  private api = inject(SessionsApi);

  // Which session is currently open (null = new-chat / empty state)
  readonly currentSessionId = signal<string | null>(null);

  // Sessions list — reactive resource. Reload after mutations.
  private sessionsResource = rxResource<SessionSummary[], void>({
    stream: () => this.api.list(),
    defaultValue: [],
  });
  readonly sessions = this.sessionsResource.value;
  readonly sessionsLoading = this.sessionsResource.isLoading;

  // Currently-open session detail — auto-fetches when currentSessionId changes.
  private currentResource = rxResource<SessionDetail | null, string | null>({
    params: () => this.currentSessionId(),
    stream: ({ params: id }) => (id ? this.api.get(id) : of(null)),
    defaultValue: null,
  });
  readonly currentSession = this.currentResource.value;
  readonly currentLoading = this.currentResource.isLoading;

  // Optimistic message queue — appended locally while waiting on API.
  private pending = signal<{ id: string; role: 'user' | 'assistant'; content: string; createdAt: string }[]>([]);
  readonly sending = signal(false);

  // Merged view: server messages + optimistic pending
  readonly messages = computed(() => {
    const server = this.currentSession()?.messages ?? [];
    const pending = this.pending();
    if (pending.length === 0) return server;
    const serverSet = new Set(server.map(m => `${m.role}:${m.content}`));
    return [...server, ...pending.filter(p => !serverSet.has(`${p.role}:${p.content}`))];
  });

  readonly hasCurrent = computed(() => this.currentSession() !== null || this.pending().length > 0);

  selectSession(id: string) {
    this.pending.set([]);
    this.currentSessionId.set(id);
  }

  newChat() {
    this.pending.set([]);
    this.currentSessionId.set(null);
  }

  async deleteSession(id: string) {
    await firstValueFrom(this.api.delete(id));
    if (this.currentSessionId() === id) this.newChat();
    this.sessionsResource.reload();
  }

  async sendMessage(question: string, attachedFile?: File) {
    if (this.sending()) return;
    this.sending.set(true);
    try {
      // Lazy session creation on first message
      let id = this.currentSessionId();
      if (!id) {
        const created = await firstValueFrom(this.api.create());
        id = created.id;
        this.currentSessionId.set(id);
      }

      if (attachedFile) {
        await firstValueFrom(this.api.upload(id, attachedFile));
      }

      // Optimistic user message
      const userMsg = {
        id: crypto.randomUUID(),
        role: 'user' as const,
        content: question,
        createdAt: new Date().toISOString(),
      };
      this.pending.update(list => [...list, userMsg]);

      const response = await firstValueFrom(this.api.chat(id, question));
      const assistantMsg = {
        id: crypto.randomUUID(),
        role: 'assistant' as const,
        content: response.answer,
        createdAt: new Date().toISOString(),
      };
      this.pending.update(list => [...list, assistantMsg]);

      // Refresh sessions list (auto-title may have changed) and session detail
      this.sessionsResource.reload();
      this.currentResource.reload();

      // Once server has caught up, clear pending — reload will bring the same messages back canonically
      // Small delay lets reload complete first to avoid visual flicker
      setTimeout(() => this.pending.set([]), 500);
    } finally {
      this.sending.set(false);
    }
  }
}

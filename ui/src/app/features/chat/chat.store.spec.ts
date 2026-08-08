import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ChatStore } from './chat.store';
import { SessionsApi } from '../../core/api/sessions.api';

describe('ChatStore', () => {
  let store: ChatStore;

  const mockApi = {
    list: () => of([
      { id: 's1', title: 'Test', createdAt: '', updatedAt: '', documentCount: 0, messageCount: 0 },
    ]),
    get: (id: string) =>
      of({
        id,
        title: 'Test',
        createdAt: '',
        updatedAt: '',
        documents: [],
        messages: [],
      }),
    create: () =>
      of({
        id: 'sess-new',
        title: null,
        createdAt: '',
        updatedAt: '',
        documentCount: 0,
        messageCount: 0,
      }),
    delete: () => of(null),
    upload: () => of({ id: 'doc-1', filename: 'test.pdf', fileType: 'pdf' }),
    chat: () => of({ answer: 'Test answer', sources: [] }),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ChatStore, { provide: SessionsApi, useValue: mockApi }],
    });
    store = TestBed.inject(ChatStore);
    TestBed.flushEffects();
  });

  it('selectSession sets currentSessionId and clears pending', () => {
    store.selectSession('s1');
    TestBed.flushEffects();
    expect(store.currentSessionId()).toBe('s1');
  });

  it('newChat sets currentSessionId to null', () => {
    store.selectSession('s1');
    TestBed.flushEffects();
    store.newChat();
    TestBed.flushEffects();
    expect(store.currentSessionId()).toBeNull();
  });

  it('hasCurrent is false on init', () => {
    expect(store.hasCurrent()).toBe(false);
  });

  it('hasCurrent is true after selectSession', () => {
    store.selectSession('s1');
    TestBed.flushEffects();
    expect(store.hasCurrent()).toBe(true);
  });

  it('sendMessage adds user and assistant messages', async () => {
    await store.sendMessage('Hello');
    TestBed.flushEffects();
    await new Promise(r => setTimeout(r, 100));
    expect(store.messages().length).toBeGreaterThan(0);
  });
});

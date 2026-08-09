import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { LibraryChatStore } from './library-chat.store';
import { SessionsApi } from '../../core/api/sessions.api';

// Provide a localStorage shim for the test environment
const lsStore: Record<string, string> = {};
const localStorageMock = {
  getItem: (k: string) => lsStore[k] ?? null,
  setItem: (k: string, v: string) => { lsStore[k] = v; },
  removeItem: (k: string) => { delete lsStore[k]; },
  clear: () => { Object.keys(lsStore).forEach(k => delete lsStore[k]); },
};
vi.stubGlobal('localStorage', localStorageMock);

const SESSION = { id: 'sess-1', title: null, createdAt: '', updatedAt: '', documentCount: 0, messageCount: 0 };
const SESSION_DETAIL = { id: 'sess-1', title: null, createdAt: '', updatedAt: '', documents: [], messages: [] };

const makeApi = (libraryChatImpl?: () => any) => ({
  list: () => of([]),
  create: vi.fn(() => of(SESSION)),
  get: vi.fn(() => of(SESSION_DETAIL)),
  delete: () => of(null),
  upload: () => of({ id: 'doc-1', filename: 'f.pdf', fileType: 'pdf' }),
  chat: () => of({ answer: 'ok', sources: [] }),
  libraryChat: vi.fn(libraryChatImpl ?? (() => of({ answer: 'Library answer', sources: [] }))),
});

const setup = (api = makeApi()) => {
  TestBed.configureTestingModule({
    providers: [LibraryChatStore, { provide: SessionsApi, useValue: api }],
  });
  return { store: TestBed.inject(LibraryChatStore), api };
};

describe('LibraryChatStore', () => {
  beforeEach(() => {
    localStorageMock.clear();
    TestBed.resetTestingModule();
  });

  it('initialises with null sessionId when localStorage is empty', () => {
    const { store } = setup();
    expect(store.sessionId()).toBeNull();
  });

  it('initialises sessionId from localStorage when key exists', () => {
    localStorageMock.setItem('rag_library_session_id', 'stored-id');
    const { store } = setup();
    expect(store.sessionId()).toBe('stored-id');
  });

  it('messages is empty on init', () => {
    const { store } = setup();
    expect(store.messages()).toHaveLength(0);
  });

  it('error is null on init', () => {
    const { store } = setup();
    expect(store.error()).toBeNull();
  });

  it('sending is false on init', () => {
    const { store } = setup();
    expect(store.sending()).toBe(false);
  });

  it('sendMessage creates session if none exists', async () => {
    const api = makeApi();
    const { store } = setup(api);
    await store.sendMessage('Hello');
    expect(api.create).toHaveBeenCalledTimes(1);
    expect(store.sessionId()).toBe('sess-1');
    expect(localStorageMock.getItem('rag_library_session_id')).toBe('sess-1');
  });

  it('sendMessage does not create session if one already exists', async () => {
    localStorageMock.setItem('rag_library_session_id', 'existing-sess');
    const api = makeApi();
    const { store } = setup(api);
    await store.sendMessage('Hi');
    expect(api.create).not.toHaveBeenCalled();
  });

  it('sendMessage adds optimistic user and assistant messages', async () => {
    const { store } = setup();
    await store.sendMessage('Test question');
    const msgs = store.messages();
    expect(msgs.some(m => m.role === 'user' && m.content === 'Test question')).toBe(true);
    expect(msgs.some(m => m.role === 'assistant' && m.content === 'Library answer')).toBe(true);
  });

  it('sending is false after sendMessage completes', async () => {
    const { store } = setup();
    await store.sendMessage('Hi');
    expect(store.sending()).toBe(false);
  });

  it('error is set with backend message when API returns 400', async () => {
    const api = makeApi(() =>
      throwError(() => new HttpErrorResponse({
        error: 'No documents in library. Upload files from the Library page.',
        status: 400,
      }))
    );
    const { store } = setup(api);
    await store.sendMessage('Hello');
    expect(store.error()).toBe('No documents in library. Upload files from the Library page.');
  });

  it('shows generic error when API fails without a string body', async () => {
    const api = makeApi(() =>
      throwError(() => new HttpErrorResponse({ error: { code: 500 }, status: 500 }))
    );
    const { store } = setup(api);
    await store.sendMessage('Hello');
    expect(store.error()).toBe('Something went wrong. Please try again.');
  });

  it('optimistic user message is removed on API failure', async () => {
    const api = makeApi(() =>
      throwError(() => new HttpErrorResponse({ error: 'fail', status: 400 }))
    );
    const { store } = setup(api);
    await store.sendMessage('Hello');
    expect(store.messages().filter(m => m.role === 'user')).toHaveLength(0);
  });

  it('error is cleared at the start of the next sendMessage', async () => {
    let callCount = 0;
    const api = makeApi(() => {
      callCount++;
      return callCount === 1
        ? throwError(() => new HttpErrorResponse({ error: 'fail', status: 400 }))
        : of({ answer: 'ok', sources: [] });
    });
    const { store } = setup(api);
    await store.sendMessage('first');
    expect(store.error()).not.toBeNull();
    await store.sendMessage('second');
    expect(store.error()).toBeNull();
  });

  it('clearChat sets sessionId to null', () => {
    const { store } = setup();
    store.clearChat();
    expect(store.sessionId()).toBeNull();
  });

  it('clearChat removes localStorage key', () => {
    localStorageMock.setItem('rag_library_session_id', 'abc');
    const { store } = setup();
    store.clearChat();
    expect(localStorageMock.getItem('rag_library_session_id')).toBeNull();
  });

  it('clearChat clears pending messages', async () => {
    const { store } = setup();
    await store.sendMessage('Hi');
    store.clearChat();
    expect(store.messages()).toHaveLength(0);
  });

  it('clearChat clears error state', async () => {
    const api = makeApi(() =>
      throwError(() => new HttpErrorResponse({ error: 'fail', status: 400 }))
    );
    const { store } = setup(api);
    await store.sendMessage('Hi');
    expect(store.error()).not.toBeNull();
    store.clearChat();
    expect(store.error()).toBeNull();
  });
});

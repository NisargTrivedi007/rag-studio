import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { LibraryChatComponent } from './library-chat.component';
import { LibraryChatStore } from './library-chat.store';

const makeStore = (overrides: Partial<{
  messages: any[];
  sending: boolean;
  error: string | null;
}> = {}) => ({
  messages: signal(overrides.messages ?? []),
  sending: signal(overrides.sending ?? false),
  error: signal(overrides.error ?? null),
  loading: signal(false),
  session: signal(null),
  sendMessage: vi.fn(),
  clearChat: vi.fn(),
});

describe('LibraryChatComponent', () => {
  let fixture: any;
  let mockStore: ReturnType<typeof makeStore>;

  const setup = async (storeOverrides?: Parameters<typeof makeStore>[0]) => {
    mockStore = makeStore(storeOverrides);
    await TestBed.configureTestingModule({
      imports: [LibraryChatComponent],
      providers: [
        { provide: LibraryChatStore, useValue: mockStore },
        provideRouter([]),
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(LibraryChatComponent);
    fixture.detectChanges();
    TestBed.flushEffects();
    return fixture;
  };

  it('should create', async () => {
    await setup();
    expect(fixture.componentInstance).toBeTruthy();
  });

  // Empty state
  it('shows empty state prompt when no messages', async () => {
    await setup({ messages: [] });
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ask anything about your library documents');
  });

  it('shows Library page link in empty state', async () => {
    await setup({ messages: [] });
    const link = fixture.nativeElement.querySelector('a[href="/library"]');
    expect(link).toBeTruthy();
  });

  // Clear button
  it('does not show Clear button when no messages', async () => {
    await setup({ messages: [] });
    const text = fixture.nativeElement.textContent;
    expect(text).not.toContain('Clear');
  });

  it('shows Clear button when messages exist', async () => {
    await setup({
      messages: [{ id: '1', role: 'user', content: 'Hi', createdAt: '' }],
    });
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Clear');
  });

  it('calls clearChat when Clear is clicked', async () => {
    await setup({
      messages: [{ id: '1', role: 'user', content: 'Hi', createdAt: '' }],
    });
    fixture.detectChanges();
    const btn = Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .find((b: HTMLButtonElement) => b.textContent?.trim() === 'Clear');
    btn?.click();
    expect(mockStore.clearChat).toHaveBeenCalledTimes(1);
  });

  // Messages rendering
  it('renders user messages', async () => {
    await setup({
      messages: [{ id: '1', role: 'user', content: 'What is RAG?', createdAt: '' }],
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('What is RAG?');
  });

  // Error banner
  it('does not show error banner when error is null', async () => {
    await setup({ error: null });
    const banner = fixture.nativeElement.querySelector('.bg-red-50, .bg-red-950');
    expect(banner).toBeFalsy();
  });

  it('shows error banner with message when error is set', async () => {
    await setup({ error: 'No documents in library. Upload files from the Library page.' });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('No documents in library');
  });

  // Send button
  it('send button is disabled when text is empty', async () => {
    await setup();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Send"]');
    expect(btn.disabled).toBe(true);
  });

  it('send button is enabled when text is non-empty and not sending', async () => {
    await setup({ sending: false });
    (fixture.componentInstance as any).text.set('a question');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Send"]');
    expect(btn.disabled).toBe(false);
  });

  it('send button is disabled while sending', async () => {
    await setup({ sending: true });
    (fixture.componentInstance as any).text.set('a question');
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Send"]');
    expect(btn.disabled).toBe(true);
  });

  it('calls sendMessage with trimmed text on send', async () => {
    await setup();
    (fixture.componentInstance as any).text.set('  my question  ');
    fixture.detectChanges();
    await (fixture.componentInstance as any).send();
    expect(mockStore.sendMessage).toHaveBeenCalledWith('my question');
  });

  it('clears text after send', async () => {
    await setup();
    (fixture.componentInstance as any).text.set('hello');
    await (fixture.componentInstance as any).send();
    expect((fixture.componentInstance as any).text()).toBe('');
  });

  // Typing indicator
  it('shows typing indicator when sending', async () => {
    await setup({
      sending: true,
      messages: [{ id: '1', role: 'user', content: 'Hi', createdAt: '' }],
    });
    fixture.detectChanges();
    const dots = fixture.nativeElement.querySelectorAll('.animate-pulse');
    expect(dots.length).toBeGreaterThan(0);
  });

  it('hides typing indicator when not sending', async () => {
    await setup({ sending: false, messages: [] });
    fixture.detectChanges();
    const dots = fixture.nativeElement.querySelectorAll('.animate-pulse');
    expect(dots.length).toBe(0);
  });

  // Header
  it('renders "Library Chat" heading', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Library Chat');
  });

  it('renders back link to chat', async () => {
    await setup();
    const link = fixture.nativeElement.querySelector('a[href="/"]');
    expect(link).toBeTruthy();
  });
});

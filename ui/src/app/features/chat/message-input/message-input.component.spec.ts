import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { MessageInputComponent } from './message-input.component';
import { ChatStore } from '../chat.store';

const makeStore = (sessionDocCount = 0, sending = false) => ({
  sending: signal(sending),
  sendMessage: vi.fn(),
  currentSession: signal(
    sessionDocCount > 0
      ? { id: 's1', title: null, createdAt: '', updatedAt: '', documents: Array(sessionDocCount).fill({ id: 'd1', filename: 'f.pdf', fileType: 'pdf' }), messages: [] }
      : null
  ),
});

describe('MessageInputComponent — no session docs', () => {
  let fixture: any;
  let component: MessageInputComponent;
  let mockStore: ReturnType<typeof makeStore>;

  beforeEach(async () => {
    mockStore = makeStore(0);
    await TestBed.configureTestingModule({
      imports: [MessageInputComponent],
      providers: [{ provide: ChatStore, useValue: mockStore }],
    }).compileComponents();
    fixture = TestBed.createComponent(MessageInputComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    TestBed.flushEffects();
  });

  it('should create', () => expect(component).toBeTruthy());

  it('hasDocContext is false when no file and session is null', () => {
    expect(component.hasDocContext()).toBe(false);
  });

  it('hasDocContext is true when file is attached', () => {
    (component as any).attachedFile.set(new File(['x'], 'doc.pdf'));
    TestBed.flushEffects();
    expect(component.hasDocContext()).toBe(true);
  });

  it('canSend is false when text is empty and no doc context', () => {
    expect(component.canSend()).toBe(false);
  });

  it('canSend is false when text entered but no doc context', () => {
    (component as any).text.set('Hello');
    TestBed.flushEffects();
    expect(component.canSend()).toBe(false);
  });

  it('canSend is true when text entered and file attached', () => {
    (component as any).text.set('Hello');
    (component as any).attachedFile.set(new File([''], 'test.pdf'));
    TestBed.flushEffects();
    expect(component.canSend()).toBe(true);
  });

  it('canSend is false when sending is true even with file', () => {
    (component as any).text.set('Hello');
    (component as any).attachedFile.set(new File([''], 'test.pdf'));
    mockStore.sending.set(true);
    TestBed.flushEffects();
    expect(component.canSend()).toBe(false);
  });

  it('shows "Attach a document before sending" hint when no doc context', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Attach a document before sending');
  });

  it('send button is disabled when no doc context', () => {
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Send"]');
    expect(btn.disabled).toBe(true);
  });

  it('send button is enabled when text entered and file attached', () => {
    (component as any).text.set('question');
    (component as any).attachedFile.set(new File(['x'], 'f.pdf'));
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[aria-label="Send"]');
    expect(btn.disabled).toBe(false);
  });

  it('clearFile removes attached file', () => {
    (component as any).attachedFile.set(new File(['x'], 'f.pdf'));
    component.clearFile();
    expect((component as any).attachedFile()).toBeNull();
  });
});

describe('MessageInputComponent — session has documents', () => {
  let fixture: any;
  let component: MessageInputComponent;

  beforeEach(async () => {
    const mockStore = makeStore(1);
    await TestBed.configureTestingModule({
      imports: [MessageInputComponent],
      providers: [{ provide: ChatStore, useValue: mockStore }],
    }).compileComponents();
    fixture = TestBed.createComponent(MessageInputComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    TestBed.flushEffects();
  });

  it('hasDocContext is true when session has documents', () => {
    expect(component.hasDocContext()).toBe(true);
  });

  it('canSend is true when text entered and session has docs', () => {
    (component as any).text.set('My question');
    TestBed.flushEffects();
    expect(component.canSend()).toBe(true);
  });

  it('shows default hint when doc context is present', () => {
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Answers come from your attached document only');
  });
});

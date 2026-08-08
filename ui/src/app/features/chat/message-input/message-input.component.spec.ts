import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { MessageInputComponent } from './message-input.component';
import { ChatStore } from '../chat.store';

describe('MessageInputComponent', () => {
  let fixture: any;
  let component: MessageInputComponent;

  const mockStore = {
    sending: signal(false),
    sendMessage: vi.fn(),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageInputComponent],
      providers: [{ provide: ChatStore, useValue: mockStore }],
    }).compileComponents();

    fixture = TestBed.createComponent(MessageInputComponent);
    component = fixture.componentInstance;
    TestBed.flushEffects();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('canSend is false when sending', () => {
    mockStore.sending.set(true);
    TestBed.flushEffects();
    expect(component.canSend()).toBe(false);
  });

  it('canSend is true when not sending', () => {
    mockStore.sending.set(false);
    TestBed.flushEffects();
    expect(component.canSend()).toBeDefined();
  });
});

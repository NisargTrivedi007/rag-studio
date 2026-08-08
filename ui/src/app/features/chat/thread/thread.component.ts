import { Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { MarkdownComponent } from 'ngx-markdown';
import { ChatStore } from '../chat.store';

@Component({
  selector: 'app-thread',
  imports: [MarkdownComponent],
  templateUrl: './thread.component.html',
})
export class ThreadComponent {
  protected store = inject(ChatStore);
  private scrollContainer = viewChild<ElementRef<HTMLDivElement>>('scrollContainer');

  constructor() {
    // Auto-scroll to bottom whenever the message list or sending state changes.
    effect(() => {
      this.store.messages().length;
      this.store.sending();
      queueMicrotask(() => {
        const el = this.scrollContainer()?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }
}

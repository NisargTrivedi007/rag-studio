import { Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MarkdownComponent } from 'ngx-markdown';
import { LibraryChatStore } from './library-chat.store';
import { IconComponent } from '../../shared/icons/icon.component';

@Component({
  selector: 'app-library-chat',
  standalone: true,
  imports: [FormsModule, RouterLink, MarkdownComponent, IconComponent],
  templateUrl: './library-chat.component.html',
})
export class LibraryChatComponent {
  protected store = inject(LibraryChatStore);
  protected text = signal('');
  private scrollContainer = viewChild<ElementRef<HTMLDivElement>>('scrollContainer');

  constructor() {
    effect(() => {
      this.store.messages();
      this.store.sending();
      queueMicrotask(() => {
        const el = this.scrollContainer()?.nativeElement;
        if (el) el.scrollTop = el.scrollHeight;
      });
    });
  }

  handleEnter(event: Event) {
    const ke = event as KeyboardEvent;
    if (ke.shiftKey) return;
    ke.preventDefault();
    this.send();
  }

  async send() {
    const question = this.text().trim();
    if (!question || this.store.sending()) return;
    this.text.set('');
    await this.store.sendMessage(question);
  }
}

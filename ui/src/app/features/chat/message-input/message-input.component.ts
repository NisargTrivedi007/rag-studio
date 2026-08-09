import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatStore } from '../chat.store';
import { IconComponent } from '../../../shared/icons/icon.component';

@Component({
  selector: 'app-message-input',
  imports: [FormsModule, IconComponent],
  templateUrl: './message-input.component.html',
})
export class MessageInputComponent {
  protected store = inject(ChatStore);
  protected text = signal('');
  protected attachedFile = signal<File | null>(null);
  protected fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInputEl');

  readonly hasDocContext = computed(() =>
    this.attachedFile() !== null ||
    (this.store.currentSession()?.documents.length ?? 0) > 0
  );

  readonly canSend = computed(() =>
    this.text().trim().length > 0 &&
    !this.store.sending() &&
    this.hasDocContext()
  );

  handleEnter(event: Event) {
    const ke = event as KeyboardEvent;
    if (ke.shiftKey) return;
    ke.preventDefault();
    this.send();
  }

  onFileSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) this.attachedFile.set(file);
  }

  openFilePicker() {
    this.fileInput()?.nativeElement.click();
  }

  clearFile() {
    this.attachedFile.set(null);
    const el = this.fileInput()?.nativeElement;
    if (el) el.value = '';
  }

  async send() {
    if (!this.canSend()) return;
    const question = this.text();
    const file = this.attachedFile();
    this.text.set('');
    this.attachedFile.set(null);
    await this.store.sendMessage(question, file ?? undefined);
  }
}

import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: string;
  message: string;
  type: 'error' | 'info' | 'success';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);

  show(message: string, type: Toast['type'] = 'error') {
    const id = crypto.randomUUID();
    this.toasts.update(list => [...list, { id, message, type }]);
    setTimeout(() => this.dismiss(id), 4000);
  }

  dismiss(id: string) {
    this.toasts.update(list => list.filter(t => t.id !== id));
  }
}

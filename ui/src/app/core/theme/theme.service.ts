import { Injectable, effect, signal } from '@angular/core';

type Theme = 'dark' | 'light';
const KEY = 'ragui.theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private stored = (localStorage.getItem(KEY) as Theme | null) ?? 'dark';
  readonly theme = signal<Theme>(this.stored);

  constructor() {
    effect(() => {
      const t = this.theme();
      document.documentElement.setAttribute('data-theme', t);
      localStorage.setItem(KEY, t);
    });
  }

  toggle() {
    this.theme.update(t => (t === 'dark' ? 'light' : 'dark'));
  }
}

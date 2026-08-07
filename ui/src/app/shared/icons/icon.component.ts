import { Component, computed, input } from '@angular/core';

// Lucide-style icons as inline SVGs. No npm dep — one component, name-driven.
type IconName =
  | 'plus'
  | 'send'
  | 'trash'
  | 'sun'
  | 'moon'
  | 'paperclip'
  | 'library'
  | 'database'
  | 'message-square'
  | 'chevron-down'
  | 'menu'
  | 'x';

const PATHS: Record<IconName, string> = {
  plus: 'M12 5v14 M5 12h14',
  send: 'M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z M15 6l-6 6',
  trash: 'M3 6h18 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2 m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M10 11v6 M14 11v6',
  sun: 'M12 3v2 M12 19v2 M5 12H3 M21 12h-2 M18.364 5.636l-1.414 1.414 M7.05 16.95l-1.414 1.414 M18.364 18.364l-1.414-1.414 M7.05 7.05L5.636 5.636',
  moon: 'M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z',
  paperclip: 'M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48',
  library: 'M16 6l4 14 M12 6v14 M8 8v12 M4 4v16',
  database: 'M12 8c4.418 0 8-1.343 8-3s-3.582-3-8-3-8 1.343-8 3 3.582 3 8 3z M4 5v6c0 1.657 3.582 3 8 3s8-1.343 8-3V5 M4 11v6c0 1.657 3.582 3 8 3s8-1.343 8-3v-6',
  'message-square': 'M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z',
  'chevron-down': 'M6 9l6 6 6-6',
  menu: 'M3 12h18 M3 6h18 M3 18h18',
  x: 'M18 6L6 18 M6 6l12 12',
};

@Component({
  selector: 'app-icon',
  templateUrl: './icon.component.html',
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly size = input<number>(20);

  readonly segments = computed(() =>
    PATHS[this.name()].split(' M').map((s, i) => (i === 0 ? s : 'M' + s))
  );
}

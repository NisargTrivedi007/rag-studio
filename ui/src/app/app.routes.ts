import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/chat/chat.component').then(m => m.ChatComponent),
  },
  {
    path: 'library',
    loadComponent: () => import('./features/library/library.component').then(m => m.LibraryComponent),
  },
  {
    path: 'sql',
    loadComponent: () => import('./features/sql/sql.component').then(m => m.SqlComponent),
  },
  {
    path: 'library-chat',
    loadComponent: () => import('./features/library-chat/library-chat.component').then(m => m.LibraryChatComponent),
  },
  { path: '**', redirectTo: '' },
];

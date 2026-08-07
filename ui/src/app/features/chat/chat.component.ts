import { Component } from '@angular/core';
import { SidebarComponent } from './sidebar/sidebar.component';
import { ThreadComponent } from './thread/thread.component';
import { MessageInputComponent } from './message-input/message-input.component';

@Component({
  selector: 'app-chat',
  imports: [SidebarComponent, ThreadComponent, MessageInputComponent],
  templateUrl: './chat.component.html',
})
export class ChatComponent {}

import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { ChatStore } from '../chat.store';
import { ThemeService } from '../../../core/theme/theme.service';
import { IconComponent } from '../../../shared/icons/icon.component';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, IconComponent],
  templateUrl: './sidebar.component.html',
})
export class SidebarComponent {
  protected store = inject(ChatStore);
  protected theme = inject(ThemeService);

  async deleteSession(event: MouseEvent, id: string) {
    event.stopPropagation();
    await this.store.deleteSession(id);
  }
}

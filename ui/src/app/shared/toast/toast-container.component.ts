import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../core/toast/toast.service';
import { IconComponent } from '../icons/icon.component';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './toast-container.component.html',
  styleUrl: './toast-container.component.scss',
})
export class ToastContainerComponent {
  protected service = inject(ToastService);
}

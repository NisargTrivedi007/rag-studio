import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { DocumentsApi } from '../../core/api/documents.api';
import { DocumentSummary } from '../../core/api/models';
import { IconComponent } from '../../shared/icons/icon.component';

@Component({
  selector: 'app-library',
  standalone: true,
  imports: [RouterLink, CommonModule, IconComponent],
  templateUrl: './library.component.html',
})
export class LibraryComponent {
  private api = inject(DocumentsApi);
  protected uploading = signal(false);
  protected error = signal<string | null>(null);

  private docsResource = rxResource<DocumentSummary[], void>({
    stream: () => this.api.list(),
    defaultValue: [],
  });
  protected docs = this.docsResource.value;
  protected loading = this.docsResource.isLoading;

  async upload(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.error.set(null);
    this.uploading.set(true);
    try {
      await firstValueFrom(this.api.upload(file));
      this.docsResource.reload();
    } catch {
      this.error.set('Upload failed. Please try again.');
    } finally {
      this.uploading.set(false);
      input.value = '';
    }
  }

  async deleteDoc(id: string) {
    await firstValueFrom(this.api.delete(id));
    this.docsResource.reload();
  }

  protected formatDate(isoDate: string): string {
    return new Date(isoDate).toLocaleDateString();
  }
}

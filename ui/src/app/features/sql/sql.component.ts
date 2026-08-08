import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { SqlApi } from '../../core/api/sql.api';
import { SqlQueryResponse } from '../../core/api/models';
import { IconComponent } from '../../shared/icons/icon.component';

@Component({
  selector: 'app-sql',
  standalone: true,
  imports: [RouterLink, CommonModule, IconComponent],
  templateUrl: './sql.component.html',
})
export class SqlComponent {
  private api = inject(SqlApi);
  protected question = signal('');
  protected loading = signal(false);
  protected result = signal<SqlQueryResponse | null>(null);
  protected schema = signal<string | null>(null);
  protected showSchema = signal(false);

  readonly canAsk = computed(() => this.question().trim().length > 0 && !this.loading());
  readonly columns = computed(() => {
    const r = this.result();
    return r && r.results.length > 0 ? Object.keys(r.results[0]) : [];
  });

  async ask() {
    if (!this.canAsk()) return;
    this.loading.set(true);
    this.result.set(null);
    try {
      this.result.set(await firstValueFrom(this.api.query(this.question())));
    } finally {
      this.loading.set(false);
    }
  }

  async toggleSchema() {
    if (!this.schema()) {
      const r = await firstValueFrom(this.api.schema());
      this.schema.set(r.schema);
    }
    this.showSchema.update(v => !v);
  }

  protected handleKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.ask();
    }
  }
}

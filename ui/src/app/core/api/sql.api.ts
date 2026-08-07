import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { SqlQueryResponse, SqlSchemaResponse } from './models';

@Injectable({ providedIn: 'root' })
export class SqlApi {
  private http = inject(HttpClient);

  schema() {
    return this.http.get<SqlSchemaResponse>('/api/sql/schema');
  }

  query(question: string) {
    return this.http.post<SqlQueryResponse>('/api/sql/query', { question });
  }
}

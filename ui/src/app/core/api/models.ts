// TypeScript mirrors of API DTOs. Kept minimal — only the fields the UI actually uses.

export interface SessionSummary {
  id: string;
  title: string | null;
  createdAt: string;
  updatedAt: string;
  documentCount: number;
  messageCount: number;
}

export interface SessionDocument {
  id: string;
  filename: string;
  fileType: string;
}

export interface SessionMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

export interface SessionDetail {
  id: string;
  title: string | null;
  createdAt: string;
  updatedAt: string;
  documents: SessionDocument[];
  messages: SessionMessage[];
}

export interface DocumentSummary {
  id: string;
  filename: string;
  fileType: string;
  uploadedAt: string;
}

export interface ChatResponse {
  answer: string;
  sources: string[];
}

export interface SqlSchemaResponse {
  schema: string;
}

export interface SqlQueryResponse {
  sql: string;
  results: Record<string, unknown>[];
}

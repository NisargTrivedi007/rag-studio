# Product Requirements Document — RAG + NL-to-SQL API

**Project:** RagAndAI  
**Type:** Learning project (single-user, no auth, local AI)  
**Stack:** .NET 10, ASP.NET Core Minimal APIs, Semantic Kernel, PostgreSQL + pgvector, Ollama

---

## 1. Purpose

Build a backend API that demonstrates two distinct AI capabilities:

1. **RAG (Retrieval-Augmented Generation)** — Upload documents, ask questions grounded in their content. Works like ChatGPT with file attachments.
2. **NL-to-SQL** — Ask plain-English questions about a sample ecommerce database and get back the data (not just SQL, but the actual results).

This is a learning project. No frontend. No auth. No multi-user concerns.

---

## 2. Features

### Feature 1: Document Library (RAG)

Upload documents to a persistent library. Ask questions across any combination of them.

**Upload**
- Accept: `.pdf`, `.docx`, `.xlsx`, `.txt`, `.md`, `.csv`
- Extract text → chunk → embed → store in `document_chunks` (pgvector)
- Store document metadata in `documents` table

**Query**
- Embed the question → vector similarity search across selected docs → retrieve top-K chunks
- Build prompt with retrieved context → call LLM → return grounded answer + cited sources

**Delete**
- Remove document record + all its vector chunks

---

### Feature 2: Chat Sessions (Session-Scoped RAG)

ChatGPT/Gemini-style: create a session, attach documents to it, ask questions with follow-up support.

**Session lifecycle**
- Create empty session → returns session ID
- Upload a document attached to that session (not visible in library)
- Chat within the session — questions are answered using session documents; prior turns are included as context (last 10 messages)
- First question auto-sets the session title (first 60 chars)
- List sessions — shows title, doc count, message count, timestamps
- Delete session — cascades to chat messages, documents, vector chunks

**Key distinction from library**
- Library documents: `session_id = NULL` — shared, permanent
- Session documents: `session_id = <id>` — scoped, deleted with session

---

### Feature 3: NL-to-SQL

Ask plain-English questions about the sample ecommerce database and get real query results.

**Sample ecommerce schema:** `customers`, `products`, `orders`, `order_items`

**Flow**
1. Introspect schema (tables, columns, types, FKs)
2. Build prompt with schema + question
3. LLM generates SQL
4. Validate SQL (SELECT only, no DML)
5. Execute against PostgreSQL
6. Return rows + the generated SQL (for transparency)

**Safety:** Only SELECT statements allowed. Reject anything else.

---

### Feature 4: Health Check

`GET /health` — returns `{ "status": "ok" }`. For Docker/liveness probes.

---

## 3. API Endpoints

### Document Library

| Method | Path | Description |
|--------|------|-------------|
| POST | `/documents/upload` | Upload file to library |
| GET | `/documents` | List all library documents |
| DELETE | `/documents/{id}` | Delete document + chunks |

### RAG Chat (Library)

| Method | Path | Description |
|--------|------|-------------|
| POST | `/chat` | Ask question across selected library docs |

Request body:
```json
{
  "question": "What are the payment terms?",
  "documentIds": ["uuid1", "uuid2"]
}
```

Response:
```json
{
  "answer": "Payment is due within 30 days...",
  "sources": ["chunk content 1", "chunk content 2"]
}
```

### Sessions

| Method | Path | Description |
|--------|------|-------------|
| POST | `/sessions` | Create empty session |
| GET | `/sessions` | List all sessions (summary) |
| GET | `/sessions/{id}` | Get session details + history + docs |
| DELETE | `/sessions/{id}` | Delete session (cascade) |
| POST | `/sessions/{id}/upload` | Upload doc scoped to session |
| POST | `/sessions/{id}/chat` | Ask question within session |

### NL-to-SQL

| Method | Path | Description |
|--------|------|-------------|
| POST | `/sql/query` | Execute natural language query |
| GET | `/sql/schema` | Return introspected schema (debug) |

### Health

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Liveness check |

---

## 4. Data Model

### `documents`
| Column | Type | Notes |
|--------|------|-------|
| id | UUID | PK |
| filename | TEXT | required |
| file_type | TEXT | pdf / docx / xlsx / txt |
| uploaded_at | TIMESTAMPTZ | UTC |
| metadata | TEXT | nullable JSON |
| session_id | UUID | nullable FK → sessions; NULL = library doc |

### `document_chunks`
| Column | Type | Notes |
|--------|------|-------|
| id | UUID | PK |
| document_id | UUID | FK → documents |
| chunk_index | INT | sequential |
| content | TEXT | chunk text |
| embedding | vector(768) | nomic-embed-text output |
| created_at | TIMESTAMPTZ | UTC |

### `sessions`
| Column | Type | Notes |
|--------|------|-------|
| id | UUID | PK |
| title | TEXT | nullable; set from first message |
| created_at | TIMESTAMPTZ | UTC |
| updated_at | TIMESTAMPTZ | updated on each chat turn |

### `chat_messages`
| Column | Type | Notes |
|--------|------|-------|
| id | UUID | PK |
| session_id | UUID | FK → sessions ON DELETE CASCADE |
| role | TEXT | 'user' or 'assistant' |
| content | TEXT | message text |
| created_at | TIMESTAMPTZ | UTC |

### Ecommerce (sample data for NL-to-SQL)

`customers(id, name, email, created_at)`  
`products(id, name, category, price, stock)`  
`orders(id, customer_id, created_at, status, total)`  
`order_items(id, order_id, product_id, quantity, unit_price)`

---

## 5. AI Configuration

| Model | Role | Default |
|-------|------|---------|
| `nomic-embed-text` | Text embeddings (768-dim) | Ollama local |
| `llama3.1` | Chat completions (RAG answers, NL-to-SQL) | Ollama local |

Chunking defaults: `ChunkSize=512 words`, `Overlap=50 words`, `TopK=5`

---

## 6. Supported File Types

| Extension | Parser | Library |
|-----------|--------|---------|
| `.txt`, `.md`, `.csv` | `TextParser` | BCL `StreamReader` |
| `.pdf` | `PdfParser` | PdfPig 0.1.15 |
| `.docx` | `WordParser` | DocumentFormat.OpenXml 3.5.1 |
| `.xlsx` | `ExcelParser` | DocumentFormat.OpenXml 3.5.1 |

---

## 7. Out of Scope

- Authentication / authorization
- Multi-user support
- Session title editing
- LLM-generated session titles
- Auto-expiry / session cleanup jobs
- Cross-session document reuse
- Streaming responses
- History summarization beyond 10-message window
- Frontend / UI
- Cloud AI providers (Azure OpenAI, Anthropic) — local Ollama only

# RAG + NL-to-SQL — Design Spec
**Date:** 2026-08-05
**Stack:** .NET 10, PostgreSQL + pgvector, Ollama (local)

---

## Purpose

Learning project. Two features in one API:
1. **RAG** — upload documents, ask questions, get answers grounded in uploaded content
2. **NL-to-SQL** — ask questions in plain English against a sample ecommerce database, get SQL + results

Single user. No auth. Dockerized app, local Postgres, local Ollama.

---

## Approach

- **RAG**: Semantic Kernel handles ingestion pipeline, pgvector memory store, retrieval, and Ollama integration
- **NL-to-SQL**: Manual — schema introspection, prompt engineering, SQL validation, execution
- **File parsing**: Pre-SK — extract text from PDF/Word/Excel/txt, then hand to SK for chunking + embedding
- **AI provider**: Ollama (nomic-embed-text for embeddings, llama3.1 for chat). Config-based — switchable to phi4/OpenAI/Azure/Gemini later without code changes

---

## Architecture

```
[ASP.NET Core Minimal API — .NET 10]
        |                    |
[RAG Service]         [NL-to-SQL Service]
(Semantic Kernel)     (manual)
        |                    |
        +--------------------+
                 |
        [PostgreSQL + pgvector]
          - documents (metadata)
          - document_chunks (text + 768-dim embeddings)
          - ecommerce tables (orders, products, customers, order_items)
                 |
        [Ollama — local]
          - nomic-embed-text (embeddings, 768 dims)
          - llama3.1 (chat completion, default)
```

---

## Data Model

```sql
-- RAG: document metadata
CREATE TABLE documents (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    filename    TEXT NOT NULL,
    file_type   TEXT NOT NULL,       -- pdf, docx, xlsx, txt
    uploaded_at TIMESTAMPTZ DEFAULT NOW(),
    metadata    JSONB
);

-- RAG: chunked text + vector embeddings
CREATE TABLE document_chunks (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id UUID REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index INT NOT NULL,
    content     TEXT NOT NULL,
    embedding   VECTOR(768),         -- nomic-embed-text output dim
    created_at  TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX ON document_chunks USING ivfflat (embedding vector_cosine_ops);

-- NL-to-SQL: sample ecommerce schema
CREATE TABLE customers (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name       TEXT NOT NULL,
    email      TEXT UNIQUE NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE TABLE products (
    id       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name     TEXT NOT NULL,
    category TEXT,
    price    NUMERIC(10,2) NOT NULL,
    stock    INT DEFAULT 0
);
CREATE TABLE orders (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID REFERENCES customers(id),
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    status      TEXT DEFAULT 'pending',  -- pending, shipped, delivered, cancelled
    total       NUMERIC(10,2)
);
CREATE TABLE order_items (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id   UUID REFERENCES orders(id),
    product_id UUID REFERENCES products(id),
    quantity   INT NOT NULL,
    unit_price NUMERIC(10,2) NOT NULL
);
```

**Chunk config (tunable via appsettings):**
- Chunk size: 512 tokens
- Overlap: 50 tokens
- Top-K retrieval: 5 chunks per query

**Vector index:** `ivfflat` for approximate nearest-neighbor search. Switch to `hnsw` if dataset grows large.

---

## API Endpoints

### Document Management
```
POST   /documents/upload     multipart/form-data — upload file, returns document_id
GET    /documents            list all uploaded documents
DELETE /documents/{id}       delete document + all its chunks
```

### RAG Chat
```
POST   /chat
  body: { "document_ids": ["uuid", ...], "question": "string" }
  returns: { "answer": "string", "sources": ["chunk text excerpts"] }
```

**Chat flow:**
1. Embed question via Ollama (nomic-embed-text)
2. Cosine similarity search — top-5 chunks from selected documents
3. Build prompt: system context + chunks + question
4. Llama generates grounded answer
5. Return answer + source chunk excerpts

### NL-to-SQL
```
POST   /sql/query
  body: { "question": "show me all orders over $100 from last month" }
  returns: { "sql": "SELECT ...", "results": [...], "explanation": "string" }

GET    /sql/schema            returns current ecommerce schema (for debugging/inspection)
```

**NL-to-SQL flow:**
1. Introspect Postgres — load table names, column names, types, foreign keys
2. Build prompt: schema context + user question
3. Llama generates SQL (instructed to output SQL only, no prose)
4. Validate: parse SQL AST, reject any destructive statements (DROP/DELETE/UPDATE/INSERT/ALTER/TRUNCATE)
5. Execute SELECT against DB
6. Return rows + brief LLM-generated explanation of results

### Health
```
GET    /health               checks Postgres connectivity + Ollama reachability
```

---

## Error Handling

| Scenario | Behavior |
|---|---|
| File parse fails | Return 400 with reason; no partial document stored |
| Ollama unreachable | Return 503; suggest checking `/health` |
| NL-to-SQL generates destructive SQL | Reject with explanation; never execute |
| NL-to-SQL generates invalid SQL syntax | Return 422 with generated SQL + parse error |
| Embedding fails mid-upload | Rollback entire document (delete from `documents` + any partial chunks) |
| Unknown file type | Return 400 |

---

## Project Structure

```
RagAndAI/
├── src/
│   ├── Api/
│   │   ├── Program.cs                  -- app bootstrap, DI, middleware
│   │   ├── Endpoints/
│   │   │   ├── DocumentEndpoints.cs
│   │   │   ├── ChatEndpoints.cs
│   │   │   └── SqlEndpoints.cs
│   ├── Services/
│   │   ├── Rag/
│   │   │   ├── RagService.cs           -- SK kernel, ingestion, retrieval
│   │   │   └── ChunkingConfig.cs       -- chunk size, overlap, top-K
│   │   ├── NlToSql/
│   │   │   ├── NlToSqlService.cs       -- orchestrates flow
│   │   │   ├── SchemaInspector.cs      -- reads Postgres schema at runtime
│   │   │   ├── SqlPromptBuilder.cs     -- builds LLM prompt with schema context
│   │   │   └── SqlValidator.cs         -- blocks destructive statements
│   │   └── FileParser/
│   │       ├── IFileParser.cs
│   │       ├── PdfParser.cs            -- PdfPig
│   │       ├── WordParser.cs           -- DocumentFormat.OpenXml
│   │       ├── ExcelParser.cs          -- flattens to CSV-like text
│   │       └── TextParser.cs
│   ├── Data/
│   │   ├── AppDbContext.cs             -- EF Core context
│   │   ├── Migrations/
│   │   └── Models/
│   │       ├── Document.cs
│   │       └── DocumentChunk.cs
│   └── Config/
│       ├── AiProviderConfig.cs         -- Ollama endpoint, model names
│       └── ChunkConfig.cs
├── docker-compose.yml                  -- app service + postgres
├── Dockerfile
├── appsettings.json
└── README.md
```

---

## Dependencies

| Package | Purpose |
|---|---|
| `Microsoft.SemanticKernel` | RAG pipeline, Ollama connector |
| `Microsoft.SemanticKernel.Connectors.Postgres` | pgvector memory store |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Postgres + EF Core |
| `Pgvector.EntityFrameworkCore` | Vector type support |
| `PdfPig` | PDF text extraction |
| `DocumentFormat.OpenXml` | Word (.docx) + Excel (.xlsx) parsing |

---

## What's Out of Scope

- Authentication / multi-user
- Frontend UI (API only)
- Fine-tuning or model training
- Multiple vector DB support
- Document management UI
- Streaming responses
- Caching layer

All of the above can be added later without redesigning the core.

---

## Chat Sessions (Added 2026-08-06)

Alongside single-shot library chat, sessions enable ChatGPT/Gemini-style chats with:
- Session-scoped file uploads (docs live only with that session)
- Follow-up questions (chat history preserved and used as context)
- Session listing and continuation

### Additional Data Model

```sql
CREATE TABLE sessions (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title      TEXT,                             -- auto: first 60 chars of first user message
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE chat_messages (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID REFERENCES sessions(id) ON DELETE CASCADE,
    role       TEXT NOT NULL,                    -- 'user' or 'assistant'
    content    TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX ON chat_messages (session_id, created_at);

ALTER TABLE documents ADD COLUMN session_id UUID
    REFERENCES sessions(id) ON DELETE CASCADE;   -- NULL = library; set = session-scoped
```

### Additional Endpoints

```
POST   /sessions                     create empty session, returns { id, createdAt }
GET    /sessions                     list all summaries (title, docCount, msgCount, timestamps), sort updated_at DESC
GET    /sessions/{id}                details: metadata + attached docs + full history
DELETE /sessions/{id}                cascade delete session + docs + chunks + messages

POST   /sessions/{id}/upload         upload doc scoped to this session
POST   /sessions/{id}/chat           body: { question, libraryDocumentIds? }
                                      returns { answer, sources, messageId }
```

### Session Chat Flow

1. Load session (404 if missing)
2. Resolve document IDs: session's own docs + optional library docs from request
3. Load last 10 chat messages for session (5 user + 5 assistant pairs)
4. Embed question, retrieve top-K chunks filtered by resolved doc IDs
5. Build prompt: system + previous conversation + retrieved context + current question
6. LLM generates answer
7. Persist user + assistant messages; set `title` on first user message; update `updated_at`
8. Return `{ answer, sources, messageId }`

### Session-Specific Design Decisions

- **Title:** auto-set from first user message (first 60 chars). No manual edit endpoint.
- **History window:** last 10 messages. No summarization.
- **Cascade delete:** session delete removes chat_messages + session-scoped documents; document deletion removes vector-store chunks.
- **No expiry, no pagination, no auth** — single-user learning project.
- **Library `/chat` and `/documents/*` unchanged** — both models coexist.

---

## Configuration (appsettings.json shape)

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "ChatModel": "llama3.1"
  },
  "Postgres": {
    "ConnectionString": "Host=localhost;Database=ragdb;Username=postgres;Password=..."
  },
  "Chunking": {
    "ChunkSize": 512,
    "Overlap": 50,
    "TopK": 5
  }
}
```

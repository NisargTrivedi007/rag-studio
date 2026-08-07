# Architecture — RagAndAI

**Stack:** .NET 10 · ASP.NET Core Minimal APIs · Semantic Kernel 1.79.0 · EF Core 10 · PostgreSQL + pgvector · Ollama

---

## 1. High-Level Flow

```
HTTP Request
    │
    ▼
ASP.NET Core Minimal API (Program.cs route groups)
    │
    ├── Features/Documents/   → FileParserFactory → IRagService.IngestAsync
    ├── Features/Chat/        → IRagService.QueryAsync
    ├── Features/Sessions/    → IRagService (ingest + query with history)
    ├── Features/SqlQuery/    → INlToSqlService
    └── Features/Health/      → 200 OK
         │
         ▼
    Services Layer
    ├── FileParserFactory     → IFileParser (Text/Pdf/Word/Excel)
    ├── RagService            → ITextEmbeddingGenerationService (SK+Ollama)
    │                         → IChatCompletionService (SK+Ollama)
    │                         → AppDbContext (EF Core + pgvector)
    └── NlToSqlService        → SchemaInspector → SqlPromptBuilder
                              → SqlValidator → IChatCompletionService
                              → AppDbContext (raw SQL execution)
         │
         ▼
    Infrastructure
    ├── Ollama (localhost:11434) — nomic-embed-text (768-dim), llama3.1
    └── PostgreSQL (ragdb)      — documents, document_chunks (pgvector),
                                  sessions, chat_messages, ecommerce tables
```

---

## 2. Project Structure

```
D:\Projects\RAG_And_AI\
├── src\
│   └── RagAndAI.Api\
│       ├── Program.cs                    — DI registration + route group wiring
│       ├── appsettings.json              — Ollama, ConnectionStrings, Chunking config
│       ├── Config\
│       │   ├── OllamaConfig.cs           — Endpoint, EmbeddingModel, ChatModel
│       │   └── ChunkingConfig.cs         — ChunkSize, Overlap, TopK
│       ├── Data\
│       │   ├── AppDbContext.cs           — EF DbContext, pgvector schema config
│       │   ├── AppDbContextFactory.cs   — design-time factory for EF migrations
│       │   └── Models\
│       │       ├── Document.cs           — uploaded file metadata
│       │       ├── DocumentChunkRecord.cs— vector chunk (Pgvector.Vector embedding)
│       │       ├── Session.cs            — chat session (Tasks 16+)
│       │       ├── ChatMessage.cs        — session message history (Tasks 16+)
│       │       ├── Customer.cs           — ecommerce sample
│       │       ├── Product.cs            — ecommerce sample
│       │       ├── Order.cs              — ecommerce sample
│       │       └── OrderItem.cs          — ecommerce sample
│       ├── Features\
│       │   ├── Documents\
│       │   │   ├── Upload.cs             — POST /documents/upload
│       │   │   ├── List.cs               — GET /documents
│       │   │   └── Delete.cs             — DELETE /documents/{id}
│       │   ├── Chat\
│       │   │   └── Query.cs              — POST /chat
│       │   ├── Sessions\
│       │   │   ├── Create.cs             — POST /sessions
│       │   │   ├── List.cs               — GET /sessions
│       │   │   ├── Get.cs                — GET /sessions/{id}
│       │   │   ├── Delete.cs             — DELETE /sessions/{id}
│       │   │   ├── Upload.cs             — POST /sessions/{id}/upload
│       │   │   └── Chat.cs               — POST /sessions/{id}/chat
│       │   ├── SqlQuery\
│       │   │   ├── Execute.cs            — POST /sql/query
│       │   │   └── Schema.cs             — GET /sql/schema
│       │   └── Health\
│       │       └── Check.cs              — GET /health
│       ├── Migrations\
│       │   ├── 20260806181501_InitialSchema.cs    — customers, products, orders, documents
│       │   └── 20260807034942_AddDocumentChunks.cs — document_chunks (vector(768))
│       └── Services\
│           ├── FileParser\
│           │   ├── IFileParser.cs        — ExtractTextAsync(Stream, fileName)
│           │   ├── FileParserFactory.cs  — dispatch by file extension
│           │   ├── TextParser.cs         — .txt / .md / .csv (StreamReader)
│           │   ├── PdfParser.cs          — .pdf (PdfPig)
│           │   ├── WordParser.cs         — .docx (OpenXml)
│           │   └── ExcelParser.cs        — .xlsx (OpenXml, tab-separated cells)
│           └── Rag\
│               ├── IRagService.cs        — IngestAsync, QueryAsync, DeleteDocumentChunksAsync
│               └── RagService.cs         — chunking + embedding + vector search + LLM answer
├── tests\
│   └── RagAndAI.Tests\
│       ├── FileParser\
│       │   ├── TextParserTests.cs
│       │   ├── PdfParserTests.cs         — skipped (needs fixture file)
│       │   ├── WordParserTests.cs
│       │   ├── ExcelParserTests.cs
│       │   └── FileParserFactoryTests.cs
│       └── Rag\
│           └── RagServiceTests.cs        — mocked via IRagService (no EF/pgvector)
├── docs\
│   ├── PRD.md                            — product requirements, endpoints, data model
│   ├── Architecture.md                   — this file
│   └── superpowers\
│       ├── specs\2026-08-05-rag-nlsql-design.md
│       └── plans\2026-08-06-rag-nlsql-implementation.md
└── CLAUDE.md                             — coding guidelines + dev commands
```

---

## 3. Dependency Injection (Program.cs)

```csharp
// Config
services.Configure<OllamaConfig>(config.GetSection("Ollama"));
services.Configure<ChunkingConfig>(config.GetSection("Chunking"));

// EF Core + pgvector
services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(connectionString, o => o.UseVector()));

// Semantic Kernel — Ollama
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaTextEmbeddingGeneration(model, endpoint);
kernelBuilder.AddOllamaChatCompletion(model, endpoint);
var kernel = kernelBuilder.Build();

// Register SK services as singletons
services.AddSingleton<ITextEmbeddingGenerationService>(...);
services.AddSingleton<IChatCompletionService>(...);

// Application services
services.AddSingleton<FileParserFactory>();
services.AddScoped<IRagService, RagService>();

// Route groups (added in Task 8+)
app.MapGroup("/documents").MapDocumentEndpoints();
app.MapGroup("/chat").MapChatEndpoints();
app.MapGroup("/sessions").MapSessionEndpoints();
app.MapGroup("/sql").MapSqlEndpoints();
app.MapHealthEndpoint();
```

---

## 4. RAG Pipeline

### Ingestion (upload → embed → store)

```
Upload request (file bytes + filename)
    │
    ▼
FileParserFactory.GetParser(extension)
    → IFileParser.ExtractTextAsync(stream)   — returns plain text
    │
    ▼
RagService.IngestAsync(documentId, text)
    → ChunkText(text, chunkSize=512, overlap=50)
      → split by spaces, sliding window, step = chunkSize - overlap
    → embeddingService.GenerateEmbeddingsAsync(chunks)
      → Ollama nomic-embed-text → List<ReadOnlyMemory<float>> (768-dim each)
    → for each chunk: new DocumentChunkRecord { Embedding = new Vector(floats) }
    → db.SaveChangesAsync()
```

### Query (question → retrieve → answer)

```
POST /chat { question, documentIds[] }
    │
    ▼
RagService.QueryAsync(question, documentIds)
    → embeddingService.GenerateEmbeddingsAsync([question])   — 768-dim vector
    → db.DocumentChunks
        .Where(c => documentIds.Contains(c.DocumentId))
        .OrderBy(c => c.Embedding.CosineDistance(queryVector))
        .Take(topK)
        .ToListAsync()
    → build prompt:
        System: "Answer using ONLY the context below..."
        Context: chunk1 \n--- \nchunk2 \n...
        Question: <question>
    → chatService.GetChatMessageContentAsync(prompt)
    → return RagResult(answer, sources: chunk content list)
```

### Session Chat (with conversation history)

Same as above, with history prepended to the prompt:

```
Previous conversation:
User: <msg>
Assistant: <msg>
...

Retrieved context:
<chunks>

Current question: <question>
```

History window: last 10 messages (5 turns). Older messages ignored.

---

## 5. NL-to-SQL Pipeline

```
POST /sql/query { question }
    │
    ▼
SchemaInspector.GetSchemaAsync()
    → queries information_schema for table/column/FK metadata
    → returns structured schema description string

SqlPromptBuilder.Build(question, schema)
    → formats prompt: schema + question + "Generate a PostgreSQL SELECT query"

IChatCompletionService.GetChatMessageContentAsync(prompt)
    → LLM returns raw SQL

SqlValidator.Validate(sql)
    → reject if not SELECT (blocks INSERT/UPDATE/DELETE/DROP)
    → basic sanity check on statement structure

AppDbContext.Database.SqlQueryRaw<Dictionary>(sql)
    → execute against PostgreSQL
    → return rows as List<Dictionary<string, object>>

return { sql, results }
```

---

## 6. Key Technical Decisions

### pgvector via EF+Npgsql (not SK Postgres connector)

`Microsoft.SemanticKernel.Connectors.Postgres` 1.51.0-preview is incompatible with SK 1.79.0. Using EF Core + Npgsql with the Pgvector package instead.

- `DocumentChunkRecord.Embedding` is `Pgvector.Vector` (CLR type)
- Mapped to `vector(768)` column via `HasColumnType("vector(768)")`
- `UseVector()` called in both `Program.cs` (runtime) and `AppDbContextFactory` (migrations)
- Cosine similarity: `.OrderBy(c => c.Embedding.CosineDistance(queryVector))`

### Unit tests mock IRagService

`Pgvector.Vector` is not supported by EF in-memory provider. Tests that need to verify chunking math or service behavior mock `IRagService` via NSubstitute. Real vector storage is an integration concern requiring a live Postgres.

### Vertical slice feature structure

Each feature lives in its own folder under `Features/`. No Clean Architecture layers, no MediatR. One file = one endpoint. Handler logic is inline in the minimal API delegate or a thin static class.

### DeleteDocumentChunksAsync uses RemoveRange

`ExecuteDeleteAsync` is not supported by in-memory EF provider. Using `ToListAsync` + `RemoveRange` + `SaveChangesAsync` for testability.

---

## 7. Database Schema

```sql
-- RAG documents
CREATE TABLE documents (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    filename    TEXT NOT NULL,
    file_type   TEXT NOT NULL,
    uploaded_at TIMESTAMPTZ DEFAULT NOW(),
    metadata    TEXT,
    session_id  UUID REFERENCES sessions(id) ON DELETE CASCADE  -- NULL = library
);

-- Vector chunks
CREATE EXTENSION IF NOT EXISTS vector;
CREATE TABLE document_chunks (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id  UUID REFERENCES documents(id) ON DELETE CASCADE,
    chunk_index  INT NOT NULL,
    content      TEXT NOT NULL,
    embedding    vector(768) NOT NULL,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX ON document_chunks (document_id);

-- Sessions (Task 16+)
CREATE TABLE sessions (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title      TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE chat_messages (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID REFERENCES sessions(id) ON DELETE CASCADE,
    role       TEXT NOT NULL,   -- 'user' | 'assistant'
    content    TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX ON chat_messages (session_id, created_at);

-- Ecommerce (NL-to-SQL sample data)
CREATE TABLE customers (id UUID PK, name TEXT, email TEXT UNIQUE, created_at TIMESTAMPTZ);
CREATE TABLE products  (id UUID PK, name TEXT, category TEXT, price DECIMAL, stock INT);
CREATE TABLE orders    (id UUID PK, customer_id UUID FK, created_at TIMESTAMPTZ, status TEXT, total DECIMAL);
CREATE TABLE order_items (id UUID PK, order_id UUID FK, product_id UUID FK, quantity INT, unit_price DECIMAL);
```

---

## 8. Configuration Reference

**appsettings.json**
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "ChatModel": "llama3.1"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ragdb;Username=postgres;Password=yourpassword"
  },
  "Chunking": {
    "ChunkSize": 512,
    "Overlap": 50,
    "TopK": 5
  }
}
```

---

## 9. NuGet Packages

**API project (RagAndAI.Api)**

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.SemanticKernel | 1.79.0 | SK core + kernel builder |
| Microsoft.SemanticKernel.Connectors.Ollama | 1.79.0-alpha | Ollama embedding + chat |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | EF Core Postgres provider |
| Pgvector.EntityFrameworkCore | 0.3.0 | pgvector CLR type + EF support |
| DocumentFormat.OpenXml | 3.5.1 | Word + Excel parsing |
| PdfPig | 0.1.15 | PDF text extraction |
| Microsoft.AspNetCore.OpenApi | 10.0.10 | OpenAPI/Swagger |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | EF migrations tooling |

**Test project (RagAndAI.Tests)**

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.9.3 | Test framework |
| NSubstitute | 6.0.0 | Mocking |
| FluentAssertions | 8.10.0 | Assertion library |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.10 | In-memory EF for tests |

---

## 10. Development Commands

```bash
# Run API
dotnet run --project api/src/RagAndAI.Api/RagAndAI.Api.csproj

# Run tests
dotnet test

# Add EF migration
dotnet ef migrations add <Name> --project api/src/RagAndAI.Api

# Apply migrations
dotnet ef database update --project api/src/RagAndAI.Api

# Build check
dotnet build
```


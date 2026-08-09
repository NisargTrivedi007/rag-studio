# RAG Studio

A full-stack AI playground built to learn and demonstrate **Retrieval-Augmented Generation (RAG)** and **Natural Language to SQL** — two of the most practical AI patterns in production today.

Upload your documents, chat with them using a local LLM, search across your entire library, and query a real database in plain English. Everything runs locally — no cloud API keys required.

---

## Features

### 📄 Document Library
Upload PDFs, Word docs, Excel sheets, Markdown, and plain text. Files are chunked, embedded, and stored in a PostgreSQL vector database (pgvector). Manage your library from a clean list view with search.

### 💬 Session Chat (File-Scoped RAG)
Create a chat session, attach a document, and have a multi-turn conversation grounded in that file. Conversation history is maintained across messages for natural follow-ups. Sessions are isolated — each one has its own documents and message thread.

### 🗂️ Library Chat (Knowledge Base RAG)
Chat with your entire document library at once. Ask questions that span multiple uploaded files. Conversation history is persisted across page reloads via a single persistent session stored in the browser.

### 🔍 Data Explorer (NL-to-SQL)
Ask plain-English questions about a sample e-commerce database (customers, products, orders). The LLM translates your question into SQL, executes it, and returns real results — not just the query.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core Minimal APIs |
| AI / Embeddings | Semantic Kernel 1.79 + Ollama (`nomic-embed-text`, `llama3.1`) |
| Vector Search | PostgreSQL + pgvector via EF Core 10 + Npgsql |
| ORM | Entity Framework Core 10 |
| File Parsing | PdfPig (PDF), DocumentFormat.OpenXml (Word/Excel) |
| Frontend | Angular 22 — Signals, Standalone Components, Zoneless |
| Styling | Tailwind CSS v4 |
| Testing | xUnit + FluentAssertions (API), Vitest + Angular TestBed (UI) |
| Container | Docker + nginx (production UI build) |

---

## Architecture

```
Browser (Angular 22)
    │  /api/*  (nginx reverse proxy)
    ▼
ASP.NET Core Minimal APIs (.NET 10)
    │
    ├── /documents/*      → FileParserFactory → RagService.IngestAsync
    ├── /sessions/*       → RagService.QueryAsync (with history)
    ├── /chat             → RagService.QueryAsync
    └── /sql/*            → NlToSqlService → raw SQL execution
                │
                ▼
    Ollama (localhost:11434)
    ├── nomic-embed-text  → 768-dim embeddings
    └── llama3.1          → chat completions
                │
                ▼
    PostgreSQL (ragdb)
    ├── documents + document_chunks  (pgvector)
    ├── sessions + chat_messages
    └── customers, products, orders  (e-commerce seed data)
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org)
- [PostgreSQL 16+](https://www.postgresql.org/) with the [pgvector extension](https://github.com/pgvector/pgvector)
- [Ollama](https://ollama.com/) with the required models

### 1. Pull Ollama models

```bash
ollama pull nomic-embed-text
ollama pull llama3.1
```

### 2. Create the database

```sql
CREATE DATABASE ragdb;
\c ragdb
CREATE EXTENSION vector;
```

### 3. Configure the connection string

Edit `api/src/RagAndAI.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ragdb;Username=postgres;Password=yourpassword"
  }
}
```

### 4. Run the API

```bash
dotnet run --project api/src/RagAndAI.Api/RagAndAI.Api.csproj
```

EF migrations run automatically on startup. The API will be available at `http://localhost:5247`.

### 5. Run the UI

```bash
cd ui
npm install
npm start
```

Open `http://localhost:4200`.

---

## Docker (Full Stack)

```bash
docker compose up --build
```

| Service | URL |
|---------|-----|
| UI | http://localhost:3000 |
| API | http://localhost:5247 |

> Requires PostgreSQL and Ollama running on the host. The compose file uses `host.docker.internal` to reach them.

---

## Running Tests

**API (integration tests — requires live PostgreSQL + Ollama):**

```bash
dotnet test api/
```

**UI (unit tests):**

```bash
cd ui && npm test
```

**UI (E2E — requires full stack running):**

```bash
cd ui && npm run e2e
```

---

## Project Structure

```
rag-studio/
├── api/
│   ├── src/RagAndAI.Api/
│   │   ├── Config/          — OllamaConfig, ChunkingConfig
│   │   ├── Data/Models/     — EF entities (Document, Session, ChatMessage, …)
│   │   ├── Features/        — Vertical slice handlers (Documents, Chat, Sessions, SqlQuery)
│   │   ├── Services/
│   │   │   ├── FileParser/  — PDF, Word, Excel, Text parsers
│   │   │   └── Rag/         — RagService (embed → search → LLM)
│   │   └── Program.cs       — DI + route registration
│   └── tests/RagAndAI.Tests/
│       ├── Integration/     — HTTP-level integration tests
│       └── FileParser/      — Unit tests for document parsers
└── ui/
    └── src/app/
        ├── core/api/        — Typed API clients
        ├── features/
        │   ├── chat/        — Session chat + sidebar + thread
        │   ├── library/     — Document library management
        │   ├── library-chat/— Library-wide RAG chat
        │   └── sql/         — NL-to-SQL data explorer
        └── shared/          — Icons, theme service
```

---

## API Reference

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/documents/upload` | Upload file to library (multipart) |
| `GET` | `/documents/` | List all library documents |
| `DELETE` | `/documents/{id}` | Delete document + vector chunks |
| `POST` | `/chat/` | RAG query over explicit document IDs |
| `POST` | `/sessions/` | Create a new chat session |
| `GET` | `/sessions/` | List all sessions |
| `GET` | `/sessions/{id}` | Get session detail + messages |
| `DELETE` | `/sessions/{id}` | Delete session (cascades) |
| `POST` | `/sessions/{id}/upload` | Upload document scoped to session |
| `POST` | `/sessions/{id}/chat` | Chat using session documents |
| `POST` | `/sessions/{id}/library-chat` | Chat using all library documents |
| `POST` | `/sql/query` | Natural language → SQL → results |
| `GET` | `/sql/schema` | Get database schema |
| `GET` | `/health` | Liveness probe |

---

## License

MIT

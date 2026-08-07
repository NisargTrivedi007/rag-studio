# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **New session? Check `docs/PROGRESS.md` first** — it shows the last completed task, next task, and build/test status so you can pick up immediately without re-reading the whole project.

---

# 📋 Instructions

## Core Development Principles

- **KISS (Keep It Simple, Stupid):** Do not overengineer. Prioritize readability and maintainability over clever or overly abstract solutions.
- **Pragmatic Refactoring:** Refactor only when it improves clarity or performance. Do not refactor for the sake of "perfection" if it complicates the logic.
- **Documentation:** Always include meaningful doc comments on public methods. Focus on the "Why" behind a specific implementation, not just the "What."

## Communication & Workflow

- **Big Picture Changes:** Before performing major architectural shifts, breaking changes, or introducing new dependencies, stop and ask for approval. Provide a brief rationale for the proposed change.
- **Incremental Progress:** Focus on one feature or refactor at a time to ensure the codebase remains stable.
- **Context Awareness:** When suggesting code, ensure it aligns with the existing project structure and naming conventions.
- **Confidence Rule:** **DO NOT** implement anything until you are 95% confident in the approach. Ask me follow-up questions until you reach that confidence.
- **Verify compatibility first:** Before writing code that uses a NuGet package, EF provider feature, or third-party type, confirm the version is compatible with the rest of the stack. Do not change the code after the fact.

## Git Workflow (MANDATORY)

**Before starting ANY work:**

1. **Check current branch:** `git branch`
2. **Commit to master directly** — this is a single-user learning project, no PR workflow required.

**During development:**

- Commit frequently with clear messages: `git commit -m "feat: implement RagService.QueryAsync"`
- One commit per logical unit of work (one task = one commit).

**When task is complete:**

- Ensure all tests pass: `dotnet test`
- Ensure build is clean: `dotnet build`
- Commit with a descriptive message.

## Behavioral Guidelines

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

> *Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.*

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

**When your changes create orphans:**

- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

> *The test: Every changed line should trace directly to the user's request.*

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**
Transform tasks into verifiable goals:

- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Implement X" → "Build + tests green before committing"

---

# 🛠️ Development Commands

## API (.NET 10, C#)

```bash
# Run API locally
dotnet run --project api/src/RagAndAI.Api/RagAndAI.Api.csproj

# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "FullyQualifiedName~RagServiceTests" --logger "console;verbosity=detailed"

# Create a migration (after schema changes)
dotnet ef migrations add <MigrationName> --project api/src/RagAndAI.Api

# Apply migrations
dotnet ef database update --project api/src/RagAndAI.Api

# Build (checks for errors without running)
dotnet build

# Clean build artifacts
dotnet clean
```

---

# 🏗️ Architecture Overview

## High-Level Stack

| Layer | Tech |
|-------|------|
| Runtime | .NET 10, ASP.NET Core Minimal APIs |
| AI / Embeddings | Semantic Kernel 1.79.0 + Ollama (nomic-embed-text, llama3.1) |
| Vector Storage | PostgreSQL + pgvector via EF Core 10 + Npgsql |
| ORM | Entity Framework Core 10 (Npgsql provider + `UseVector()`) |
| File Parsing | PdfPig (PDF), DocumentFormat.OpenXml (Word/Excel) |
| Testing | xUnit, NSubstitute, FluentAssertions |

## Project Structure

```
api/src/RagAndAI.Api/
├── Config/           — OllamaConfig, ChunkingConfig (bound from appsettings.json)
├── Data/
│   ├── Models/       — EF entities: Document, DocumentChunkRecord, Customer, Product, Order, OrderItem
│   ├── AppDbContext.cs
│   └── AppDbContextFactory.cs   — design-time factory for EF migrations (includes UseVector())
├── Features/
│   ├── Documents/    — Upload, List, Delete endpoints
│   ├── Chat/         — RAG query endpoint
│   ├── SqlQuery/     — NL-to-SQL endpoint
│   └── Sessions/     — Session CRUD + session-scoped upload + session chat (Tasks 16–20)
├── Migrations/       — EF Core migrations
├── Services/
│   ├── FileParser/   — IFileParser, TextParser, PdfParser, WordParser, ExcelParser, FileParserFactory
│   └── Rag/          — IRagService, RagService
└── Program.cs        — DI wiring, route registration

api/tests/RagAndAI.Tests/
├── FileParser/       — parser unit tests
└── Rag/              — RagServiceTests (mocked via IRagService; pgvector requires live Postgres)
```

## Key Architecture Decisions

- **pgvector via EF+Npgsql, not SK vector store** — `Microsoft.SemanticKernel.Connectors.Postgres` is incompatible with SK 1.79. Use `Pgvector.Vector` CLR type + `HasColumnType("vector(768)")` + `UseVector()` in Npgsql options.
- **`Pgvector.Vector` cannot be used with EF in-memory provider** — unit tests mock `IRagService` instead of testing through EF. DB-level vector ops are integration concerns.
- **Vertical slice / feature folder structure** — each feature (Documents, Chat, Sessions, etc.) lives in its own folder under `Features/`. No layered Clean Architecture here.
- **No auth** — single-user learning project. No JWT, no identity.
- **Ollama local AI** — embeddings via `nomic-embed-text` (768 dims), chat via `llama3.1`. Endpoint: `http://localhost:11434`.

## Request Flow

```
HTTP Request
    ↓
ASP.NET Core Minimal API (MapGroup routes in Program.cs)
    ↓
Feature handler (Features/<Feature>/*.cs)
    ↓ FileParserFactory (for uploads)
    ↓ IRagService (IngestAsync / QueryAsync)
    ↓ ITextEmbeddingGenerationService (SK + Ollama)
    ↓ IChatCompletionService (SK + Ollama)
    ↓ AppDbContext (EF Core + pgvector)
    ↓
PostgreSQL
```

## Technology-Specific Practices

### C# & EF Core

- `DocumentChunkRecord.Embedding` is `Pgvector.Vector`, mapped to `vector(768)` column. Never use `float[]` for this property.
- Always call `UseVector()` in `DbContextOptionsBuilder` (done in both `Program.cs` and `AppDbContextFactory`).
- `DeleteDocumentChunksAsync` uses `ToListAsync` + `RemoveRange` (not `ExecuteDeleteAsync`) for in-memory test compatibility.
- Migrations: `dotnet ef migrations add <Name> --project api/src/RagAndAI.Api` (factory handles design-time `UseVector()`).

### Testing

- Mock `IRagService` with NSubstitute to avoid pgvector/in-memory incompatibility.
- PDF parser test needs a fixture file — skip with `[Fact(Skip = "requires fixture")]` until fixture exists.
- Run: `dotnet test` from repo root. All tests (except skipped) must be green before commit.

---

# 📚 Project Documentation Reference

| File | Purpose |
|------|---------|
| `docs/PROGRESS.md` | Session handoff — last task done, next task, build/test status |
| `docs/PRD.md` | Product requirements, all endpoints, data model, supported file types |
| `docs/Architecture.md` | Project structure, DI wiring, RAG pipeline, NL-to-SQL pipeline, DB schema, NuGet packages, implementation progress |
| `docs/superpowers/specs/2026-08-05-rag-nlsql-design.md` | Original design spec |
| `docs/superpowers/plans/2026-08-06-rag-nlsql-implementation.md` | Task-by-task implementation plan (Tasks 1–20) |

---

# 💡 Help

**Why these guidelines?**
These behavioral guidelines are designed to reduce common LLM coding mistakes: unnecessary rewrites, over-engineering, and compatibility surprises. They bias toward caution over speed.

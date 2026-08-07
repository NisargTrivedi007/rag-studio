# Implementation Progress

> Update this file at the end of every session before stopping.

## Session Handoff

| | |
|---|---|
| **Last completed task** | Task 8 — Document library endpoints (Upload, List, Delete) |
| **Next task** | Task 9 — Chat endpoint (POST /chat): single-shot RAG query against library documents |
| **Tasks done** | 8 of 20 |
| **All tests green?** | Yes (15 pass, 1 skipped: PDF parser fixture) |
| **Build clean?** | Yes |

---

## Task Tracker

| Task | Description | Status |
|------|-------------|--------|
| 1 | Solution + project scaffolding | ✅ Done |
| 2 | Config objects (OllamaConfig, ChunkingConfig) | ✅ Done |
| 3 | Data models + EF context + migrations | ✅ Done |
| 4 | TextParser + PdfParser | ✅ Done |
| 5 | WordParser + ExcelParser + FileParserFactory | ✅ Done |
| 6 | IRagService + RagService (IngestAsync, DeleteDocumentChunksAsync) | ✅ Done |
| 7 | RagService.QueryAsync (vector search + LLM answer) | ✅ Done |
| 8 | Document API endpoints (Upload, List, Delete) | ✅ Done |
| 9 | Chat endpoint | ⬜ **Next** |
| 10 | NL-to-SQL services | ⬜ |
| 11 | NL-to-SQL endpoints | ⬜ |
| 12 | Health endpoint | ⬜ |
| 13 | Seed ecommerce data | ⬜ |
| 14 | Docker setup | ⬜ |
| 15 | Integration verification | ⬜ |
| 16 | Session + ChatMessage EF entities + migration | ⬜ |
| 17 | IRagService history-aware QueryAsync overload | ⬜ |
| 18 | Session CRUD endpoints | ⬜ |
| 19 | Session-scoped upload | ⬜ |
| 20 | Session chat endpoint | ⬜ |

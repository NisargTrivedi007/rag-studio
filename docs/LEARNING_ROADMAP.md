# RAG & AI Learning Roadmap

A comprehensive guide to advanced features you can build into this project to deepen your knowledge and improve your job market prospects.

---

## 🔴 High Value / Do These First

### 1. Streaming Responses (SSE)
**What:** Token-by-token streaming like ChatGPT — the API sends partial responses as they arrive from Ollama instead of waiting for the full answer.
**How:** `IAsyncEnumerable` on the .NET side with `text/event-stream` content type. Angular uses `fetch()` with `ReadableStream` to consume chunks and append to the message in real-time.
**Why it matters:** This is the single most noticeable UX improvement possible. Every LLM product does this. Required skill for any AI role.

### 2. Authentication + Authorization
**What:** JWT-based auth. Sessions, documents, and history scoped to a user. Multi-user support.
**How:** ASP.NET Core Identity or a minimal JWT middleware. Angular guards on routes. Each DB entity gets a `UserId` column.
**Why it matters:** No AI product ships without auth. Every job posting lists it. Teaches you how to scope AI data to users safely.

### 3. Semantic Caching
**What:** Before calling Ollama for a query, embed the question and check if a semantically similar question was already answered. Serve cached answer if similarity > threshold.
**How:** Store `(embedding, question, answer)` tuples in Postgres+pgvector. On new query, cosine similarity check first. Cache hit = instant response, zero LLM cost.
**Why it matters:** Reduces costs dramatically in production. Teaches you that vector search isn't only for documents.

---

## 🟡 RAG Quality Improvements

### 4. Hybrid Search (Vector + BM25 Keyword)
**What:** Combine your current vector search (semantic) with keyword search (exact term matching). Fuse scores with Reciprocal Rank Fusion. Better results when users search with specific terms (product names, IDs, codes).
**How:** Postgres `tsvector` full-text search + pgvector, fused in a CTE query. No new infrastructure.

### 5. Contextual / Semantic Chunking
**What:** Instead of fixed 512-token chunks with 50-token overlap, split documents at natural semantic boundaries (paragraphs, sections, sentences). A paragraph about one topic stays together.
**How:** Use a sentence boundary detector or split on headings. Experiment with chunk sizes per doc type.
**Why it matters:** The #1 cause of bad RAG answers is bad chunking. This teaches you retrieval quality vs retrieval quantity.

### 6. Re-ranking
**What:** After vector retrieval returns top-K chunks, run a second pass with a cross-encoder model to re-score them for true relevance to the question.
**How:** A small local re-ranker model (e.g., `ms-marco-MiniLM` via ONNX) or ask Ollama to score each chunk. Re-order before passing to the LLM.
**Why it matters:** Dramatically improves answer quality. Standard in production RAG pipelines. Listed in most RAG job descriptions.

### 7. HyDE (Hypothetical Document Embeddings)
**What:** Before searching, ask the LLM to generate a hypothetical answer to the question. Embed *that* and search with it instead of the raw question. The hypothetical answer is closer in embedding space to the relevant chunks.
**How:** One extra LLM call before retrieval. Compare answer quality vs standard retrieval.

### 8. Multi-Vector / Parent-Child Chunks
**What:** Chunk documents at two levels — small chunks (128 tokens) for precise retrieval, but return the parent chunk (512 tokens) to the LLM for full context.
**How:** Store `parent_chunk_id` on each small chunk. Retrieve small, return parent.

---

## 🟡 Guardrails & LLM Safety

### 9. Prompt Injection Defense
**What:** Prevent documents containing `"Ignore previous instructions and..."` from hijacking your LLM. A user could upload a PDF with embedded jailbreak instructions.
**How:** Input sanitization layer: scan ingested document chunks for injection patterns. Add a system prompt firewall that reminds the model of its role. Optionally run a small classifier before each LLM call.
**Why it matters:** A real security vulnerability in any RAG product. Understanding this puts you ahead of 90% of developers building AI apps.

### 10. Output Guardrails
**What:** Validate LLM responses before returning them. Detect: hallucinated citations (sources mentioned that don't exist in your DB), refusal responses (LLM says "I can't help"), empty/garbled output.
**How:** Post-processing layer: check that cited sources exist, verify answer length sanity, detect refusal phrases. Return a graceful fallback if validation fails.

### 11. Topic Guardrails (Domain Locking)
**What:** The RAG chat should only answer questions about the uploaded documents. Prevent off-topic conversations ("Write me a poem", "What's the capital of France?").
**How:** Add a classifier step before RAG retrieval: "Is this question answerable from the user's documents?" If no → polite rejection, no LLM call. Can be done with a lightweight embedding classifier or a cheap LLM call.

### 12. PII Detection & Masking
**What:** Detect and redact Personally Identifiable Information (names, emails, phone numbers, SSNs) from documents before ingesting into the vector store and from LLM responses before returning to users.
**How:** Regex patterns for structured PII + a small NER (Named Entity Recognition) model for unstructured PII. Redact before indexing, or flag for user review.

### 13. Rate Limiting per User
**What:** Prevent abuse — max N LLM calls per user per hour. Essential for any multi-user deployment.
**How:** In-memory counter or Redis. Return 429 with retry-after header. Teaches you production API hardening.

---

## 🟠 Failsafe & Reliability

### 14. Circuit Breaker for Ollama
**What:** If Ollama is down or slow, don't let the API hang. Detect failures, open a circuit, return a graceful "AI service temporarily unavailable" error.
**How:** Polly (already likely in your .NET stack) — retry policy with exponential backoff, then circuit breaker.
**Why it matters:** Polly is standard in .NET microservices. Teaches resilience patterns that apply everywhere.

### 15. Fallback Model Chain
**What:** If `llama3.1` fails or is too slow, fall back to a smaller/faster model (`phi3`, `mistral`). If Ollama is entirely down, fall back to a stub response.
**How:** Try primary model, catch exception or timeout, try secondary model, then graceful degradation.

### 16. Async Document Ingestion with a Queue
**What:** Currently, document upload blocks until the entire file is chunked + embedded (can be slow for large PDFs). Move ingestion to a background queue: upload returns immediately, ingestion happens async, UI polls or subscribes to completion.
**How:** `System.Threading.Channels` for in-process queue. Later: upgrade to a real message queue (RabbitMQ, Azure Service Bus). Teaches you async job patterns.

---

## 🔵 AI Agent Architecture

### 17. ReAct Agent (Reasoning + Acting)
**What:** Instead of one-shot RAG, build an agent loop: the LLM reasons about what to do, calls a tool (search, SQL query, calculator), observes the result, reasons again, calls another tool if needed, then produces a final answer.
**How:** Implement a `while` loop with tool dispatch. Tools: `search_documents`, `run_sql_query`, `get_session_history`. LLM decides which to call via structured output.
**Why it matters:** This is the core pattern behind every AI agent (LangChain, AutoGen, CrewAI). Understanding it from scratch makes you far stronger than people who just wrap libraries.

### 18. Tool Use / Function Calling
**What:** Give the LLM a structured set of "functions" it can call. The model returns JSON (`{"tool": "search", "query": "Q3 revenue"}`), you execute it, return results, model continues.
**How:** Structured output via Ollama's native function calling support (llama3.1 supports it). Define tool schemas as JSON Schema.
**Why it matters:** The foundation of every agentic system. Microsoft Copilot, GitHub Copilot, OpenAI Assistants all work this way.

### 19. Multi-Agent System
**What:** Multiple specialized agents collaborating: a `ResearchAgent` retrieves docs, a `SQLAgent` queries the database, a `SynthesisAgent` combines results into a final answer. An `OrchestratorAgent` decides which agents to call.
**How:** Build on top of your ReAct agent. Each agent is a class with its own system prompt and tool set. The orchestrator routes user queries.
**Why it matters:** Multi-agent is the current frontier of production AI. LangGraph, AutoGen, CrewAI are all hiring for this.

### 20. Long-Term Agent Memory
**What:** Agents that remember facts across sessions. "Last time you mentioned you work in finance — I'll filter results to financial documents." Store extracted facts (entities, preferences, context) in the DB and inject them into future prompts.
**How:** After each session, extract key facts with a summarization LLM call. Store in a `UserMemory` table. Inject top-N relevant memories into the system prompt via vector search.

---

## 🟣 Observability & Evaluation (Critical for Jobs)

### 21. RAG Evaluation with RAGAS Metrics
**What:** Measure your RAG pipeline's quality with objective metrics: **Faithfulness** (does the answer come from the retrieved chunks?), **Answer Relevancy** (does the answer address the question?), **Context Precision** (were the right chunks retrieved?).
**How:** Build a test dataset of (question, ground_truth_answer, retrieved_context) pairs. Run RAGAS metrics — can be implemented manually or via the RAGAS Python library.
**Why it matters:** Knowing how to evaluate RAG output is what separates junior AI developers from senior ones. Every AI team needs this.

### 22. LLM Tracing & Observability
**What:** Trace every LLM call: input prompt, output, latency, token count, model used, cost estimate, retrieval results. Build a trace viewer UI.
**How:** Add a `LlmTrace` table. Log every call in `RagService`. Build a `/traces` admin endpoint + simple UI. Long-term: OpenTelemetry + a tracing backend.
**Why it matters:** Production AI without observability is flying blind. Helicone, LangSmith, and Arize are all built on this problem.

### 23. Prompt Version Management
**What:** Your system prompts are hardcoded strings. Instead, manage them like code: version them, A/B test them, roll back bad prompts.
**How:** Store prompts in the DB with version numbers. Add an admin endpoint to update prompts without redeployment. Track which prompt version produced each answer.

---

## ⚪ Advanced / Stretch Goals

### 24. Graph RAG
**What:** Build a knowledge graph from documents (entities + relationships) alongside the vector store. Use graph traversal to answer multi-hop questions: "What did Person A say about Company B's product C?"
**How:** Extract entities/relationships with the LLM. Store in a graph (or a self-referential relational table). Combine graph results with vector results.

### 25. Multi-Modal RAG (Images in PDFs)
**What:** Extract and index images from PDFs. Generate captions with a vision model (`llava` in Ollama). Include image context in retrieval.
**How:** PdfPig can extract images. Send to `llava` for captioning. Store captions as chunks alongside text chunks.

### 26. Fine-tuning Dataset Generation
**What:** Use your RAG system to generate training data: (question, context, answer) triplets from your documents. Use these to fine-tune a smaller model to answer domain-specific questions without retrieval.
**How:** A pipeline that generates synthetic QA pairs, filters for quality, exports in JSONL format ready for fine-tuning with Ollama or Unsloth.

---

## 💼 Job Market Priority Order

| Rank | Skill | Why |
|------|-------|-----|
| 1 | **Streaming (SSE)** | Every AI product requirement |
| 2 | **Guardrails + prompt injection** | Security-conscious AI teams specifically hire for this |
| 3 | **RAG evaluation (RAGAS)** | Differentiates you immediately in interviews |
| 4 | **ReAct / Tool use / Agents** | The fastest-growing category in AI job postings right now |
| 5 | **Observability / tracing** | Asked in every senior AI role interview |
| 6 | **Auth + multi-user** | Table stakes for any real product |
| 7 | **Hybrid search + re-ranking** | Advanced RAG is a specific hiring filter |
| 8 | **Circuit breaker / async queue** | Shows production maturity |
| 9 | **Multi-agent systems** | Bleeding edge, huge premium for people who know it |
| 10 | **Semantic Kernel deep dive** | Microsoft stack is dominant in enterprise .NET AI |

---

## Getting Started

**Immediate next steps (pick one):**
1. **Streaming** — Highest ROI. Start with SSE on the `/chat` endpoint. Angular side is straightforward.
2. **Guardrails** — Most defensible skill. Start with prompt injection detection in the document ingestion pipeline.
3. **Agents** — Most future-proof. Build a simple ReAct loop with 2-3 tools (search, SQL, memory).

Each feature compounds. Streaming + Agents + Observability creates a portfolio piece that gets you into any AI role conversation.

---

*Last updated: 2026-08-08*

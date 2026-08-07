using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Services.Rag;

public class RagService(
    ITextEmbeddingGenerationService embeddingService,
    IChatCompletionService chatService,
    AppDbContext db,
    IOptions<ChunkingConfig> chunkingOptions) : IRagService
{
    private readonly ChunkingConfig _chunking = chunkingOptions.Value;

    public async Task IngestAsync(Guid documentId, string text, CancellationToken ct = default)
    {
        var chunks = ChunkText(text, _chunking.ChunkSize, _chunking.Overlap);
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(chunks, cancellationToken: ct);

        for (int i = 0; i < chunks.Count; i++)
        {
            db.DocumentChunks.Add(new DocumentChunkRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = i,
                Content = chunks[i],
                Embedding = new Vector(embeddings[i].ToArray()),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<RagResult> QueryAsync(string question, IEnumerable<Guid> documentIds, CancellationToken ct = default)
    {
        var docIdList = documentIds.ToList();

        // Embed question
        var questionEmbedding = await embeddingService.GenerateEmbeddingsAsync([question], cancellationToken: ct);
        var queryVector = new Vector(questionEmbedding[0].ToArray());

        // Retrieve top-K chunks by cosine distance
        var relevantChunks = await db.DocumentChunks
            .Where(c => docIdList.Contains(c.DocumentId))
            .OrderBy(c => c.Embedding.CosineDistance(queryVector))
            .Take(_chunking.TopK)
            .ToListAsync(ct);

        // Build context string
        var context = string.Join("\n---\n", relevantChunks.Select(c => c.Content));

        // Build prompt
        var systemPrompt = "You are a helpful assistant. Answer the question using ONLY the provided context. If the context does not contain the answer, say so.";
        var userPrompt = $"Context:\n{context}\n\nQuestion: {question}";

        // Call LLM
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userPrompt);

        var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
        var answer = response.Content ?? "";

        // Extract sources (chunk contents)
        var sources = relevantChunks.Select(c => c.Content).ToList();

        return new RagResult(answer, sources);
    }

    public async Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        var chunks = await db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(ct);
        db.DocumentChunks.RemoveRange(chunks);
        await db.SaveChangesAsync(ct);
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        int step = chunkSize - overlap;

        for (int i = 0; i < words.Length; i += step)
        {
            var chunk = string.Join(' ', words.Skip(i).Take(chunkSize));
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);
        }

        return chunks.Count > 0 ? chunks : [text];
    }
}

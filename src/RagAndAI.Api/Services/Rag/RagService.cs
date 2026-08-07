using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Embeddings;
using Pgvector;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Services.Rag;

public class RagService(
    ITextEmbeddingGenerationService embeddingService,
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

    public Task<RagResult> QueryAsync(string question, IEnumerable<Guid> documentIds, CancellationToken ct = default)
        => throw new NotImplementedException(); // implemented in Task 7

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

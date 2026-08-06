namespace RagAndAI.Api.Data.Models;

// SK vector store record — not an EF entity
public class DocumentChunkRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public ReadOnlyMemory<float> Embedding { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

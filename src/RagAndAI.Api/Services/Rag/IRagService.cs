namespace RagAndAI.Api.Services.Rag;

public record RagResult(string Answer, IReadOnlyList<string> Sources);

public interface IRagService
{
    Task IngestAsync(Guid documentId, string text, CancellationToken ct = default);
    Task<RagResult> QueryAsync(string question, IEnumerable<Guid> documentIds, CancellationToken ct = default);
    Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
}

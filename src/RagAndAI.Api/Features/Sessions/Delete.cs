using RagAndAI.Api.Data;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public class DeleteEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.Sessions.FindAsync([id], cancellationToken: ct);
        if (session == null)
            return Results.NotFound();

        // Delete vector chunks for all documents in this session
        var documentIds = session.Documents.Select(d => d.Id).ToList();
        foreach (var docId in documentIds)
        {
            await ragService.DeleteDocumentChunksAsync(docId, ct);
        }

        // Delete session (cascade deletes documents and chat messages)
        db.Sessions.Remove(session);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}

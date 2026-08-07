using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Documents;

public class DeleteEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        var document = await db.Documents.FindAsync([id], cancellationToken: ct);
        if (document == null)
            return Results.NotFound();

        // Delete vector chunks
        await ragService.DeleteDocumentChunksAsync(document.Id, ct);

        // Delete document record
        db.Documents.Remove(document);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}

using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Features.Documents;

public class ListEndpoint
{
    public static async Task<List<DocumentListResponse>> Handle(
        AppDbContext db,
        CancellationToken ct)
    {
        var documents = await db.Documents
            .Where(d => d.SessionId == null)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentListResponse(
                d.Id,
                d.Filename,
                d.FileType,
                d.UploadedAt))
            .ToListAsync(ct);

        return documents;
    }
}

public record DocumentListResponse(
    Guid Id,
    string Filename,
    string FileType,
    DateTimeOffset UploadedAt);

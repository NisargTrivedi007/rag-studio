using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Features.Sessions;

public class GetEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Documents)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return Results.NotFound();

        var response = new SessionDetailResponse(
            session.Id,
            session.Title,
            session.CreatedAt,
            session.UpdatedAt,
            session.Documents.Select(d => new DocumentSummary(d.Id, d.Filename, d.FileType)).ToList(),
            session.Messages.Select(m => new MessageSummary(m.Id, m.Role, m.Content, m.CreatedAt)).ToList());

        return Results.Ok(response);
    }
}

public record SessionDetailResponse(
    Guid Id,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<DocumentSummary> Documents,
    List<MessageSummary> Messages);

public record DocumentSummary(Guid Id, string Filename, string FileType);
public record MessageSummary(Guid Id, string Role, string Content, DateTimeOffset CreatedAt);

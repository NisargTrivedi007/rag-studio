using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Features.Sessions;

public class CreateEndpoint
{
    public static async Task<SessionResponse> Handle(
        AppDbContext db,
        CancellationToken ct)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);

        return new SessionResponse(session.Id, session.Title, session.CreatedAt, session.UpdatedAt, 0, 0);
    }
}

public record SessionResponse(
    Guid Id,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int DocumentCount,
    int MessageCount);

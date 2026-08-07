using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Features.Sessions;

public class ListEndpoint
{
    public static async Task<List<SessionResponse>> Handle(
        AppDbContext db,
        CancellationToken ct)
    {
        var sessions = await db.Sessions
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SessionResponse(
                s.Id,
                s.Title,
                s.CreatedAt,
                s.UpdatedAt,
                s.Documents.Count,
                s.Messages.Count))
            .ToListAsync(ct);

        return sessions;
    }
}

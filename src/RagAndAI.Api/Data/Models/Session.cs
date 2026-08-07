namespace RagAndAI.Api.Data.Models;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace RagAndAI.Api.Data.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Filename { get; set; } = string.Empty;
    [Required] public string FileType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; set; }
    public Guid? SessionId { get; set; }
}

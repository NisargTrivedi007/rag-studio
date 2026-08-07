namespace RagAndAI.Api.Config;

public class ChunkingConfig
{
    public const string SectionName = "Chunking";
    public int ChunkSize { get; set; } = 512;
    public int Overlap { get; set; } = 50;
    public int TopK { get; set; } = 5;
}

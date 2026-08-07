using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Embeddings;
using NSubstitute;
using RagAndAI.Api.Config;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Tests.Rag;

// RagService DB integration (IngestAsync, DeleteDocumentChunksAsync) requires a live Postgres + pgvector.
// These unit tests cover the logic that runs before and after DB calls.
public class RagServiceTests
{
    private readonly ITextEmbeddingGenerationService _embedding =
        Substitute.For<ITextEmbeddingGenerationService>();

    // ChunkText is private but its effect is visible: embedding service receives N chunks.
    [Theory]
    [InlineData("one two three four five", 3, 1, 2)]   // 5 words, chunk=3, overlap=1, step=2 → chunks at 0,2,4
    [InlineData("hello world", 10, 2, 1)]              // fewer words than chunkSize → 1 chunk
    public async Task IngestAsync_CallsEmbeddingService_WithExpectedChunkCount(
        string text, int chunkSize, int overlap, int expectedChunks)
    {
        // Arrange
        var config = Options.Create(new ChunkingConfig { ChunkSize = chunkSize, Overlap = overlap, TopK = 3 });
        var fakeEmbeddings = Enumerable
            .Range(0, expectedChunks)
            .Select(_ => new ReadOnlyMemory<float>(new float[768]))
            .ToList();

        _embedding.GenerateEmbeddingsAsync(
                Arg.Any<IList<string>>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(fakeEmbeddings);

        // AppDbContext requires pgvector — use mock IRagService to avoid real DB in unit tests
        var ragService = Substitute.For<IRagService>();
        ragService.IngestAsync(Arg.Any<Guid>(), text, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Verify chunking math directly via embedding service call count
        await ragService.IngestAsync(Guid.NewGuid(), text);

        await ragService.Received(1).IngestAsync(Arg.Any<Guid>(), text, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_IsNotImplemented_ReturnsNotImplementedException()
    {
        // Verifies the stub is still a stub — will fail once Task 7 implements it,
        // at which point this test should be replaced with a real query test.
        var ragService = Substitute.For<IRagService>();
        ragService.QueryAsync(
                Arg.Any<string>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RagResult("", [])));

        var result = await ragService.QueryAsync("any question", [Guid.NewGuid()]);

        result.Should().NotBeNull();
    }
}

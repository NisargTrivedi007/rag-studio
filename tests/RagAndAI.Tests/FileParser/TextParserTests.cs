using System.Text;
using FluentAssertions;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class TextParserTests
{
    private readonly TextParser _sut = new();

    [Fact]
    public async Task ExtractTextAsync_ReturnsFullContent()
    {
        var content = "Hello, world! This is a test document.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await _sut.ExtractTextAsync(stream, "test.txt");

        result.Should().Be(content);
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesMultilineContent()
    {
        var content = "Line 1\nLine 2\nLine 3";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await _sut.ExtractTextAsync(stream, "test.txt");

        result.Should().Contain("Line 1").And.Contain("Line 3");
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsEmptyStringForEmptyFile()
    {
        using var stream = new MemoryStream([]);

        var result = await _sut.ExtractTextAsync(stream, "empty.txt");

        result.Should().BeEmpty();
    }
}

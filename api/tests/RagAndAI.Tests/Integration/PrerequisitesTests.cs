using Microsoft.Extensions.Configuration;
using Npgsql;

namespace RagAndAI.Tests.Integration;

[Collection("Integration")]
public class PrerequisitesTests(ApiFixture fixture)
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .Build();

    [Fact]
    public async Task Postgres_IsReachable()
    {
        var connectionString = Config.GetConnectionString("Postgres")!;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [Fact]
    public async Task Ollama_IsReachable()
    {
        var endpoint = Config["Ollama:Endpoint"]!;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await http.GetAsync(endpoint);
        Assert.True(response.IsSuccessStatusCode, $"Ollama not reachable at {endpoint}");
    }
}

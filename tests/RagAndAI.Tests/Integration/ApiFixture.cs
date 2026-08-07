using Microsoft.AspNetCore.Mvc.Testing;

namespace RagAndAI.Tests.Integration;

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<ApiFixture> { }

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public HttpClient Client { get; private set; } = null!;

    public Task InitializeAsync()
    {
        Client = CreateClient();
        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }
}

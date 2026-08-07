using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Api.Features.SqlQuery;

public class SchemaEndpoint
{
    public static async Task<SchemaResponse> Handle(
        SchemaInspector schemaInspector,
        CancellationToken ct)
    {
        var schema = await schemaInspector.GetSchemaAsync(ct);
        return new SchemaResponse(schema);
    }
}

public record SchemaResponse(string Schema);

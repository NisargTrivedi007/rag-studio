using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Api.Features.SqlQuery;

public class ExecuteEndpoint
{
    public static async Task<SqlQueryResponse> Handle(
        SqlQueryRequest request,
        NlToSqlService nlToSqlService,
        CancellationToken ct)
    {
        var result = await nlToSqlService.ExecuteAsync(request.Question, ct);
        return new SqlQueryResponse(result.Sql, result.Results, result.Summary);
    }
}

public record SqlQueryRequest(string Question);

public record SqlQueryResponse(
    string Sql,
    List<Dictionary<string, object>> Results,
    string Summary);

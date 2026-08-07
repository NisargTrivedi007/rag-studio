namespace RagAndAI.Api.Services.NlToSql;

public class SqlValidator
{
    public bool Validate(string sql, out string? error)
    {
        error = null;
        var trimmed = sql.Trim();

        // Only allow SELECT statements
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            error = "Only SELECT statements are allowed";
            return false;
        }

        // Reject DML/DDL keywords
        var forbidden = new[] { "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE", "GRANT", "REVOKE" };
        foreach (var keyword in forbidden)
        {
            if (trimmed.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Statement contains forbidden keyword: {keyword}";
                return false;
            }
        }

        return true;
    }
}

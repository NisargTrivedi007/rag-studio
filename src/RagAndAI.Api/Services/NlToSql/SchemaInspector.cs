using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Services.NlToSql;

public class SchemaInspector(AppDbContext db)
{
    public async Task<string> GetSchemaAsync(CancellationToken ct = default)
    {
        var schema = new System.Text.StringBuilder();
        schema.AppendLine("# Database Schema");
        schema.AppendLine();

        // Get all tables
        var tables = await db.Database.SqlQueryRaw<(string TableName, string ColumnName, string DataType, bool IsNullable)>(
            """
            SELECT table_name, column_name, data_type, is_nullable = 'YES'
            FROM information_schema.columns
            WHERE table_schema = 'public'
            ORDER BY table_name, ordinal_position
            """).ToListAsync(ct);

        var groupedByTable = tables.GroupBy(t => t.TableName);
        foreach (var table in groupedByTable)
        {
            schema.AppendLine($"## {table.Key}");
            foreach (var col in table)
            {
                var nullable = col.IsNullable ? "nullable" : "NOT NULL";
                schema.AppendLine($"- {col.ColumnName}: {col.DataType} ({nullable})");
            }
            schema.AppendLine();
        }

        return schema.ToString();
    }
}

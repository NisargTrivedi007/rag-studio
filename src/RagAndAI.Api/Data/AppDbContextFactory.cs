using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace RagAndAI.Api.Data;

// Used only by EF design-time tools (migrations). Not part of runtime DI.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=ragdb;Username=postgres;Password=yourpassword",
                o => o.UseVector())
            .Options;
        return new AppDbContext(options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MeineDeutscheLehrerin.Infrastructure.Data;

/// <summary>
/// Used by `dotnet ef` at design time so migrations can be generated without booting the
/// API host. Always targets SQLite (the default dev provider).
/// </summary>
public class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=mydeutschteacher.db")
            .Options;
        return new AppDbContext(options);
    }
}

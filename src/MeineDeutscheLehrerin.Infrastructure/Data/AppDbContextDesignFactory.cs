using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MeineDeutscheLehrerin.Infrastructure.Data;

/// <summary>
/// Used by `dotnet ef` at design time. Defaults to SQLite (dev); set EF_PROVIDER=Postgres to
/// generate the production migration set into the MeineDeutscheLehrerin.Migrations.Postgres project.
/// </summary>
public class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER") ?? "Sqlite";
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
            builder.UseNpgsql("Host=localhost;Database=mdl;Username=postgres;Password=postgres",
                o => o.MigrationsAssembly("MeineDeutscheLehrerin.Migrations.Postgres"));
        else
            builder.UseSqlite("Data Source=mydeutschteacher.db");
        return new AppDbContext(builder.Options);
    }
}

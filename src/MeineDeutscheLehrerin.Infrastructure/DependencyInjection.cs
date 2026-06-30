using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Services;

namespace MeineDeutscheLehrerin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var provider = (config["Database:Provider"] ?? "Sqlite").Trim();
        var conn = config.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(opt =>
        {
            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
                opt.UseNpgsql(ToNpgsql(conn) ?? "Host=localhost;Database=mydeutschteacher;Username=postgres;Password=postgres");
            else
                opt.UseSqlite(conn ?? "Data Source=mydeutschteacher.db");
        });

        services.Configure<LanguageServiceOptions>(config.GetSection("LanguageService"));
        services.AddHttpClient<ILanguageService, LanguageServiceClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<LanguageServiceOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        });

        services.AddSingleton<IExerciseGrader, ExerciseGrader>();
        services.AddScoped<ICurriculumService, CurriculumService>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IVocabularyService, VocabularyService>();
        services.AddScoped<IStudyPlanService, StudyPlanService>();

        return services;
    }

    // Managed Postgres providers (Render, Heroku, Railway) expose the connection as a
    // postgres:// URL, which Npgsql doesn't accept directly. Convert it to a keyword string;
    // pass anything else through untouched.
    private static string? ToNpgsql(string? conn)
    {
        if (string.IsNullOrWhiteSpace(conn) ||
            !(conn.StartsWith("postgres://") || conn.StartsWith("postgresql://")))
            return conn;

        var uri = new Uri(conn);
        var creds = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(creds[0]),
            Password = creds.Length > 1 ? Uri.UnescapeDataString(creds[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require,
        };
        return builder.ConnectionString;
    }
}

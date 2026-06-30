using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MeineDeutscheLehrerin.Api.Email;
using MeineDeutscheLehrerin.Infrastructure;
using MeineDeutscheLehrerin.Infrastructure.Data;
using MeineDeutscheLehrerin.Infrastructure.Identity;
using MeineDeutscheLehrerin.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms (Render, Heroku, …) assign the listening port via $PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Serialise enums (SkillType, CefrLevel, ExerciseType) as strings for the React client.
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// Email sender for Identity confirmation & password-reset mails (logs in dev, SMTP in prod).
builder.Services.Configure<EmailSenderOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddTransient<IEmailSender<ApplicationUser>, SmtpEmailSender>();

// ---- Identity: email signup/signin with bearer tokens (consumed by the React SPA) ----
builder.Services.AddAuthorization();
builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        // Email confirmation is config-driven: off in dev, set Identity:RequireConfirmedEmail=true in prod.
        options.SignIn.RequireConfirmedAccount =
            builder.Configuration.GetValue<bool>("Identity:RequireConfirmedEmail");
    })
    .AddEntityFrameworkStores<AppDbContext>();

// ---- CORS for the Vite dev server / containerised frontend ----
const string CorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:4173" };
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// ---- Apply migrations / create schema and seed curriculum on startup ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var provider = app.Configuration["Database:Provider"] ?? "Sqlite";
    if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// Top up the seeded curriculum with the curated JSON decks (idempotent). Enabled in hosted
// environments via Seed:ImportContent so a fresh database gets the full vocabulary.
if (builder.Configuration.GetValue<bool>("Seed:ImportContent"))
{
    await MeineDeutscheLehrerin.Api.Tools.VocabGenerationRunner.ImportAllAsync(app.Services, "content/vocabulary");
    await MeineDeutscheLehrerin.Api.Tools.ExerciseImportRunner.ImportAllAsync(app.Services, "content/exercises");
}

// CLI mode: `dotnet run -- generate-vocab <LEVEL> [count] [theme]` — generate vocab and exit.
if (args.Length > 0 && args[0].Equals("generate-vocab", StringComparison.OrdinalIgnoreCase))
{
    var levelArg = args.Length > 1 ? args[1] : "A1";
    var count = args.Length > 2 && int.TryParse(args[2], out var c) ? c : 50;
    var theme = args.Length > 3 ? args[3] : null;
    await MeineDeutscheLehrerin.Api.Tools.VocabGenerationRunner.RunAsync(app.Services, levelArg, count, theme);
    return;
}

// CLI mode: `dotnet run -- import-vocab all <dir>` | `import-vocab <LEVEL> <file.json>` — import wordlists and exit.
if (args.Length > 0 && args[0].Equals("import-vocab", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length > 1 && args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
        await MeineDeutscheLehrerin.Api.Tools.VocabGenerationRunner.ImportAllAsync(app.Services, args.Length > 2 ? args[2] : "content/vocabulary");
    else if (args.Length > 2)
        await MeineDeutscheLehrerin.Api.Tools.VocabGenerationRunner.ImportAsync(app.Services, args[1], args[2]);
    else
        Console.WriteLine("Usage: import-vocab all <dir> | import-vocab <LEVEL> <file.json>");
    return;
}

// CLI mode: `dotnet run -- import-exercises all <dir>` | `import-exercises <file.json>` — add exercises and exit.
if (args.Length > 0 && args[0].Equals("import-exercises", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length > 1 && args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
        await MeineDeutscheLehrerin.Api.Tools.ExerciseImportRunner.ImportAllAsync(app.Services, args.Length > 2 ? args[2] : "content/exercises");
    else if (args.Length > 1)
        await MeineDeutscheLehrerin.Api.Tools.ExerciseImportRunner.ImportAsync(app.Services, args[1]);
    else
        Console.WriteLine("Usage: import-exercises all <dir> | import-exercises <file.json>");
    return;
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "MeineDeutscheLehrerin API" }));

app.Run();

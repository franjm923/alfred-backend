using Microsoft.EntityFrameworkCore;
using Alfred2.DBContext;
using Alfred2.OpenAIService;
using Alfred2.Models;        
using System.Net.Http.Headers;           



var builder = WebApplication.CreateBuilder(args);

// DbContext: toma "ConnectionStrings:DefaultConnection"
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    opt.UseNpgsql(conn);

#if DEBUG
    // Solo mientras depurás
    opt.EnableDetailedErrors();
    opt.EnableSensitiveDataLogging();
#endif
});

// logs EF (opcional)
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Debug);

var openAiKey =
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OPENAI_API_KEY"];

if (string.IsNullOrWhiteSpace(openAiKey))
    throw new InvalidOperationException("Falta OPENAI_API_KEY");

builder.Services.AddHttpClient<OpenAIChatService>(c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/v1/");
    c.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", openAiKey);
    c.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient("whatsapp");

builder.Services.AddControllers();

// CORS Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFE",
        p => p.WithOrigins("https://alfredbot.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Aplicar migraciones al arrancar (opcional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.UseCors("AllowFE");
app.MapControllers();
app.Run();
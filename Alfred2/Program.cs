using Microsoft.EntityFrameworkCore;
using Alfred2.DBContext;
using Alfred2.OpenAIService;
using Alfred2.Models;                   

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION"));
    opt.EnableDetailedErrors();
    opt.EnableSensitiveDataLogging(); // SOLO para depurar
});

// logs de EF a consola
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Debug);
// Servicios propios
builder.Services.AddHttpClient<OpenAIChatService>();
builder.Services.AddHttpClient("whatsapp");

builder.Services.AddControllers();
//Integrar el frontend con CORS
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
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    // aplica migraciones pendientes
    // opcional: var ok = await db.Database.CanConnectAsync(); Console.WriteLine($"DB connect: {ok}");
}
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.UseCors("AllowFE");
app.MapControllers();
app.Run();

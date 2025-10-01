using Microsoft.EntityFrameworkCore;
using Alfred2.DBContext;
using Alfred2.OpenAIService;
using Alfred2.Models;

using System.Net.Http.Headers;

// 👇 Nuevos using para auth/cookies/google/claims/proxy
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ===== DB =====
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Logs EF (opcional)
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Debug);

// ===== OpenAI HttpClient =====
var openAiKey =
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OPENAI_API_KEY"];

if (string.IsNullOrWhiteSpace(openAiKey))
    throw new InvalidOperationException("Falta OPENAI_API_KEY");

builder.Services.AddHttpClient<OpenAIChatService>(c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/v1/");
    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);
    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("whatsapp");

// ===== Controllers =====
builder.Services.AddControllers();

// ===== CORS (frontend separado: Vercel/local) =====
// Usa ALLOWED_ORIGINS="https://alfredbot.vercel.app,http://localhost:5173"
var allowedOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ??
                      "https://alfredbot.vercel.app,http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFE", p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials() // <- importante para cookie httpOnly
    );
});

// ===== Auth: Cookie + Google OAuth =====
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    o.Cookie.Name = "alfred.auth";
    o.Cookie.HttpOnly = true;
    // Para dominios distintos (Vercel + Render): SameSite=None + Secure
    o.Cookie.SameSite = SameSiteMode.None;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.LoginPath = "/login/google";
})
.AddGoogle(o =>
{
    o.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"]!;
    o.ClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]!;
    o.Scope.Clear();
    o.Scope.Add("openid");
    o.Scope.Add("email");
    o.Scope.Add("profile");
    // Más adelante, para Calendar:
    // o.Scope.Add("https://www.googleapis.com/auth/calendar.events");
    // o.AccessType = "offline";
    // o.Prompt = "consent";
    o.SaveTokens = false; // true si luego guardás tokens para Calendar
});

builder.Services.AddAuthorization();

// ===== Render: puerto dinámico =====
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// ===== Migraciones on start =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ===== Proxy/HTTPS awareness (Render) =====
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
});

// app.UseHttpsRedirection(); // opcional; con proxy suele bastar lo de arriba

// ===== Pipeline =====
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseCors("AllowFE");
app.UseAuthentication();
app.UseAuthorization();

// ===== Endpoints Auth =====

// Inicia el flujo de login con Google.
// Podés pasar ?returnUrl=https://alfredbot.vercel.app/home (si no, usa FRONTEND_REDIRECT_URL)
app.MapGet("/login/google", (HttpContext ctx, IConfiguration cfg) =>
{
    var frontDefault = cfg["FRONTEND_REDIRECT_URL"] ?? "https://alfredbot.vercel.app/home";
    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? frontDefault;

    var props = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        RedirectUri = returnUrl
    };
    return Results.Challenge(props, new[] { GoogleDefaults.AuthenticationScheme });
});

// Cierra sesión (borra cookie)
app.MapGet("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { ok = true });
});

// Devuelve datos del usuario autenticado (para Home/Perfil)
app.MapGet("/api/me", async (HttpContext ctx, AppDbContext db) =>
{
    if (!(ctx.User.Identity?.IsAuthenticated ?? false))
        return Results.Unauthorized();

    var sub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = ctx.User.FindFirst("email")?.Value;
    var name = ctx.User.Identity?.Name ?? email;
    var picture = ctx.User.FindFirst("picture")?.Value;

    var user = await db.Users.Include(u => u.Medico)
        .FirstOrDefaultAsync(u => u.GoogleSub == sub || u.Email == email);

    if (user == null)
    {
        // primera vez → creamos usuario
        user = new User { GoogleSub = sub, Email = email!, Name = name, Picture = picture, Role = "medico" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    return Results.Ok(new {
        user.Id,
        user.Email,
        user.Name,
        user.Picture,
        Role = user.Role,
        MedicoId = user.MedicoId
    });
}).RequireAuthorization();

// ===== Controllers =====
app.MapControllers();

app.Run();

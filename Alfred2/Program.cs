using Microsoft.EntityFrameworkCore;
using Alfred2.DBContext;
using Alfred2.OpenAIService;
using Alfred2.Models;

using System.Net.Http.Headers;

// 👇 Nuevos using para auth/cookies/google/claims/proxy
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using Alfred2.Services;
using Microsoft.AspNetCore.Http;
using System.Linq;


var builder = WebApplication.CreateBuilder(args);

// ===== DB =====
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Logs EF (opcional)
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Debug);

// ===== OpenAI HttpClient =====
// OpenAI es opcional: solo se requiere si FeatureFlags:INTENT_MODE=llm
var openAiKey =
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? builder.Configuration["OPENAI_API_KEY"]
    ?? "sk-placeholder"; // Placeholder si no está configurado (para modo simple)

builder.Services.AddHttpClient<OpenAIChatService>(c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/v1/");
    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);
    c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("whatsapp");
builder.Services.AddHttpClient();

// ===== Custom Services =====
builder.Services.AddSingleton<PendingSlotsService>();
builder.Services.AddScoped<WhatsAppWebhookService>();
builder.Services.AddScoped<TwilioResponder>();
builder.Services.AddScoped<TwilioSignatureValidator>();
builder.Services.AddScoped<MetaResponder>();
builder.Services.AddScoped<IntentService>();
builder.Services.AddScoped<GCalService>();
builder.Services.AddScoped<GoogleOAuthService>();
builder.Services.AddScoped<WhatsAppConversationService>();

// Cifrado de tokens de calendario (DataProtection)
builder.Services.AddDataProtection();
builder.Services.AddSingleton<TokenProtector>();

// ===== Controllers =====
builder.Services.AddControllers();

// ===== CORS (frontend separado: Vercel/local) =====
// Usa ALLOWED_ORIGINS="https://alfredbot.vercel.app,http://localhost:5173,http://localhost:3000"
var allowedOrigins = (builder.Configuration["ALLOWED_ORIGINS"] ??
                      "https://alfredbot.vercel.app,http://localhost:5173,http://localhost:3000")
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
    // Para localhost: SameSite=Lax + sin Secure
    var isLocalhost = builder.Configuration["PUBLIC_BASE_URL"]?.Contains("localhost") ?? false;
    o.Cookie.SameSite = isLocalhost ? SameSiteMode.Lax : SameSiteMode.None;
    o.Cookie.SecurePolicy = isLocalhost ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
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
     // 👇 este es el callback donde Google te devuelve el token
    o.CallbackPath = "/signin-google";
    // Más adelante, para Calendar:
    // o.Scope.Add("https://www.googleapis.com/auth/calendar.events");
    // o.AccessType = "offline";
    // o.Prompt = "consent";
    o.SaveTokens = false; // true si luego guardás tokens para Calendar
});

builder.Services.AddAuthorization();

// ===== Render: puerto dinámico =====
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
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
//autorización por roles:
app.MapGet("/api/admin/users", [Authorize(Roles = Roles.Admin)] async (AppDbContext db) =>
{
    return await db.Users.ToListAsync();
});
//Cuando devuelvas datos, asegurate de filtrar por MedicoId salvo que el rol sea admin.
app.MapGet("/api/turnos", async (ClaimsPrincipal user, [FromServices] AppDbContext db) =>
{
     var role = user.FindFirst(ClaimTypes.Role)?.Value ?? Roles.Medico;

    if (role == Roles.Admin)
    {
        return Results.Ok(await db.Turnos.ToListAsync());
    }

    if (role == Roles.Soporte)
    {
        return Results.Forbid(); // soporte no puede ver turnos
    }

    // default: medico → solo sus turnos
    var userEmail = user.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(userEmail))
        return Results.Unauthorized();

    var medico = await db.Medicos.FirstOrDefaultAsync(m => m.Email == userEmail);
    if (medico == null) return Results.NotFound();

    var turnos = await db.Turnos.Where(t => t.MedicoId == medico.Id).ToListAsync();
    return Results.Ok(turnos);
})
.RequireAuthorization(); // 👈 asegura que solo usuarios logueados accedan

// Devuelve datos del usuario autenticado (para Home/Perfil)
app.MapGet("/api/me", async (ClaimsPrincipal user, [FromServices] AppDbContext db) =>
{
    if (!(user.Identity?.IsAuthenticated ?? false))
        return Results.Unauthorized();

    var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = user.FindFirst("email")?.Value;
    var name = user.Identity?.Name ?? email;
    var picture = user.FindFirst("picture")?.Value;

    // Buscamos si ya existe en la BD
    var u = await db.Users.Include(x => x.Medico)
        .FirstOrDefaultAsync(x => x.GoogleSub == sub || x.Email == email);

    if (u == null)
    {
        // Primera vez → creamos usuario
        var role = email == "franjuarez2013@gmail.com"
            ? Roles.Admin
            : Roles.Medico;

        u = new User
        {
            GoogleSub = sub,
            Email = email!,
            Name = name ?? "",
            Picture = picture,
            Role = role
        };

        // Si es médico o admin → creamos un Medico base
        if (role == Roles.Medico || role == Roles.Admin)
        {
            u.Medico = new Medico
            {
                NombreCompleto = name ?? email!,
                Email = email!,
                Especialidad = null,
                Matricula = null
            };
        }

        db.Users.Add(u);
        await db.SaveChangesAsync();
    }
    else
    {
        // Si existe pero no tiene rol → corregimos
        if (string.IsNullOrEmpty(u.Role))
        {
            u.Role = email == "franjuarez2013@gmail.com"
                ? Roles.Admin
                : Roles.Medico;
        }

        // Si es medico/admin y no tiene Medico → creamos ahora
        if ((u.Role == Roles.Medico || u.Role == Roles.Admin) && u.Medico == null)
        {
            u.Medico = new Medico
            {
                NombreCompleto = u.Name ?? u.Email,
                Email = u.Email,
                Especialidad = null,
                Matricula = null
            };
        }

        db.Users.Update(u);
        await db.SaveChangesAsync();
    }

    // Devolvemos los datos
    return Results.Ok(new
    {
        u.Id,
        u.Email,
        u.Name,
        u.Picture,
        Role = u.Role,
        Medico = u.Medico == null ? null : new
        {
            u.Medico.Id,
            u.Medico.NombreCompleto,
            u.Medico.Especialidad,
            u.Medico.Matricula
        }
    });
})
.RequireAuthorization();

// ====== Google Calendar OAuth ======
// Nota: Aquí agregamos skeleton; implementación real de OAuth puede requerir Google SDK.
app.MapGet("/calendar/connect", (ClaimsPrincipal user, [FromServices] GoogleOAuthService oauth) =>
{
    if (!(user.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var email = user.FindFirst("email")?.Value;
    var url = oauth.GetConsentUrl(email);
    return Results.Redirect(url);
}).RequireAuthorization();

app.MapGet("/calendar/oauth-callback", async (
    HttpContext ctx,
    [FromServices] GoogleOAuthService oauth,
    [FromServices] AppDbContext db,
    [FromServices] TokenProtector tokens
) =>
{
    var code = ctx.Request.Query["code"].ToString();
    if (string.IsNullOrEmpty(code)) return Results.BadRequest(new { error = "missing_code" });

    // Leer email del state parameter
    var state = ctx.Request.Query["state"].ToString();
    var email = string.IsNullOrEmpty(state) ? null : state;
    
    if (string.IsNullOrEmpty(email)) 
        return Results.BadRequest(new { error = "missing_state", note = "Se requiere el email en el state parameter" });

    // Intercambio code -> tokens
    var (accessToken, refreshToken, expiresUtc) = await oauth.ExchangeCodeAsync(code);
    if (string.IsNullOrEmpty(refreshToken))
        return Results.BadRequest(new { error = "no_refresh_token", note = "Asegurate de usar prompt=consent&access_type=offline" });

    // Vincular a Medico del usuario
    var medico = await db.Medicos.Include(m => m.Integraciones).FirstOrDefaultAsync(m => m.Email == email);
    if (medico == null) return Results.NotFound(new { error = "medico_not_found" });

    var integ = await db.Integraciones.FirstOrDefaultAsync(i => i.MedicoId == medico.Id && i.Proveedor == "Google");
    if (integ == null)
    {
        integ = new Alfred2.Models.IntegracionCalendario
        {
            MedicoId = medico.Id,
            Proveedor = "Google",
            AccessTokenEnc = tokens.Protect(accessToken),
            RefreshTokenEnc = tokens.Protect(refreshToken),
            ExpiraUtc = expiresUtc
        };
        db.Integraciones.Add(integ);
    }
    else
    {
        integ.AccessTokenEnc = tokens.Protect(accessToken);
        integ.RefreshTokenEnc = tokens.Protect(refreshToken);
        integ.ExpiraUtc = expiresUtc;
        db.Integraciones.Update(integ);
    }
    await db.SaveChangesAsync();

    // Redirigir al frontend
    var frontendUrl = builder.Configuration["FRONTEND_REDIRECT_URL"] ?? "http://localhost:3000/home";
    var settingsUrl = frontendUrl.Replace("/home", "/settings");
    return Results.Redirect(settingsUrl);
});

// Verificar si el médico tiene Calendar conectado
app.MapGet("/api/calendar/status", async (ClaimsPrincipal user, [FromServices] AppDbContext db) =>
{
    if (!(user.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var email = user.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

    var medico = await db.Medicos.Include(m => m.Integraciones)
        .FirstOrDefaultAsync(m => m.Email == email);
    
    if (medico == null) return Results.NotFound(new { error = "medico_not_found" });

    var integ = medico.Integraciones.FirstOrDefault(i => i.Proveedor == "Google");
    var connected = integ != null && !string.IsNullOrEmpty(integ.RefreshTokenEnc);

    return Results.Ok(new
    {
        connected,
        provider = "Google",
        connectedAt = integ?.CreadoUtc,
        email = integ != null ? medico.Email : null
    });
}).RequireAuthorization();

// ====== Slots para frontend ======
app.MapGet("/api/slots", async (ClaimsPrincipal user, [FromServices] AppDbContext db, [FromServices] GCalService gcal, int count = 3) =>
{
    if (!(user.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var email = user.FindFirst("email")?.Value;
    var medico = await db.Medicos.FirstOrDefaultAsync(m => m.Email == email);
    if (medico is null) return Results.NotFound(new { error = "medico_not_found" });
    var slots = await gcal.GetNextSlotsAsync(medico.Id, count);
    return Results.Ok(slots);
}).RequireAuthorization();

// ====== Webhook WhatsApp ======
app.MapGet("/webhooks/whatsapp", (HttpRequest req, IConfiguration cfg) =>
{
    // Verificación Meta (hub.challenge)
    var verifyToken = cfg["VERIFY_TOKEN"] ?? "";
    var mode = req.Query["hub.mode"].ToString();
    var token = req.Query["hub.verify_token"].ToString();
    var challenge = req.Query["hub.challenge"].ToString();
    if (mode == "subscribe" && token == verifyToken)
        return Results.Text(challenge);
    return Results.Unauthorized();
});

app.MapPost("/webhooks/whatsapp", async (
    HttpRequest req,
    IConfiguration cfg,
    WhatsAppWebhookService parser,
    TwilioSignatureValidator sig,
    WhatsAppConversationService conversation,
    ILoggerFactory lf
) =>
{
    var log = lf.CreateLogger("whatsapp");
    var provider = (cfg["WH_PROVIDER"] ?? "twilio").Trim().ToLowerInvariant();
    log.LogInformation("Webhook WA provider={Provider} INTENT_MODE={IntentMode} CALENDAR_MODE={CalMode} PERSIST_TURNOS={Persist}",
        provider,
        (cfg["FeatureFlags:INTENT_MODE"] ?? "simple").Trim().ToLowerInvariant(),
        (cfg["FeatureFlags:CALENDAR_MODE"] ?? "simulate").Trim().ToLowerInvariant(),
        (cfg["FeatureFlags:PERSIST_TURNOS"] ?? "true").Trim());

    // Twilio: validar firma (en Development o sin token, no bloquea)
    if (provider == "twilio")
    {
        if (!req.HasFormContentType) return Results.BadRequest();
        var form = await req.ReadFormAsync();
        var env = builder.Environment.EnvironmentName;
        var sigHeader = req.Headers["X-Twilio-Signature"].FirstOrDefault();
        var baseUrl = (cfg["PUBLIC_BASE_URL"] ?? "").TrimEnd('/');
        var hasToken = !string.IsNullOrEmpty(cfg["TWILIO_AUTH_TOKEN"]);
        if (Uri.TryCreate(baseUrl + "/webhooks/whatsapp", UriKind.Absolute, out var url) && hasToken && !string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!sig.IsValid(url, form, sigHeader))
            {
                log.LogWarning("Firma Twilio inválida; rechazando request.");
                return Results.Unauthorized();
            }
        }
        else
        {
            log.LogWarning("Saltando validación de firma Twilio (env={Env}, hasToken={HasToken})", env, hasToken);
        }
    }

    var incoming = await parser.ParseAsync(req, provider);
    if (incoming == null) return Results.Ok();

    await conversation.HandleAsync(incoming, provider);
    return Results.Ok();
});

// ===== Controllers =====
app.MapControllers();

// ===== Dev-only endpoints =====
if (builder.Environment.IsDevelopment())
{
    app.MapPost("/dev/default-medico", async ([FromServices] AppDbContext db, Guid medicoId) =>
    {
        var medico = await db.Medicos.FindAsync(medicoId);
        if (medico == null) return Results.NotFound(new { error = "medico_not_found" });
        DevState.DefaultMedicoId = medicoId;
        return Results.Ok(new { ok = true, medicoId });
    });
}

app.Run();

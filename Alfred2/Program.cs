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
app.MapGet("/calendar/connect", ([FromServices] GoogleOAuthService oauth) =>
{
    var url = oauth.GetConsentUrl();
    return Results.Redirect(url);
}).RequireAuthorization();

app.MapGet("/calendar/oauth-callback", async (
    HttpContext ctx,
    [FromServices] GoogleOAuthService oauth,
    [FromServices] AppDbContext db
) =>
{
    var code = ctx.Request.Query["code"].ToString();
    if (string.IsNullOrEmpty(code)) return Results.BadRequest(new { error = "missing_code" });

    // Usuario logueado
    if (!(ctx.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var email = ctx.User.FindFirst("email")?.Value;
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

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
            AccessTokenEnc = accessToken, // TODO: cifrar
            RefreshTokenEnc = refreshToken,
            ExpiraUtc = expiresUtc
        };
        db.Integraciones.Add(integ);
    }
    else
    {
        integ.AccessTokenEnc = accessToken; // TODO: cifrar
        integ.RefreshTokenEnc = refreshToken;
        integ.ExpiraUtc = expiresUtc;
        db.Integraciones.Update(integ);
    }
    await db.SaveChangesAsync();

    return Results.Redirect("/healthz");
});

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
    PendingSlotsService pending,
    AppDbContext db,
    IntentService intents,
    TwilioResponder twilio,
    TwilioSignatureValidator sig,
    MetaResponder meta,
    GCalService gcal,
    ILoggerFactory lf
) =>
{
    var log = lf.CreateLogger("whatsapp");
    var provider = (cfg["WH_PROVIDER"] ?? "twilio").Trim().ToLowerInvariant();
    var calMode  = (cfg["FeatureFlags:CALENDAR_MODE"] ?? "simulate").Trim().ToLowerInvariant();
    var persist  = string.Equals((cfg["FeatureFlags:PERSIST_TURNOS"] ?? "true").Trim(), "true", StringComparison.OrdinalIgnoreCase);
    var intentMode = (cfg["FeatureFlags:INTENT_MODE"] ?? "simple").Trim().ToLowerInvariant();
    log.LogInformation("Webhook WA provider={Provider} INTENT_MODE={IntentMode} CALENDAR_MODE={CalMode} PERSIST_TURNOS={Persist}", provider, intentMode, calMode, persist);

    // Twilio firma
    if (provider == "twilio")
    {
        if (!req.HasFormContentType) return Results.BadRequest();
        var form = await req.ReadFormAsync();
        // Firma Twilio: en Development o si no hay token, no bloquea
        var env = builder.Environment.EnvironmentName;
        var sigHeader = req.Headers["X-Twilio-Signature"].FirstOrDefault();
        var baseUrl = (cfg["PUBLIC_BASE_URL"] ?? "").TrimEnd('/');
        var hasToken = !string.IsNullOrEmpty(cfg["TWILIO_AUTH_TOKEN"]);
        if (Uri.TryCreate(baseUrl + "/webhooks/whatsapp", UriKind.Absolute, out var url) && hasToken && !string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!sig.IsValid(url, form, sigHeader))
                log.LogWarning("Firma Twilio inválida (continuo en modo dev)");
        }
        else
        {
            log.LogWarning("Saltando validación de firma Twilio (env={Env}, hasToken={HasToken})", env, hasToken);
        }
        // No need to reset body: ReadFormAsync is cached
    }

    // curl de ejemplo (dev):
    // curl -X POST -H "Content-Type: application/x-www-form-urlencoded" \
    //      -d "From=whatsapp:+5491112345678&To=whatsapp:+14155238886&Body=turno&MessageSid=SM123" \
    //      http://localhost:{port}/webhooks/whatsapp

    var incoming = await parser.ParseAsync(req, provider);
    if (incoming == null) return Results.Ok();

    log.LogInformation("WA in {Provider} {From}->{To}: {Text}", incoming.Provider, incoming.FromE164, incoming.ToE164, incoming.Text);

    // Conversación básica: encontrar/crear médico por ToE164
    var medico = await db.Medicos.FirstOrDefaultAsync(m => m.TelefonoE164 == incoming.ToE164);
    if (medico == null && DevState.DefaultMedicoId.HasValue)
    {
        medico = await db.Medicos.FindAsync(DevState.DefaultMedicoId.Value);
        if (medico != null)
            log.LogWarning("Usando DevState.DefaultMedicoId={MedicoId} como fallback para To={To}", DevState.DefaultMedicoId, incoming.ToE164);
    }
    medico ??= await db.Medicos.FirstOrDefaultAsync(); // fallback primer médico
    if (medico == null) return Results.Ok();

    // Conversación/Paciente
    var paciente = await db.Pacientes.FirstOrDefaultAsync(p => p.MedicoId == medico.Id && p.TelefonoE164 == incoming.FromE164);
    if (paciente == null)
    {
        paciente = new Alfred2.Models.Paciente
        {
            MedicoId = medico.Id,
            TelefonoE164 = incoming.FromE164,
            NombreCompleto = "Paciente WhatsApp"
        };
        db.Pacientes.Add(paciente);
        await db.SaveChangesAsync();
    }

    var conv = await db.Conversaciones.FirstOrDefaultAsync(c => c.MedicoId == medico.Id && c.PacienteId == paciente.Id);
    if (conv == null)
    {
        conv = new Alfred2.Models.Conversacion
        {
            MedicoId = medico.Id,
            PacienteId = paciente.Id,
            UltimoMensajeUtc = DateTime.UtcNow
        };
        db.Conversaciones.Add(conv);
        await db.SaveChangesAsync();
    }
    else
    {
        conv.UltimoMensajeUtc = DateTime.UtcNow;
        db.Conversaciones.Update(conv);
        await db.SaveChangesAsync();
    }

    db.Mensajes.Add(new Alfred2.Models.Mensaje
    {
        ConversacionId = conv.Id,
        Direccion = Alfred2.Models.DireccionMensaje.Entrante,
        Texto = incoming.Text,
        EnviadoUtc = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    // Flujo simple de intención
    var intent = await intents.ClassifyAsync(incoming.Text);
    log.LogInformation("Intent detected: {Intent} whenTag={When}", intent.Intent, intent.WhenTag);

    async Task SendAsync(string to, string text)
    {
        if (provider == "twilio") await twilio.SendTextAsync(to, text);
        else await meta.SendTextAsync(to, text);
        // Persistimos mensaje saliente
        db.Mensajes.Add(new Alfred2.Models.Mensaje
        {
            ConversacionId = conv.Id,
            Direccion = Alfred2.Models.DireccionMensaje.Saliente,
            Texto = text,
            EnviadoUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    if (intent.Intent == "solicitar_turno")
    {
        var slots = (await gcal.GetNextSlotsAsync(medico.Id, 3)).ToList();
        log.LogInformation("Proposed {Count} slots", slots.Count);
        if (slots.Count == 0)
        {
            await SendAsync(incoming.FromE164, "No tengo disponibilidad por ahora. ¿Otro día?");
            return Results.Ok();
        }
        pending.Set(incoming.FromE164, medico.Id, slots.Select(s => (s.StartUtc, s.EndUtc)));
        var msg = $"Tengo estas opciones ({calMode}):\n1) {TimeFormatting.ToArDisplay(slots[0].StartUtc)}\n2) {TimeFormatting.ToArDisplay(slots[1].StartUtc)}\n3) {TimeFormatting.ToArDisplay(slots[2].StartUtc)}\nRespondé 1, 2 o 3.";
        await SendAsync(incoming.FromE164, msg);
        return Results.Ok();
    }

    // Si el usuario responde 1/2/3 y hay pending
    if (int.TryParse(incoming.Text.Trim(), out var n))
    {
        if (pending.TryGetValid(incoming.FromE164, out var pend) && n >= 1 && n <= pend.Slots.Count)
        {
            var choice = pend.Slots[n - 1];
            // Crear evento en Calendar según modo
            string eventId;
            if (calMode == "simulate")
            {
                log.LogInformation("CALENDAR_MODE=simulate → creando id simulado");
                eventId = $"simulated-{Guid.NewGuid():N}";
            }
            else
            {
                log.LogInformation("CALENDAR_MODE=real → llamando GCalService.CreateEventAsync");
                eventId = await gcal.CreateEventAsync(pend.MedicoId, paciente.NombreCompleto, choice.startUtc, choice.endUtc);
            }

            if (persist)
            {
                var turno = new Alfred2.Models.Turno
                {
                    MedicoId = pend.MedicoId,
                    PacienteId = paciente.Id,
                    ServicioId = null,
                    InicioUtc = choice.startUtc,
                    FinUtc = choice.endUtc,
                    Estado = Alfred2.Models.EstadoTurno.Confirmado,
                    Origen = Alfred2.Models.OrigenTurno.WhatsApp,
                    NotasInternas = $"GCAL {eventId}"
                };
                db.Turnos.Add(turno);
                await db.SaveChangesAsync();
                log.LogInformation("PERSIST_TURNOS=true → Turno {TurnoId} creado para médico {MedicoId}", turno.Id, pend.MedicoId);
            }
            else
            {
                log.LogInformation("PERSIST_TURNOS=false → no se persiste el turno");
            }
            pending.Clear(incoming.FromE164);

            log.LogInformation("Event created id={EventId} (mode={CalMode})", eventId, calMode);
            await SendAsync(incoming.FromE164, $"Listo, reservé tu turno para {TimeFormatting.ToArDisplay(choice.startUtc)}. ¡Gracias!");
            return Results.Ok();
        }
        else if (n >= 1 && n <= 3)
        {
            await SendAsync(incoming.FromE164, "Las opciones vencieron. Escribí 'turno' para ver disponibilidad actualizada.");
            return Results.Ok();
        }
    }

    // Otros casos
    if (!string.IsNullOrEmpty(intent.ClarifyCopy))
    {
        await SendAsync(incoming.FromE164, intent.ClarifyCopy);
        return Results.Ok();
    }

    await SendAsync(incoming.FromE164, "¿Querés pedir un turno? Decime, por ejemplo: 'turno martes 14:30'.");
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

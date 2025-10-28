using Alfred2.DBContext;
using Alfred2.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Alfred2.Services;

public record Slot(DateTime StartUtc, DateTime EndUtc);

public class GCalService
{
    private readonly AppDbContext _db;
    private readonly ILogger<GCalService> _log;
    private readonly IHttpClientFactory _httpFactory;
    private readonly GoogleOAuthService _oauth;
    private readonly IConfiguration _cfg;

    public GCalService(AppDbContext db, ILogger<GCalService> log, IHttpClientFactory httpFactory, GoogleOAuthService oauth, IConfiguration cfg)
    {
        _db = db; _log = log; _httpFactory = httpFactory; _oauth = oauth; _cfg = cfg;
    }

    public async Task<IReadOnlyList<Slot>> GetNextSlotsAsync(Guid medicoId, int count = 3, int durMin = 30)
    {
    // Flags: sólo desde FeatureFlags (no claves planas)
    var mode = (_cfg["FeatureFlags:CALENDAR_MODE"] ?? "simulate").Trim().ToLowerInvariant();
        _log.LogInformation("GCalService.GetNextSlotsAsync mode={Mode}", mode);
        var result = new List<Slot>();
        var dur = TimeSpan.FromMinutes(durMin);

        if (mode == "simulate")
        {
            var start = DateTime.UtcNow.AddMinutes(30);
            for (int i = 0; i < count; i++)
            {
                var s = start.AddMinutes(60 * i);
                result.Add(new Slot(s, s + dur));
            }
            return result;
        }

        // real: calcular según turnos existentes (fallback simple sin freeBusy aún)
        var now = DateTime.UtcNow.AddMinutes(5);

        // Cargar turnos existentes del médico para evitar solapamientos
        var existentes = await _db.Turnos
            .Where(t => t.MedicoId == medicoId && t.Estado == EstadoTurno.Confirmado && t.FinUtc > now)
            .OrderBy(t => t.InicioUtc)
            .ToListAsync();

        var cursor = now;
        while (result.Count < count)
        {
            var local = cursor.ToLocalTime();
            var inicioDia = new DateTime(local.Year, local.Month, local.Day, 9, 0, 0, DateTimeKind.Local);
            var finDia    = new DateTime(local.Year, local.Month, local.Day, 18, 0, 0, DateTimeKind.Local);
            var start = local > inicioDia ? local : inicioDia;

            for (var candidate = start; candidate + dur <= finDia; candidate = candidate.Add(dur))
            {
                var cStartUtc = candidate.ToUniversalTime();
                var cEndUtc   = cStartUtc + dur;
                var solapa = existentes.Any(t => !(cEndUtc <= t.InicioUtc || cStartUtc >= t.FinUtc));
                if (!solapa)
                {
                    result.Add(new Slot(cStartUtc, cEndUtc));
                    if (result.Count >= count) break;
                }
            }

            cursor = new DateTime(local.Year, local.Month, local.Day, 9, 0, 0, DateTimeKind.Local)
                        .AddDays(1).ToUniversalTime();
        }

        return result;
    }

    public async Task<string> CreateEventAsync(Guid medicoId, string pacienteNombre, DateTime startUtc, DateTime endUtc)
    {
    var mode = (_cfg["FeatureFlags:CALENDAR_MODE"] ?? "simulate").Trim().ToLowerInvariant();
        _log.LogInformation("GCalService.CreateEventAsync mode={Mode}", mode);
        if (mode == "simulate") return $"simulated-{Guid.NewGuid():N}";

        // real: Buscar integración guardada (si existiera)
        var integ = await _db.Integraciones.FirstOrDefaultAsync(i => i.MedicoId == medicoId && i.Proveedor == "Google");
        if (integ == null || string.IsNullOrEmpty(integ.RefreshTokenEnc))
        {
            _log.LogWarning("CALENDAR_MODE=real pero no hay integración/refresh token; devolviendo simulated");
            return $"simulated-{Guid.NewGuid():N}";
        }

        // Refrescar access token si hace falta
        var (accessToken, _) = await _oauth.RefreshAsync(integ.RefreshTokenEnc);

        // Crear evento en el calendario primario
        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.BaseAddress = new Uri("https://www.googleapis.com/calendar/v3/");

        var payload = new
        {
            summary = $"Turno: {pacienteNombre}",
            start = new { dateTime = startUtc.ToString("o"), timeZone = "UTC" },
            end = new { dateTime = endUtc.ToString("o"), timeZone = "UTC" }
        };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await http.PostAsync("calendars/primary/events", content);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            _log.LogWarning("Google Calendar error {Status}: {Body}", res.StatusCode, body);
            return $"simulated-{Guid.NewGuid():N}";
        }
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        return id ?? $"simulated-{Guid.NewGuid():N}";
    }
}

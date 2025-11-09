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

        // real: usar FreeBusy API de Google Calendar
        return await GetAvailableSlotsAsync(medicoId, count, durMin);
    }

    /// <summary>
    /// Obtiene slots disponibles usando Google Calendar FreeBusy API
    /// </summary>
    public async Task<IReadOnlyList<Slot>> GetAvailableSlotsAsync(Guid medicoId, int count = 3, int durMin = 30, int daysAhead = 7)
    {
        var result = new List<Slot>();
        var dur = TimeSpan.FromMinutes(durMin);

        // Buscar integración del médico
        var integ = await _db.Integraciones.FirstOrDefaultAsync(i => i.MedicoId == medicoId && i.Proveedor == "Google");
        if (integ == null || string.IsNullOrEmpty(integ.RefreshTokenEnc))
        {
            _log.LogWarning("No hay integración de Calendar para medico={MedicoId}, usando fallback simulate", medicoId);
            return await GetSimulatedSlots(count, durMin);
        }

        // Refrescar access token
        var (accessToken, _) = await _oauth.RefreshAsync(integ.RefreshTokenEnc);

        // Llamar a FreeBusy API
        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        http.BaseAddress = new Uri("https://www.googleapis.com/calendar/v3/");

        var timeMin = DateTime.UtcNow;
        var timeMax = timeMin.AddDays(daysAhead);

        var payload = new
        {
            timeMin = timeMin.ToString("o"),
            timeMax = timeMax.ToString("o"),
            items = new[] { new { id = "primary" } }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var res = await http.PostAsync("freeBusy", content);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _log.LogWarning("Google Calendar FreeBusy error {Status}: {Body}", res.StatusCode, body);
            return await GetSimulatedSlots(count, durMin);
        }

        using var doc = JsonDocument.Parse(body);
        var calendars = doc.RootElement.GetProperty("calendars");
        var primary = calendars.GetProperty("primary");
        var busyPeriods = new List<(DateTime start, DateTime end)>();

        if (primary.TryGetProperty("busy", out var busyArray))
        {
            foreach (var busy in busyArray.EnumerateArray())
            {
                var start = DateTime.Parse(busy.GetProperty("start").GetString()!);
                var end = DateTime.Parse(busy.GetProperty("end").GetString()!);
                busyPeriods.Add((start, end));
            }
        }

        // Generar slots disponibles (lunes a viernes, 9am-6pm)
        var cursor = DateTime.UtcNow.AddMinutes(30);
        var maxDate = cursor.AddDays(daysAhead);

        while (result.Count < count && cursor < maxDate)
        {
            var local = cursor.ToLocalTime();
            
            // Saltar fines de semana
            if (local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday)
            {
                cursor = cursor.AddDays(1);
                continue;
            }

            // Horario laboral: 9am - 6pm
            var dayStart = new DateTime(local.Year, local.Month, local.Day, 9, 0, 0, DateTimeKind.Local).ToUniversalTime();
            var dayEnd = new DateTime(local.Year, local.Month, local.Day, 18, 0, 0, DateTimeKind.Local).ToUniversalTime();

            var slotStart = cursor < dayStart ? dayStart : cursor;

            while (slotStart + dur <= dayEnd && result.Count < count)
            {
                var slotEnd = slotStart + dur;

                // Verificar si el slot no solapa con períodos ocupados
                var isBusy = busyPeriods.Any(b => !(slotEnd <= b.start || slotStart >= b.end));

                if (!isBusy)
                {
                    result.Add(new Slot(slotStart, slotEnd));
                }

                slotStart = slotStart.AddMinutes(durMin);
            }

            cursor = dayEnd;
        }

        return result;
    }

    private async Task<IReadOnlyList<Slot>> GetSimulatedSlots(int count, int durMin)
    {
        var result = new List<Slot>();
        var dur = TimeSpan.FromMinutes(durMin);
        var start = DateTime.UtcNow.AddMinutes(30);
        
        for (int i = 0; i < count; i++)
        {
            var s = start.AddMinutes(60 * i);
            result.Add(new Slot(s, s + dur));
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

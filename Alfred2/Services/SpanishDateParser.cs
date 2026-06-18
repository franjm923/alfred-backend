using System.Text.RegularExpressions;

namespace Alfred2.Services;

/// <summary>
/// Interpreta fecha/hora en español rioplatense desde texto libre:
/// hoy/mañana, lunes..domingo, dd/MM (año opcional), HH:mm con am/pm/hs.
/// Devuelve la hora LOCAL (DateTimeKind.Unspecified) o null si no la encuentra.
/// </summary>
public static class SpanishDateParser
{
    private static readonly Dictionary<string, DayOfWeek> DiasSemana = new()
    {
        ["domingo"] = DayOfWeek.Sunday,
        ["lunes"] = DayOfWeek.Monday,
        ["martes"] = DayOfWeek.Tuesday,
        ["miércoles"] = DayOfWeek.Wednesday,
        ["miercoles"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday,
        ["viernes"] = DayOfWeek.Friday,
        ["sábado"] = DayOfWeek.Saturday,
        ["sabado"] = DayOfWeek.Saturday,
    };

    public static DateTime? TryParse(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        var now = DateTime.Now;
        texto = texto.ToLowerInvariant();

        var fecha = ParseFecha(texto, now);
        var hora = ParseHora(texto, now);

        if (fecha.HasValue && hora.HasValue)
        {
            return new DateTime(fecha.Value.Year, fecha.Value.Month, fecha.Value.Day,
                                hora.Value.Hour, hora.Value.Minute, 0, DateTimeKind.Unspecified);
        }
        return null;
    }

    private static DateTime? ParseFecha(string texto, DateTime now)
    {
        DateTime? fecha = null;

        if (texto.Contains("hoy")) fecha = now.Date;
        else if (texto.Contains("mañana")) fecha = now.Date.AddDays(1);
        else
        {
            foreach (var (palabra, dow) in DiasSemana)
            {
                if (texto.Contains(palabra))
                {
                    fecha = NextDayOfWeek(now.Date, dow);
                    break;
                }
            }
        }

        // dd/MM o dd-MM (año opcional) — tiene prioridad si aparece una fecha explícita
        var m = Regex.Match(texto, @"\b(\d{1,2})[\/\-](\d{1,2})(?:[\/\-](\d{2,4}))?\b");
        if (m.Success)
        {
            var dia = int.Parse(m.Groups[1].Value);
            var mes = int.Parse(m.Groups[2].Value);
            var anio = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : now.Year;
            fecha = new DateTime(anio, mes, dia);
        }
        return fecha;
    }

    private static DateTime? ParseHora(string texto, DateTime now)
    {
        var m = Regex.Match(texto, @"\b(\d{1,2})[:\.h\s]?(\d{2})?\s*(am|pm|hs)?\b");
        if (!m.Success) return null;

        int h = int.Parse(m.Groups[1].Value);
        int min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        var sufijo = m.Groups[3].Success ? m.Groups[3].Value : null;

        if (string.Equals(sufijo, "pm", StringComparison.OrdinalIgnoreCase) && h < 12) h += 12;
        if (string.Equals(sufijo, "am", StringComparison.OrdinalIgnoreCase) && h == 12) h = 0;

        return now.Date.AddHours(h).AddMinutes(min);
    }

    private static DateTime NextDayOfWeek(DateTime from, DayOfWeek day)
    {
        int diff = ((int)day - (int)from.DayOfWeek + 7) % 7;
        if (diff == 0) diff = 7;
        return from.AddDays(diff);
    }
}

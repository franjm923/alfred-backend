namespace Alfred2.Services;

/// <summary>
/// Resolución de zona horaria con fallback a Argentina (IANA o Windows) y
/// formateo de fechas UTC a display local.
/// </summary>
public static class TimeZoneHelper
{
    private const string DefaultIana = "America/Argentina/Buenos_Aires";
    private const string WindowsId = "Argentina Standard Time";

    /// <summary>
    /// Devuelve la zona pedida; si no existe en el SO, cae a Argentina (IANA → Windows → Local).
    /// </summary>
    public static TimeZoneInfo Get(string? iana = null)
    {
        foreach (var id in new[] { iana, DefaultIana, WindowsId })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* probar el siguiente */ }
        }
        return TimeZoneInfo.Local;
    }

    /// <summary>Formatea una fecha UTC a "ddd dd/MM HH:mm" en hora argentina.</summary>
    public static string ToArDisplay(DateTime utc)
    {
        var normalized = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(normalized, Get());
        return local.ToString("ddd dd/MM HH:mm");
    }
}

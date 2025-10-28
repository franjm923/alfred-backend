using Alfred2.OpenAIService;
using System.Text.Json;

namespace Alfred2.Services;

public record IntentResult(string Intent, string? WhenTag, string? ClarifyCopy);

public class IntentService
{
    private readonly OpenAIChatService _openai;
    private readonly IConfiguration _cfg;
    private readonly ILogger<IntentService> _log;
    public IntentService(OpenAIChatService openai, IConfiguration cfg, ILogger<IntentService> log)
    { _openai = openai; _cfg = cfg; _log = log; }

    // Muy simple: pedimos al LLM que devuelva JSON con intent y whenTag
    public async Task<IntentResult> ClassifyAsync(string text)
    {
        text = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return new IntentResult("otro", null, "No te entendí. ¿Querés pedir un turno?");

    // Flags: sólo desde FeatureFlags (no claves planas)
    var mode = (_cfg["FeatureFlags:INTENT_MODE"] ?? "simple").Trim().ToLowerInvariant();
        _log.LogInformation("IntentService mode: {Mode}", mode);

        if (mode == "simple")
        {
            // Heurística rápida
            var low = text.ToLowerInvariant();
            if (low.Contains("turno") || low.Contains("cita") || low.Contains("consulta"))
            {
                // whenTag muy simple
                string? when = null;
                if (low.Contains("mañana") || low.Contains("manana")) when = "manana";
                else if (low.Contains("tarde")) when = "tarde";
                else if (low.Contains("mañana a la tarde") || low.Contains("manana a la tarde")) when = "manana_tarde";
                else if (low.Contains("semana que viene") || low.Contains("próxima semana") || low.Contains("proxima semana")) when = "proxima_semana";

                return new IntentResult("solicitar_turno", when, null);
            }
            if (low.Contains("cancelar"))
                return new IntentResult("cancelar", null, null);
            if (low.Contains("hola") || low.Contains("buenas") || low.Contains("buen día") || low.Contains("buen dia"))
                return new IntentResult("saludo", null, null);

            return new IntentResult("otro", null, "¿Querés pedir un turno? Por ejemplo: \"turno martes 14:30\"");
        }

        // LLM mode usando OpenAIChatService existente para extraer intención aproximada
        try
        {
            _log.LogInformation("INTENT_MODE=llm → usando OpenAIChatService.ExtraerTurnoAsync");
            var dto = await _openai.ExtraerTurnoAsync(text);
            // Si LLM devuelve algo interpretable, lo consideramos solicitud de turno
            if (dto != null)
            {
                // whenTag: preferimos LocalInicio si lo calculó; si no, sugerimos pedir aclaración
                if (dto.LocalInicio.HasValue)
                {
                    var when = dto.LocalInicio.Value.ToString("yyyy-MM-dd HH:mm");
                    return new IntentResult("solicitar_turno", when, null);
                }
                if (dto.Faltan?.Contains("fecha y hora", StringComparer.OrdinalIgnoreCase) == true)
                {
                    return new IntentResult("solicitar_turno", null, dto.Copy ?? "¿Preferís mañana por la tarde o el martes a la mañana?");
                }
                // Si no hay LocalInicio ni faltan fecha/hora, aun así tratamos como intención débil
                return new IntentResult("solicitar_turno", null, dto.Copy);
            }
            return new IntentResult("otro", null, "¿Querés pedir un turno? Por ejemplo: \"turno martes 14:30\"");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fallo LLM, vuelvo a heurística simple");
            // fallback: heurística simple
            var low = text.ToLowerInvariant();
            if (low.Contains("turno") || low.Contains("cita") || low.Contains("consulta"))
                return new IntentResult("solicitar_turno", null, null);
            if (low.Contains("cancelar")) return new IntentResult("cancelar", null, null);
            if (low.Contains("hola") || low.Contains("buenas") || low.Contains("buen día") || low.Contains("buen dia")) return new IntentResult("saludo", null, null);
            return new IntentResult("otro", null, "¿Querés pedir un turno? Por ejemplo: \"turno martes 14:30\"");
        }
    }

    // Contrato corto pedido: devuelve solo (intent, whenTag)
    public async Task<(string intent, string? whenTag)> ClassifyTupleAsync(string text)
    {
        var res = await ClassifyAsync(text);
        return (res.Intent, res.WhenTag);
    }
}

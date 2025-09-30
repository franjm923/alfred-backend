using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Alfred2.Models; // Para ModalidadTurno

namespace Alfred2.DTOs
{
    // Entrada del bot (desde WhatsApp u otro canal)
    public class Inbound
    {
        public string TelefonoBot { get; set; } = string.Empty; // línea del médico (E164 sin '+')
        public string Telefono { get; set; } = string.Empty;    // paciente (E164 sin '+')
        public string? Nombre { get; set; }                     // opcional, nombre del paciente
        public string Texto { get; set; } = string.Empty;       // mensaje libre
    }

    // Confirmar/cerrar un turno desde el panel admin
    public class ConfirmarTurnoDTO
    {
        public decimal? PrecioAcordado { get; set; }
        public ModalidadTurno? Modalidad { get; set; } // Presencial/Virtual (opcional al confirmar)
        public string? Notas { get; set; }
    }

    // Cancelar un turno
    public class CancelarTurnoDTO
    {
        public string? Motivo { get; set; }
    }

    // (Opcional) Reprogramar un turno
    public class ReprogramarTurnoDTO
    {
        // Fecha/hora LOCAL del médico (se convertirá a UTC en el controller/servicio)
        public DateTime NuevaFechaHoraLocal { get; set; }
        public int? NuevaDuracionMin { get; set; }
        public Guid? NuevoServicioId { get; set; }
        public ModalidadTurno? NuevaModalidad { get; set; }
        public string? Notas { get; set; }
    }

    // Salida de un extractor (LLM u otro) orientado a TURNOS
    public class ExtraccionTurnoDTO
    {
        public string? Servicio { get; set; }           // nombre del servicio (match por nombre)
        public DateTime? LocalInicio { get; set; }      // fecha/hora LOCAL del médico
        public int? DuracionMin { get; set; }           // duración sugerida
        public ModalidadTurno? Modalidad { get; set; }  // Presencial/Virtual
        public string? Nombre { get; set; }             // nombre del paciente (si lo captura)
        public List<string> Faltan { get; set; } = new();
        public string? Copy { get; set; }               // texto breve para repreguntar

        /// <summary>
        /// Intenta parsear una respuesta de un LLM con distintos formatos comunes:
        ///  - { "output_parsed": { ...dto... } }
        ///  - { "output": [ { "content": [ { "type": "output_json", "json": { ...dto... } }, ... ] } ] }
        ///  - { "output": [ { "content": [ { "type": "output_text", "text": "{...dto...}" } ] } ] }
        /// Devuelve un DTO vacío si no encuentra nada.
        /// </summary>
        public static ExtraccionTurnoDTO TryParseFromResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // 1) output_parsed directo
                if (root.TryGetProperty("output_parsed", out var parsed))
                {
                    var dto = JsonSerializer.Deserialize<ExtraccionTurnoDTO>(parsed.GetRawText());
                    return dto ?? new ExtraccionTurnoDTO();
                }

                // 2) recorrer output -> content
                if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in output.EnumerateArray())
                    {
                        if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var c in content.EnumerateArray())
                        {
                            if (!c.TryGetProperty("type", out var t)) continue;
                            var type = t.GetString();

                            // a) output_json
                            if (type == "output_json" && c.TryGetProperty("json", out var jsonEl))
                            {
                                var dto = JsonSerializer.Deserialize<ExtraccionTurnoDTO>(jsonEl.GetRawText());
                                if (dto != null) return dto;
                            }

                            // b) output_text que contiene JSON
                            if (type == "output_text" && c.TryGetProperty("text", out var textEl))
                            {
                                var text = textEl.GetString();
                                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("{"))
                                {
                                    try
                                    {
                                        var dto = JsonSerializer.Deserialize<ExtraccionTurnoDTO>(text);
                                        if (dto != null) return dto;
                                    }
                                    catch { /* ignorar */ }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* ignorar parse errors */ }

            return new ExtraccionTurnoDTO();
        }
    }
}

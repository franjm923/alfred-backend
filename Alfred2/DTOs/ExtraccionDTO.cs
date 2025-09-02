using System.Collections.Generic;
using System.Text.Json;
using Alfred2.Models; // Asumiendo que Solicitud está aquí

namespace Alfred2.DTOs
{
    // Entrada del bot (desde WhatsApp/tu canal)
    public class Inbound
    {
         public string TelefonoBot { get; set; }
        public string Telefono { get; set; }
        public string Nombre { get; set; }
        public string Texto { get; set; }
    }

    // Aceptar pedido desde admin
    public class AceptarDTO
    {
        public decimal PrecioEnvio { get; set; }
        public decimal PrecioTotal { get; set; }
    }

    // Salida del LLM (Structured Output)
    public class ExtraccionDTO
    {
        public string Tipo { get; set; }       // "pedido" | "turno"
        public string Producto { get; set; }
        public int? Cantidad { get; set; }
        public string Direccion { get; set; }
        public string FormaPago { get; set; }
        public string Nombre { get; set; }
        public List<string> Faltan { get; set; } = new();
        public string Copy { get; set; }       // texto breve para repreguntar

        public static ExtraccionDTO ParseExtraccionFromResponses(string json, Solicitud borradorActual)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 1) Si existe 'output_parsed' (algunos SDK/formatos lo exponen), úsalo directo
            if (root.TryGetProperty("output_parsed", out var parsed))
            {
                var dto = JsonSerializer.Deserialize<ExtraccionDTO>(parsed.GetRawText());
                return dto ?? new ExtraccionDTO { Faltan = new List<string> { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };
            }

            // 2) Recorrer 'output' -> 'content' y buscar un bloque JSON estructurado
            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in output.EnumerateArray())
                {
                    if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var c in content.EnumerateArray())
                        {
                            if (c.TryGetProperty("type", out var t))
                            {
                                var type = t.GetString();
                                // a) Ideal: 'output_json' con un objeto 'json'
                                if (type == "output_json" && c.TryGetProperty("json", out var jsonEl))
                                {
                                    var dto = JsonSerializer.Deserialize<ExtraccionDTO>(jsonEl.GetRawText());
                                    if (dto != null) return dto;
                                }
                                // b) Fallback: 'output_text' que sea un JSON en texto
                                if (type == "output_text" && c.TryGetProperty("text", out var textEl))
                                {
                                    var text = textEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith("{"))
                                    {
                                        try
                                        {
                                            var dto = JsonSerializer.Deserialize<ExtraccionDTO>(text);
                                            if (dto != null) return dto;
                                        }
                                        catch {/* ignore */}
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 3) Último recurso: pregunta por dirección
            return new ExtraccionDTO { Faltan = new List<string> { "direccion" }, Copy = "¿A qué dirección lo mandamos?" };
        }
    }
}

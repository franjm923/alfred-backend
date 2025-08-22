using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Alfred2.DBContext;
using Alfred2.Models;
using System.Text.Encodings.Web;


using Alfred2.OpenAIService;
using System.Net.Http;

namespace Alfred2.Controladores
{
    [ApiController]
    [Route("webhook/whatsapp")]
    public class WhatsAppCloudWebhookController : ControllerBase
{
        private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AppDbContext _db;
        private readonly OpenAIChatService _llm;

        public WhatsAppCloudWebhookController(IConfiguration cfg, IHttpClientFactory httpFactory,
                                              AppDbContext db, OpenAIChatService llm)
        {
            _cfg = cfg; _httpFactory = httpFactory; _db = db; _llm = llm;
        }

        // 1) Verificación (Meta llama con GET una sola vez)
        [HttpGet]
        public IActionResult Verify(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            var verify = (_cfg.GetValue<string>("WHATSAPP_VERIFY_TOKEN") ?? "").Trim();
            var got = (token ?? "").Trim();

            // DEBUG (temporal): te muestra en la consola qué está leyendo
            Console.WriteLine($"[Webhook Verify] mode='{mode}', cfg='{verify}'(len={verify.Length}) got='{got}'(len={got.Length}) challengeLen={challenge?.Length}");

            if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(challenge)
                && string.Equals(got, verify, StringComparison.Ordinal))
            {
                return Content(challenge, "text/plain", System.Text.Encoding.UTF8);
            }

            return Unauthorized("Invalid verify token");
        }

        // 2) Mensajes entrantes
        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            using var doc = JsonDocument.Parse(body);

            // Busca el primer mensaje de texto
            var root = doc.RootElement;
            var entries = root.GetProperty("entry");
            foreach (var entry in entries.EnumerateArray())
            {
                foreach (var change in entry.GetProperty("changes").EnumerateArray())
                {
                    var value = change.GetProperty("value");
                    if (!value.TryGetProperty("messages", out var messages)) continue;

                    foreach (var msg in messages.EnumerateArray())
                    {
                        var from = msg.GetProperty("from").GetString();        // número del usuario (E.164)
                        var type = msg.GetProperty("type").GetString();
                        if (type != "text") continue;

                        var text = msg.GetProperty("text").GetProperty("body").GetString() ?? "";

                        // Lógica de tu bot (igual que en BotController)
                        var reply = await HandleIncomingAsync(from, text);
                        if (!string.IsNullOrWhiteSpace(reply))
                            await SendWhatsAppAsync(from, reply);
                    }
                }
            }
            return Ok();
        }

        // --- MINI BOT FLOW (mismo que BotController.Mensaje, compactado) ---
        private async Task<string> HandleIncomingAsync(string telefono, string texto)
        {
            if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(texto)) return null;

            // Cliente o crear
            var cli = await _db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono);
            if (cli == null)
            {
                cli = new Cliente { Telefono = telefono, Nombre = "Cliente" };
                _db.Clientes.Add(cli);
                await _db.SaveChangesAsync();
            }

            // Borrador
            var borrador = await _db.Solicitudes
                .FirstOrDefaultAsync(s => s.ClienteId == cli.Id && s.Estado == EstadoSolicitud.Borrador);
            if (borrador == null)
            {
                borrador = new Solicitud { ClienteId = cli.Id, NombreCliente = cli.Nombre };
                _db.Solicitudes.Add(borrador);
                await _db.SaveChangesAsync();
            }

            // Extraer con LLM
            var ext = await _llm.ExtraerAsync(texto, borrador);

            // Merge
            if (!string.IsNullOrWhiteSpace(ext.Tipo))
                borrador.Tipo = ext.Tipo.ToLower() == "turno" ? TipoSolicitud.Turno : TipoSolicitud.Pedido;

            borrador.Producto = ext.Producto ?? borrador.Producto;
            borrador.Cantidad = ext.Cantidad ?? borrador.Cantidad;
            borrador.Direccion = ext.Direccion ?? borrador.Direccion;
            borrador.FormaPago = ext.FormaPago ?? borrador.FormaPago;
            borrador.NombreCliente = ext.Nombre ?? borrador.NombreCliente;

            // Completo?
            var faltan = new List<string>();
            if (borrador.Tipo == TipoSolicitud.Pedido)
            {
                if (string.IsNullOrWhiteSpace(borrador.Producto)) faltan.Add("producto");
                if (!borrador.Cantidad.HasValue) faltan.Add("cantidad");
            }
            if (string.IsNullOrWhiteSpace(borrador.Direccion)) faltan.Add("direccion");
            if (string.IsNullOrWhiteSpace(borrador.FormaPago)) faltan.Add("formaPago");
            if (string.IsNullOrWhiteSpace(borrador.NombreCliente)) faltan.Add("nombre");

            if (faltan.Count == 0)
            {
                borrador.Estado = EstadoSolicitud.Pendiente;
                await _db.SaveChangesAsync();
                return "¡Listo! Registré tu solicitud. En breve te paso el total con envío.";
            }
            else
            {
                await _db.SaveChangesAsync();
                return ext.Copy ?? "¿Me pasás ese dato?";
            }
        }

        // --- Enviar mensaje al usuario usando Cloud API ---
        private async Task SendWhatsAppAsync(string toE164, string text)
        {
            var token = _cfg["WHATSAPP_TOKEN"];
            var phoneId = _cfg["WHATSAPP_PHONE_NUMBER_ID"];
            var client = _httpFactory.CreateClient("whatsapp");

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                messaging_product = "whatsapp",
                to = toE164,
                type = "text",
                text = new { body = text }
            };

            var json = JsonSerializer.Serialize(payload);
            var res = await client.PostAsync(
                $"https://graph.facebook.com/v20.0/{phoneId}/messages",
                new StringContent(json, Encoding.UTF8, "application/json"));

            // (opcional) manejar errores/respuesta
        }
    

        private static string NormalizeE164(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = s.Replace("whatsapp:", "").Trim();
            if (s.StartsWith("+")) s = s.Substring(1);
            var digits = new string(s.Where(char.IsDigit).ToArray());
            return digits;
        }
    }
}

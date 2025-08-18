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
    [Route("webhook/twilio")]
    public class TwilioWebhookController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly OpenAIChatService _llm;

        public TwilioWebhookController(AppDbContext db, OpenAIChatService llm)
        {
            _db = db; _llm = llm;
        }

        public class TwilioInbound
        {
            public string From { get; set; }   // ej: "whatsapp:+54911XXXXXXX"
            public string Body { get; set; }   // texto del usuario
            public string WaId { get; set; }   // solo dígitos del usuario
            public string ProfileName { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Receive([FromForm] TwilioInbound f)
        {
            // Normaliza el teléfono a E.164 sin prefijo "whatsapp:" ni '+'
            var telefono = NormalizeE164(f.From ?? f.WaId);
            var texto = f.Body ?? string.Empty;

            // --- mismo flujo que usás en Cloud API ---
            if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(texto))
                return Ok(); // no cortar el webhook

            var cli = await _db.Clientes.FirstOrDefaultAsync(c => c.Telefono == telefono);
            if (cli == null)
            {
                cli = new Cliente { Telefono = telefono, Nombre = f.ProfileName ?? "Cliente" };
                _db.Clientes.Add(cli);
                await _db.SaveChangesAsync();
            }

            var borrador = await _db.Solicitudes
                .FirstOrDefaultAsync(s => s.ClienteId == cli.Id && s.Estado == EstadoSolicitud.Borrador);
            if (borrador == null)
            {
                borrador = new Solicitud { ClienteId = cli.Id, NombreCliente = cli.Nombre };
                _db.Solicitudes.Add(borrador);
                await _db.SaveChangesAsync();
            }

            var ext = await _llm.ExtraerAsync(texto, borrador);

            if (!string.IsNullOrWhiteSpace(ext.Tipo))
                borrador.Tipo = ext.Tipo.ToLower() == "turno" ? TipoSolicitud.Turno : TipoSolicitud.Pedido;

            borrador.Producto      = ext.Producto      ?? borrador.Producto;
            borrador.Cantidad      = ext.Cantidad      ?? borrador.Cantidad;
            borrador.Direccion     = ext.Direccion     ?? borrador.Direccion;
            borrador.FormaPago     = ext.FormaPago     ?? borrador.FormaPago;
            borrador.NombreCliente = ext.Nombre        ?? borrador.NombreCliente;

            var faltan = new List<string>();
            if (borrador.Tipo == TipoSolicitud.Pedido)
            {
                if (string.IsNullOrWhiteSpace(borrador.Producto)) faltan.Add("producto");
                if (!borrador.Cantidad.HasValue) faltan.Add("cantidad");
            }
            if (string.IsNullOrWhiteSpace(borrador.Direccion))  faltan.Add("direccion");
            if (string.IsNullOrWhiteSpace(borrador.FormaPago))  faltan.Add("formaPago");
            if (string.IsNullOrWhiteSpace(borrador.NombreCliente)) faltan.Add("nombre");

            string reply;
            if (faltan.Count == 0)
            {
                borrador.Estado = EstadoSolicitud.Pendiente;
                await _db.SaveChangesAsync();
                reply = "¡Listo! Registré tu solicitud. En breve te paso el total con envío.";
            }
            else
            {
                await _db.SaveChangesAsync();
                reply = ext.Copy ?? "¿Me pasás ese dato?";
            }

            // Responder en Twilio con TwiML (WhatsApp lo soporta)
            var safe = HtmlEncoder.Default.Encode(reply ?? "👍");
            var twiml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
        <Response><Message>{safe}</Message></Response>";

            return Content(twiml, "application/xml", Encoding.UTF8);
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

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Alfred2.DBContext;
using Alfred2.Models;
using Alfred2.OpenAIService;
using Alfred2.DTOs;

namespace Alfred2.Controladores
{
    [ApiController]
    [Route("api/bot")]
    public class BotController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly OpenAIChatService _llm;

        public BotController(AppDbContext db, OpenAIChatService llm)
        {
            _db = db; _llm = llm;
        }

        [HttpPost("mensaje")]
        public async Task<IActionResult> Mensaje([FromBody] Inbound dto)
        {
             if (string.IsNullOrWhiteSpace(dto?.TelefonoBot) ||
                string.IsNullOrWhiteSpace(dto?.Telefono)   ||
                string.IsNullOrWhiteSpace(dto?.Texto))
                return BadRequest("Faltan datos mínimos (telefonoBot, telefono, texto).");

            // 0) Usuario dueño del bot
            var usr = await _db.Usuarios.FirstOrDefaultAsync(u => u.TelefonoBot == dto.TelefonoBot);
            if (usr == null)
                return BadRequest("Bot no reconocido");
                
            // 1) Cliente (o crear)
             var cli = await _db.Clientes
                .FirstOrDefaultAsync(c => c.UsuarioId == usr.Id && c.Telefono == dto.Telefono);
            if (cli == null)
            {
                cli = new Cliente { Telefono = dto.Telefono, Nombre = dto.Nombre ?? "Cliente", UsuarioId = usr.Id };
                _db.Clientes.Add(cli);
                await _db.SaveChangesAsync();
            }

            // 2) Borrador actual
            var borrador = await _db.Solicitudes
                .FirstOrDefaultAsync(s => s.ClienteId == cli.Id && s.UsuarioId == usr.Id && s.Estado == EstadoSolicitud.Borrador);

            if (borrador == null)
            {
                borrador = new Solicitud { ClienteId = cli.Id, NombreCliente = cli.Nombre, UsuarioId = usr.Id };
                _db.Solicitudes.Add(borrador);
                await _db.SaveChangesAsync();
            }

            // 3) Extraer con LLM (o mock)
            var ext = await _llm.ExtraerAsync(dto.Texto, borrador);

            // 4) Merge de campos
            if (!string.IsNullOrWhiteSpace(ext.Tipo))
                borrador.Tipo = ext.Tipo.ToLower() == "turno" ? TipoSolicitud.Turno : TipoSolicitud.Pedido;

            borrador.Producto      = ext.Producto      ?? borrador.Producto;
            borrador.Cantidad      = ext.Cantidad      ?? borrador.Cantidad;
            borrador.Direccion     = ext.Direccion     ?? borrador.Direccion;
            borrador.FormaPago     = ext.FormaPago     ?? borrador.FormaPago;
            borrador.NombreCliente = ext.Nombre        ?? borrador.NombreCliente;

            // 5) Chequeo de completitud (simple)
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
                return Ok(new { reply = "¡Listo! Registré tu solicitud. En breve te paso el total con envío." });
            }
            else
            {
                await _db.SaveChangesAsync();
                return Ok(new { reply = ext.Copy ?? "¿Me pasás ese dato?" });
            }
        }
    }
}

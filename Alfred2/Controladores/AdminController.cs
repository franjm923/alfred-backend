using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Alfred2.DBContext;
using Alfred2.Models;
using Alfred2.DTOs;

namespace Alfred2.Controladores
{
    [ApiController]
    [Route("api/admin/solicitudes")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminController(AppDbContext db) { _db = db; }

        [HttpGet("pendientes")]
        public async Task<IActionResult> Pendientes()
        {
            var list = await _db.Solicitudes
                .Where(s => s.Estado == EstadoSolicitud.Pendiente)
                .OrderByDescending(s => s.Creado)
                .Select(s => new {
                    s.Id,
                    s.Tipo,
                    s.Producto,
                    s.Cantidad,
                    s.Direccion,
                    s.FormaPago,
                    Cliente = s.NombreCliente,
                    s.PrecioEnvio,
                    s.PrecioTotal
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("{id}/aceptar")]
        public async Task<IActionResult> Aceptar(int id, [FromBody] AceptarDTO dto)
        {
            var s = await _db.Solicitudes.FindAsync(id);
            if (s == null || s.Estado != EstadoSolicitud.Pendiente) return NotFound();

            s.PrecioEnvio = dto.PrecioEnvio;
            s.PrecioTotal = dto.PrecioTotal;
            s.Estado = EstadoSolicitud.Confirmado;

            await _db.SaveChangesAsync();

            // TODO: enviar mensaje al cliente con el total (integración WhatsApp)
            return Ok(new { ok = true });
        }
    }
}

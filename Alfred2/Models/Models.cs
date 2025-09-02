using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alfred2.Models;
public enum EstadoSolicitud { Borrador, Pendiente, Confirmado }
public enum TipoSolicitud { Pedido, Turno }

public class Usuario
{
    public int Id { get; set; }
    public string? Nombre { get; set; }
    public string? TelefonoBot { get; set; }

    public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
    public ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
}

public class Cliente
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string? Nombre { get; set; }
    public string? Telefono { get; set; } // clave de WhatsApp

    public ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
}

public class Solicitud
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public TipoSolicitud Tipo { get; set; } = TipoSolicitud.Pedido;
    public string? Producto { get; set; }       // null si es turno
    public int? Cantidad { get; set; }         // null si es turno
    public string? Direccion { get; set; }
    public string? FormaPago { get; set; }      // efectivo | transferencia | tarjeta
    public string? NombreCliente { get; set; }

    public decimal? PrecioEnvio { get; set; }
    public decimal? PrecioTotal { get; set; }

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Borrador;
    public  DateTime Creado { get; set; } = DateTime.UtcNow;
}

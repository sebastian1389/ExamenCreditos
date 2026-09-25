using Microsoft.AspNetCore.Identity;

namespace CreditosApp.Models;

public class Cliente
{
    public int Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    public decimal IngresosMensuales { get; set; }

    public bool Activo { get; set; }

    public IdentityUser? Usuario { get; set; }

    public ICollection<SolicitudCredito> Solicitudes { get; set; } = new List<SolicitudCredito>();
}
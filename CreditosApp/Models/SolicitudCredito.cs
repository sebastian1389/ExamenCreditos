namespace CreditosApp.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public decimal MontoSolicitado { get; set; }

    public DateTime FechaSolicitud { get; set; }

    public EstadoSolicitud Estado { get; set; }

    public string? MotivoRechazo { get; set; }

    public Cliente? Cliente { get; set; }
}

public enum EstadoSolicitud
{
    Pendiente,
    Aprobado,
    Rechazado
}
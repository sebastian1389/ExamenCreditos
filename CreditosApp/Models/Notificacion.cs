namespace CreditosApp.Models;

public class Notificacion
{
    public int Id { get; set; }

    public Guid MessageId { get; set; }

    public int SolicitudId { get; set; }

    public string UsuarioId { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; }
}

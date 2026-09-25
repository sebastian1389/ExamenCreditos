namespace CreditosApp.Messaging;

public sealed class SolicitudRegistradaMessage
{
    public Guid MessageId { get; init; }

    public int SolicitudId { get; init; }

    public string UsuarioId { get; init; } = string.Empty;

    public DateTime FechaEventoUtc { get; init; }
}

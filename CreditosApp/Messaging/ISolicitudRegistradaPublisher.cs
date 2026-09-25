namespace CreditosApp.Messaging;

public interface ISolicitudRegistradaPublisher
{
    Task PublishAsync(SolicitudRegistradaMessage message, CancellationToken cancellationToken = default);
}

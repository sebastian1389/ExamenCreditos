namespace CreditosApp.Messaging;

public sealed class RabbitMqOptions
{
    public const string QueueName = "solicitudes.notificaciones";

    public string ConnectionString { get; set; } = string.Empty;

    public bool ConsumerEnabled { get; set; }
}

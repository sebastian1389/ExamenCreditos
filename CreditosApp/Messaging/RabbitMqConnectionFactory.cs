using RabbitMQ.Client;

namespace CreditosApp.Messaging;

public static class RabbitMqConnectionFactory
{
    public static ConnectionFactory Create(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("RabbitMq__ConnectionString no está configurada.");
        }

        if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "amqps", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("RabbitMq__ConnectionString debe usar el esquema amqps.");
        }

        return new ConnectionFactory
        {
            Uri = uri,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };
    }
}

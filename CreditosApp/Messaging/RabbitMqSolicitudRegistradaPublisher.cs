using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CreditosApp.Messaging;

public sealed class RabbitMqSolicitudRegistradaPublisher : ISolicitudRegistradaPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqSolicitudRegistradaPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqSolicitudRegistradaPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqSolicitudRegistradaPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        SolicitudRegistradaMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogWarning("No se publicó la notificación porque RabbitMq__ConnectionString no está configurada.");
            return;
        }

        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: RabbitMqOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Persistent = true,
            MessageId = message.MessageId.ToString("D"),
            Type = "SolicitudRegistrada"
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: RabbitMqOptions.QueueName,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var connection = Interlocked.Exchange(ref _connection, null);
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        _connectionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = RabbitMqConnectionFactory.Create(_options.ConnectionString);
            _connection = await factory.CreateConnectionAsync("CreditosApp-publisher", cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}

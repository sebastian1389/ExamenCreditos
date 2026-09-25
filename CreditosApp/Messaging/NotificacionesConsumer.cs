using System.Text.Json;
using CreditosApp.Data;
using CreditosApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CreditosApp.Messaging;

public sealed class NotificacionesConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificacionesConsumer> _logger;

    public NotificacionesConsumer(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificacionesConsumer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ConsumerEnabled)
        {
            _logger.LogInformation("El consumidor de notificaciones está deshabilitado.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogError("RabbitMq__ConsumerEnabled está activo, pero RabbitMq__ConnectionString no está configurada.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error al consumir la cola de notificaciones.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var factory = RabbitMqConnectionFactory.Create(_options.ConnectionString);
        await using var connection = await factory.CreateConnectionAsync("CreditosApp-consumer", cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: RabbitMqOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                await GuardarNotificacionAsync(eventArgs.Body, cancellationToken);
                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No se pudo procesar el mensaje {MessageId}.", eventArgs.BasicProperties.MessageId);
                await channel.BasicNackAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: cancellationToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: RabbitMqOptions.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private async Task GuardarNotificacionAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var mensaje = JsonSerializer.Deserialize<SolicitudRegistradaMessage>(body.Span, JsonOptions);
        if (mensaje is null
            || mensaje.MessageId == Guid.Empty
            || mensaje.SolicitudId <= 0
            || string.IsNullOrWhiteSpace(mensaje.UsuarioId))
        {
            throw new InvalidDataException("El mensaje SolicitudRegistrada no contiene los datos requeridos.");
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var yaProcesada = await context.Notificaciones
            .AnyAsync(n => n.MessageId == mensaje.MessageId, cancellationToken);

        if (yaProcesada)
        {
            return;
        }

        context.Notificaciones.Add(new Notificacion
        {
            MessageId = mensaje.MessageId,
            SolicitudId = mensaje.SolicitudId,
            UsuarioId = mensaje.UsuarioId,
            Texto = $"La solicitud {mensaje.SolicitudId} fue registrada exitosamente.",
            FechaProcesamientoUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}

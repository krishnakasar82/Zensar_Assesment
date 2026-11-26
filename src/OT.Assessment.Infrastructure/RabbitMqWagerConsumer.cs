using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OT.Assessment.Application;
using OT.Assessment.Shared;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace OT.Assessment.Infrastructure;

public sealed class RabbitMqWagerConsumer : IWagerQueueConsumer
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ICasinoWagerRepository _repository;
    private readonly ILogger<RabbitMqWagerConsumer> _logger;

    public RabbitMqWagerConsumer(
        RabbitMqOptions options,
        ICasinoWagerRepository repository,
        ILogger<RabbitMqWagerConsumer> logger)
    {
        _options = options;
        _repository = repository;
        _logger = logger;
        _connectionFactory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = false,
            ConsumerDispatchConcurrency = 1
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeSessionAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (
                exception is RabbitMQClientException or IOException or SocketException or TimeoutException)
            {
                _logger.LogError(exception, "RabbitMQ consumer disconnected; retrying shortly.");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ConsumeSessionAsync(CancellationToken cancellationToken)
    {
        await using var connection =
            await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(true, true, null, 1),
            cancellationToken);

        await RabbitMqTopology.DeclareAsync(channel, _options, cancellationToken);
        await channel.BasicQosAsync(0, 16, false, cancellationToken);

        var channelClosed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            channelClosed.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) =>
            HandleDeliveryAsync(channel, delivery, cancellationToken);

        await channel.BasicConsumeAsync(
            _options.QueueName,
            autoAck: false,
            consumer,
            cancellationToken: cancellationToken);
        _logger.LogInformation("Consuming casino wagers from {QueueName}.", _options.QueueName);

        await Task.WhenAny(
            channelClosed.Task,
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessDeliveryAsync(channel, delivery, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not complete processing for delivery {DeliveryTag}; requeueing it.",
                delivery.DeliveryTag);
            await channel.BasicNackAsync(delivery.DeliveryTag, false, true, cancellationToken);
        }
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        CasinoWagerMessage wager;
        try
        {
            wager = JsonSerializer.Deserialize<CasinoWagerMessage>(delivery.Body.Span)
                ?? throw new JsonException("Wager message body was empty.");
            ValidateMessage(wager);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Rejecting an invalid wager message.");
            await RouteToDeadLetterAsync(
                channel,
                delivery,
                "invalid-message",
                cancellationToken);
            return;
        }

        try
        {
            await _repository.SaveAsync(wager, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to persist wager {WagerId}; scheduling retry or dead-lettering.",
                wager.WagerId);
            await RetryOrDeadLetterAsync(channel, delivery, exception, cancellationToken);
        }
    }

    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var retryCount = ReadRetryCount(delivery.BasicProperties.Headers);
        var retrying = retryCount < _options.MaxRetries;
        var headers = new Dictionary<string, object?>
        {
            ["x-retry-count"] = retryCount + 1,
            ["x-failure-reason"] = exception.GetType().Name
        };
        var properties = new BasicProperties
        {
            ContentType = delivery.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = delivery.BasicProperties.MessageId,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            retrying ? _options.RetryExchangeName : _options.DeadLetterExchangeName,
            retrying ? RabbitMqTopology.RetryRoutingKey : RabbitMqTopology.DeadLetterRoutingKey,
            mandatory: true,
            properties,
            delivery.Body,
            cancellationToken);
        await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);

        if (!retrying)
        {
            _logger.LogError(
                "Wager message {MessageId} exceeded {MaxRetries} retries and was dead-lettered.",
                delivery.BasicProperties.MessageId,
                _options.MaxRetries);
        }
    }

    private async Task RouteToDeadLetterAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        string reason,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-failure-reason"] = reason
        };
        var properties = new BasicProperties
        {
            ContentType = delivery.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = delivery.BasicProperties.MessageId,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            _options.DeadLetterExchangeName,
            RabbitMqTopology.DeadLetterRoutingKey,
            mandatory: true,
            properties,
            delivery.Body,
            cancellationToken);
        await channel.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-retry-count", out var value))
        {
            return 0;
        }

        return value switch
        {
            byte count => count,
            int count => count,
            long count when count is >= 0 and <= int.MaxValue => (int)count,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var count) => count,
            _ => 0
        };
    }

    private static void ValidateMessage(CasinoWagerMessage wager)
    {
        if (wager.WagerId == Guid.Empty
            || wager.AccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(wager.Username)
            || wager.Username.Length > 100
            || string.IsNullOrWhiteSpace(wager.Provider)
            || wager.Provider.Length > 200
            || string.IsNullOrWhiteSpace(wager.GameName)
            || wager.GameName.Length > 200
            || wager.Amount <= 0m
            || wager.Amount > 99999999999.99999999999999999m
            || wager.CreatedDateTime == default)
        {
            throw new JsonException("Wager message fields are invalid.");
        }
    }
}

using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OT.Assessment.Application;
using OT.Assessment.Shared;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace OT.Assessment.Infrastructure;

public sealed class RabbitMqWagerPublisher : IWagerPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _connectionFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqWagerPublisher> _logger;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqWagerPublisher(
        RabbitMqOptions options,
        ILogger<RabbitMqWagerPublisher> logger)
    {
        _options = options;
        _logger = logger;
        _connectionFactory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task PublishAsync(CasinoWagerMessage wager, CancellationToken cancellationToken)
    {
        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(wager);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = wager.WagerId.ToString()
            };

            await channel.BasicPublishAsync(
                _options.ExchangeName,
                RabbitMqTopology.RoutingKey,
                mandatory: true,
                properties,
                body,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is RabbitMQClientException or IOException or SocketException or TimeoutException)
        {
            await DisposeConnectionAsync();
            throw new BrokerUnavailableException(
                "The wager message could not be published to RabbitMQ.",
                exception);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _publishLock.WaitAsync();
        try
        {
            await DisposeConnectionAsync();
        }
        finally
        {
            _publishLock.Release();
            _publishLock.Dispose();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || !_connection.IsOpen)
        {
            await DisposeConnectionAsync();
            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        }

        if (_channel is null || !_channel.IsOpen)
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }

            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(true, true, null, null),
                cancellationToken);
            await RabbitMqTopology.DeclareAsync(_channel, _options, cancellationToken);
        }

        return _channel;
    }

    private async Task DisposeConnectionAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

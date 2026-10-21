using Microsoft.Extensions.Options;
using OT.Assessment.Common.Messages;
using OT.Assessment.Common.Settings;
using RabbitMQ.Client;
using System.Runtime;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;

namespace OT.Assessment.App.Services
{
    public class RabbitMqBetPublisher : IBetPublisher, IAsyncDisposable
    {
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqBetPublisher> _logger;
        private IConnection _connection;
        // several channels, each with its own lock: one channel sends one bet at a time (it waits for the broker's
        // confirm), so more channels means more bets in flight at once
        private IChannel[] _channels;
        private readonly SemaphoreSlim[] _channelLocks;
        private int _nextChannel;
        private readonly SemaphoreSlim _connectLock = new(1, 1);



        public RabbitMqBetPublisher(IOptions<RabbitMqSettings> options, ILogger<RabbitMqBetPublisher> logger)
        {
            _settings = options.Value;
            _logger = logger;
            _channelLocks = Enumerable.Range(0, Math.Max(1, _settings.PublishChannels))
                .Select(_ => new SemaphoreSlim(1, 1))
                .ToArray();
        }

        public async Task PublishAsync(
    CasinoBetMessage bet,
    CancellationToken cancellationToken = default)
        {
            await ConnectAsync(cancellationToken);

            var message = JsonSerializer.SerializeToUtf8Bytes(bet);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            // the array is replaced as a whole when the connection is rebuilt, so take one copy
            var channels = _channels;
            var slot = PublishSlot.Next(ref _nextChannel, channels.Length);

            await _channelLocks[slot].WaitAsync(cancellationToken);
            try
            {
                await channels[slot].BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _settings.QueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: message,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                _channelLocks[slot].Release();
            }
        }



        //private async Task ConnectAsync(CancellationToken cancellationToken)
        //{
        //    if (_connection != null && _connection.IsOpen &&
        //        _channel != null && _channel.IsOpen)
        //    {
        //        return;
        //    }

        //    var factory = new ConnectionFactory
        //    {
        //        HostName = _settings.HostName,
        //        Port = _settings.Port,
        //        UserName = _settings.UserName,
        //        Password = _settings.Password
        //    };

        //    if (_connection == null || !_connection.IsOpen)
        //    {
        //        _connection = await factory.CreateConnectionAsync(cancellationToken);
        //    }

        //    _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        //    await DeclareQueuesAsync(cancellationToken);
        //}

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (IsReady)
            {
                return;
            }

            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (IsReady)
                {
                    return;
                }

                if (_connection == null || !_connection.IsOpen)
                {
                    if (_connection != null)
                    {
                        _logger.LogWarning("The RabbitMQ connection was lost; reconnecting to {Host}:{Port}",
                            _settings.HostName, _settings.Port);
                    }

                    var factory = new ConnectionFactory
                    {
                        HostName = _settings.HostName,
                        Port = _settings.Port,
                        UserName = _settings.UserName,
                        Password = _settings.Password
                    };

                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                }

                //_channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);
                var channels = new IChannel[_channelLocks.Length];
                for (var i = 0; i < channels.Length; i++)
                {
                    channels[i] = await _connection.CreateChannelAsync(channelOptions, cancellationToken);
                }

                // the queues are declared before any request can use the channels
                await DeclareQueuesAsync(channels[0], cancellationToken);

                var oldChannels = _channels;
                _channels = channels;
                await CloseAsync(oldChannels);

                _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}, queue {Queue} is ready ({Channels} publish channels)",
                    _settings.HostName, _settings.Port, _settings.QueueName, channels.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not connect to RabbitMQ at {Host}:{Port}", _settings.HostName, _settings.Port);
                throw;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private bool IsReady => _connection != null && _connection.IsOpen &&
                                _channels != null && _channels.All(channel => channel.IsOpen);

        private async Task DeclareQueuesAsync(IChannel channel, CancellationToken cancellationToken)
        {
            await channel.QueueDeclareAsync(
                queue: _settings.DeadLetterQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var arguments = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _settings.DeadLetterQueueName
            };

            await channel.QueueDeclareAsync(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments,
                cancellationToken: cancellationToken);
        }

        private static async Task CloseAsync(IChannel[] channels)
        {
            if (channels == null)
            {
                return;
            }

            foreach (var channel in channels)
            {
                try
                {
                    await channel.DisposeAsync();
                }
                catch
                {
                    // a channel of a lost connection is already closed
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync(_channels);

            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }

            _logger.LogInformation("RabbitMQ publisher closed");
        }
    }
}


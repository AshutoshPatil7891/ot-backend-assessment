using Microsoft.Extensions.Options;
using OT.Assessment.Common.msg;
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
        private IChannel _channel;
        private readonly SemaphoreSlim _connectLock = new(1, 1);
        private readonly SemaphoreSlim _publishLock = new(1, 1);



        public RabbitMqBetPublisher(IOptions<RabbitMqSettings> options, ILogger<RabbitMqBetPublisher> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task PublishAsync(
    CasinoBetMesg bet,
    CancellationToken cancellationToken = default)
        {
            await ConnectAsync(cancellationToken);

            var message = JsonSerializer.SerializeToUtf8Bytes(bet);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _publishLock.WaitAsync(cancellationToken);
            try
            {
                await _channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _settings.QueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: message,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                _publishLock.Release();
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
            if (_connection != null && _connection.IsOpen &&
                _channel != null && _channel.IsOpen)
            {
                return;
            }

            await _connectLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection != null && _connection.IsOpen &&
                    _channel != null && _channel.IsOpen)
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
                _channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);



                await DeclareQueuesAsync(cancellationToken);

                _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}, queue {Queue} is ready",
                    _settings.HostName, _settings.Port, _settings.QueueName);
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

        private async Task DeclareQueuesAsync(CancellationToken cancellationToken)
        {
            await _channel.QueueDeclareAsync(
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

            await _channel.QueueDeclareAsync(
                queue: _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments,
                cancellationToken: cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
            {
                await _channel.DisposeAsync();
            }

            if (_connection != null)
            {
                await _connection.DisposeAsync();
            }

            _logger.LogInformation("RabbitMQ publisher closed");
        }
    }
}


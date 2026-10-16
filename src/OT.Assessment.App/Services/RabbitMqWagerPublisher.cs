using System.Text.Json;
using Microsoft.Extensions.Options;
using OT.Assessment.Common.msg;
using OT.Assessment.Common.Settings;
using RabbitMQ.Client;

namespace OT.Assessment.App.Services
{
    public class RabbitMqWagerPublisher : IWagerPublisher, IAsyncDisposable
    {
        private readonly RabbitMqSettings _settings;
        private IConnection _connection;
        private IChannel _channel;
        private readonly SemaphoreSlim _connectLock = new(1, 1);


        public RabbitMqWagerPublisher(IOptions<RabbitMqSettings> options)
        {
            _settings = options.Value;
        }

        public async Task PublishAsync(
            CasinoWagerMesg wager,
            CancellationToken cancellationToken = default)
        {
            await ConnectAsync(cancellationToken);

            var message = JsonSerializer.SerializeToUtf8Bytes(wager);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json"
            };

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _settings.QueueName,
                mandatory: false,
                basicProperties: properties,
                body: message,
                cancellationToken: cancellationToken);
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
                    var factory = new ConnectionFactory
                    {
                        HostName = _settings.HostName,
                        Port = _settings.Port,
                        UserName = _settings.UserName,
                        Password = _settings.Password
                    };

                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                }

                _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                await DeclareQueuesAsync(cancellationToken);
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
        }
    }
}


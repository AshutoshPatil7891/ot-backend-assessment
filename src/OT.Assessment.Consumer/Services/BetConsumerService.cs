using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OT.Assessment.Common.msg;
using OT.Assessment.Common.Settings;
using OT.Assessment.Consumer.Monitoring;
using OT.Assessment.Consumer.Settings;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace OT.Assessment.Consumer.Services
{
    /// <summary>
    /// Reads bets from the RabbitMQ queue, saves them to SQL Server in batches, and
    /// acknowledges a message only after it has been saved. A message that can never be
    /// saved is rejected, so RabbitMQ moves it to the dead-letter queue.
    /// </summary>
    public class BetConsumerService : BackgroundService
    {
        // SQL Server error numbers that mean "this data cannot be stored" (retrying will not help).
        private static readonly int[] DataErrorNumbers =
            { 220, 232, 242, 245, 515, 547, 2601, 2627, 2628, 8114, 8115, 8152 };

        private readonly RabbitMqSettings _rabbit;
        private readonly ConsumerSettings _settings;
        private readonly IBetWriter _writer;
        private readonly ILogger<BetConsumerService> _logger;
        private readonly Channel<PendingBet> _buffer = Channel.CreateUnbounded<PendingBet>();

        private IConnection _connection;
        private IChannel _channel;

        public BetConsumerService(
            IOptions<RabbitMqSettings> rabbit,
            IOptions<ConsumerSettings> settings,
            IBetWriter writer,
            ILogger<BetConsumerService> logger)
        {
            _rabbit = rabbit.Value;
            _settings = settings.Value;
            _writer = writer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ConnectAsync(stoppingToken);
            await StartConsumingAsync(stoppingToken);
            await RunBatchLoopAsync(stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Consumer stopping; {Waiting} bets still waiting in memory", _buffer.Reader.Count);

            await base.StopAsync(cancellationToken);

            if (_channel != null)
            {
                await _channel.CloseAsync(cancellationToken);
            }

            if (_connection != null)
            {
                await _connection.CloseAsync(cancellationToken);
            }
        }

        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbit.HostName,
                Port = _rabbit.Port,
                UserName = _rabbit.UserName,
                Password = _rabbit.Password
            };

            while (true)
            {
                try
                {
                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "RabbitMQ is not reachable; trying again in 5 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }

            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.BasicQosAsync(0, (ushort)_settings.PrefetchCount, false, cancellationToken);

            // Same queue arguments as the API, otherwise RabbitMQ refuses with PRECONDITION_FAILED.
            await _channel.QueueDeclareAsync(
                _rabbit.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: cancellationToken);

            var arguments = new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = _rabbit.DeadLetterQueueName
            };

            await _channel.QueueDeclareAsync(
                _rabbit.QueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: arguments, cancellationToken: cancellationToken);

            _logger.LogInformation("Connected to RabbitMQ, reading queue {Queue}", _rabbit.QueueName);
        }

        private async Task StartConsumingAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var bet = JsonSerializer.Deserialize<CasinoBetMesg>(ea.Body.Span);

                    if (bet == null)
                    {
                        throw new JsonException("The message is empty.");
                    }

                    await _buffer.Writer.WriteAsync(new PendingBet(ea.DeliveryTag, bet), stoppingToken);
                    ConsumerMetrics.BufferedBets.Set(_buffer.Reader.Count);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Message {Tag} is not a valid bet; sending it to the dead-letter queue", ea.DeliveryTag);
                    ConsumerMetrics.DeadLettered.Inc();
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, false, stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(_rabbit.QueueName, autoAck: false, consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation(
                "Consuming queue {Queue}: batch size {BatchSize}, flush every {FlushMs} ms, prefetch {Prefetch}",
                _rabbit.QueueName, _settings.BatchSize, _settings.FlushIntervalMs, _settings.PrefetchCount);
        }

        private async Task RunBatchLoopAsync(CancellationToken stoppingToken)
        {
            var reader = _buffer.Reader;
            var batch = new List<PendingBet>(_settings.BatchSize);

            while (await reader.WaitToReadAsync(stoppingToken))
            {
                // The flush timer starts when the first message of the batch arrives.
                using var flushTimer = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushTimer.CancelAfter(_settings.FlushIntervalMs);

                try
                {
                    while (batch.Count < _settings.BatchSize)
                    {
                        if (reader.TryRead(out var item))
                        {
                            batch.Add(item);
                        }
                        else if (!await reader.WaitToReadAsync(flushTimer.Token))
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // The flush interval has passed; save what we have.
                }

                await FlushAsync(batch, stoppingToken);
                batch.Clear();
            }
        }

        private async Task FlushAsync(List<PendingBet> batch, CancellationToken cancellationToken)
        {
            if (batch.Count == 0)
            {
                return;
            }

            var lastTag = batch[^1].DeliveryTag;
            var timer = Stopwatch.StartNew();

            try
            {
                var inserted = await WriteWithRetryAsync(batch.Select(b => b.Bet).ToList(), cancellationToken);
                var skipped = batch.Count - inserted;

                // All earlier delivery tags are already settled, so one multiple-ack covers the batch.
                await _channel.BasicAckAsync(lastTag, true, cancellationToken);
                _logger.LogInformation(
                    "Saved {Inserted} of {Count} bets in {ElapsedMs} ms ({Skipped} repeats skipped, {Waiting} more waiting in memory)",
                    inserted, batch.Count, timer.ElapsedMilliseconds, skipped, _buffer.Reader.Count);
                ConsumerMetrics.BetsSaved.Inc(inserted);
                ConsumerMetrics.BetsSkipped.Inc(skipped);
                ConsumerMetrics.Batches.Inc();
                ConsumerMetrics.BatchDuration.Observe(timer.Elapsed.TotalSeconds);
                ConsumerMetrics.BufferedBets.Set(_buffer.Reader.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && IsDataError(ex))
            {
                _logger.LogWarning(ex, "The database rejected a batch of {Count} bets; saving them one by one", batch.Count);
                await FlushOneByOneAsync(batch, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not save {Count} bets; returning them to the queue", batch.Count);
                ConsumerMetrics.Requeued.Inc(batch.Count);
                await _channel.BasicNackAsync(lastTag, true, true, cancellationToken);
            }
        }

        private async Task<int> WriteWithRetryAsync(IReadOnlyList<CasinoBetMesg> bets, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await _writer.WriteBatchAsync(bets, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException
                                           && !IsDataError(ex)
                                           && attempt < _settings.MaxRetries)
                {
                    _logger.LogWarning(ex, "Saving failed (attempt {Attempt} of {Max}); trying again", attempt, _settings.MaxRetries);
                    ConsumerMetrics.SaveRetries.Inc();
                    await Task.Delay(_settings.RetryDelayMs * attempt, cancellationToken);
                }
            }
        }

        private async Task FlushOneByOneAsync(List<PendingBet> batch, CancellationToken cancellationToken)
        {
            foreach (var item in batch)
            {
                try
                {
                    var inserted = await _writer.WriteBatchAsync(new[] { item.Bet }, cancellationToken);
                    await _channel.BasicAckAsync(item.DeliveryTag, false, cancellationToken);
                    ConsumerMetrics.BetsSaved.Inc(inserted);
                    ConsumerMetrics.BetsSkipped.Inc(1 - inserted);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && IsDataError(ex))
                {
                    _logger.LogError(ex, "Bet {BetId} cannot be saved; sending it to the dead-letter queue", item.Bet.BetId);
                    ConsumerMetrics.DeadLettered.Inc();
                    await _channel.BasicNackAsync(item.DeliveryTag, false, false, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Bet {BetId} could not be saved right now; returning it to the queue", item.Bet.BetId);
                    ConsumerMetrics.Requeued.Inc();
                    await _channel.BasicNackAsync(item.DeliveryTag, false, true, cancellationToken);
                }
            }
        }

        private static bool IsDataError(Exception ex)
        {
            return ex switch
            {
                SqlException sql => sql.Errors.Cast<SqlError>().Any(e => DataErrorNumbers.Contains(e.Number)),
                ArgumentException or InvalidCastException or FormatException or OverflowException => true,
                _ => false
            };
        }

        private record PendingBet(ulong DeliveryTag, CasinoBetMesg Bet);
    }
}

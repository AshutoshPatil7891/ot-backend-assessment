using Microsoft.Extensions.Configuration;
using OT.Assessment.Common.Logging;
using OT.Assessment.Common.Settings;
using OT.Assessment.Consumer.Services;
using OT.Assessment.Consumer.Settings;
using Serilog;


// console + daily log files in <project folder>/logs/info, warning and error
Log.Logger = FileLogging.Configure(new LoggerConfiguration(), Path.Combine(Directory.GetCurrentDirectory(), "logs")).CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()

    .ConfigureAppConfiguration(config =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
    })
    .ConfigureServices((context, services) =>
    {
        //configure services
        services.Configure<RabbitMqSettings>(context.Configuration.GetSection("RabbitMq"));
        services.Configure<ConsumerSettings>(context.Configuration.GetSection("Consumer"));
        services.AddSingleton<IBetWriter, SqlBetWriter>();
        services.AddHostedService<BetConsumerService>();


    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Application started {time:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

await host.RunAsync();

logger.LogInformation("Application ended {time:yyyy-MM-dd HH:mm:ss}", DateTime.Now);
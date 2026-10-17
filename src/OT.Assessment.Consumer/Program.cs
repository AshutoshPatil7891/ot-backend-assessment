using Microsoft.Extensions.Configuration;
using OT.Assessment.Common.Settings;
using OT.Assessment.Consumer.Services;
using OT.Assessment.Consumer.Settings;


var host = Host.CreateDefaultBuilder(args)

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
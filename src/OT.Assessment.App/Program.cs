using System.Reflection;
using OT.Assessment.App.Services;
using OT.Assessment.Common.Logging;
using OT.Assessment.Common.Settings;
using Serilog;



var builder = WebApplication.CreateBuilder(args);

// console + daily log files in <project folder>/logs/info, warning and error
Log.Logger = FileLogging.Configure(new LoggerConfiguration(), Path.Combine(Directory.GetCurrentDirectory(), "logs")).CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddControllers();

builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IBetPublisher, RabbitMqBetPublisher>();
builder.Services.AddSingleton<IPlayerReader, SqlPlayerReader>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckl
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(opts =>
    {
        opts.EnableTryItOutByDefault();
        opts.DocumentTitle = "OT Assessment App";
        opts.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

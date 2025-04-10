using NLog;
using NLog.Web;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings()
    .GetCurrentClassLogger();

try
{
    logger.Debug("ShippingService starting...");

    var builder = WebApplication.CreateBuilder(args);

    // Tilføj NLog som logger
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
    builder.Host.UseNLog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    //app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();
    logger.Info("🔥 Test log from shipping-service");

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "ShippingService stopped because of exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}

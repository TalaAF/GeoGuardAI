using AeroCompliance.Infrastructure;
using AeroCompliance.Infrastructure.Notifications;
using AeroCompliance.Infrastructure.Persistence;
using AeroCompliance.ProcessingService;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<AeroComplianceDbContext>(DependencyInjection.DatabaseResourceName,
    configureDbContextOptions: options => options.UseAeroComplianceNpgsql());
builder.AddRabbitMQClient("messaging");
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<AnalysisResultConsumer>();
await builder.Build().RunAsync();

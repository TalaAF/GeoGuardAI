using AeroCompliance.ApiService.Authentication;
using AeroCompliance.ApiService.Authorization;
using AeroCompliance.ApiService.Services;
using AeroCompliance.Domain.Entities;
using AeroCompliance.Infrastructure;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using AeroCompliance.Infrastructure.Notifications;
using AeroCompliance.Infrastructure.Storage;
using Microsoft.AspNetCore.Http.Features;

// QuestPDF is source-available under the Community License (NOT MIT); its free tier is
// eligibility-based and must be re-checked before any production municipal deployment.
// Declared once at startup, before any report is rendered.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, resilience.
builder.AddServiceDefaults();

// --- Data access -----------------------------------------------------------
// Aspire wires the connection string from the AppHost ("aerocompliancedb"); we layer
// on the PostGIS/NetTopologySuite + migrations-assembly options.
builder.AddNpgsqlDbContext<AeroComplianceDbContext>(
    connectionName: DependencyInjection.DatabaseResourceName,
    configureDbContextOptions: options => options.UseAeroComplianceNpgsql());

// --- Messaging -------------------------------------------------------------
// RabbitMQ client is registered from the "messaging" Aspire resource so controllers/handlers
// can publish or consume once the business logic lands.
builder.AddRabbitMQClient(connectionName: "messaging");

// --- AuthN / AuthZ ---------------------------------------------------------
builder.Services.AddAeroComplianceJwtAuth(builder.Configuration, builder.Environment);
builder.Services.AddAeroComplianceAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IObjectStorage, MinioObjectStorage>();
builder.Services.AddHostedService<ProcessingOutboxDispatcher>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit =
    builder.Configuration.GetValue<long?>("Uploads:MaxBytes") ?? 262_144_000);

// Password hashing (Identity's PasswordHasher only — not the full Identity stack) + auth services.
builder.Services.AddSingleton<PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<DevUserSeeder>();
builder.Services.AddScoped<DemoDataSeeder>();

// --- MVC + OpenAPI ---------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AeroCompliance API",
        Version = "v1",
        Description = "Drone-based municipal compliance platform core API."
    });

    // Wire the Bearer scheme into Swagger so the "Authorize" button works.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token (without the 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

var app = builder.Build();

// Development-only GIS import CLI. Runs instead of the web host when invoked as
//   dotnet run --project src/AeroCompliance.ApiService -- import-gis <dir> [--dry-run]
// so it never affects normal service startup.
if (GisImportCommand.Matches(args))
{
    return await GisImportCommand.RunAsync(args, app.Services, app.Environment);
}

// Dev convenience: apply migrations on startup so `dotnet run` yields a ready database.
// ApiService is the sole migration owner (IngestionService doesn't touch the schema). In
// non-dev environments, run migrations as a deliberate deploy step instead — and pre-provision
// the PostGIS extension where the app user lacks CREATE EXTENSION rights.
var applyMigrations = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
var seedDemo = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("DemoSeed:Enabled");
if (applyMigrations)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();

        if (seedDemo)
        {
            // Local/demo-only accounts and synthetic records. Production deployments must leave
            // DemoSeed:Enabled false and manage identities/data explicitly.
            var userSeeder = scope.ServiceProvider.GetRequiredService<DevUserSeeder>();
            await userSeeder.SeedAsync();
            var dataSeeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            await dataSeeder.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        // Aspire's WaitFor makes the connection *likely* ready, but not guaranteed — a slow
        // Postgres container start can still surface a transient Npgsql failure here. Log it
        // clearly (rather than letting a raw exception crash startup opaquely) before rethrowing.
        logger.LogError(
            ex,
            "Startup database migration/seeding failed. If Postgres is still starting, restarting " +
            "the ApiService usually resolves it; otherwise check the connection string and that the " +
            "PostGIS-enabled Postgres container is healthy.");
        throw;
    }
}

// Aspire health/liveness endpoints (/health, /alive).
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "AeroCompliance API v1"));
}

if (builder.Configuration.GetValue("HttpsRedirection:Enabled", true))
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
return 0;

// Exposes the generated top-level Program type to WebApplicationFactory integration tests.
public partial class Program;

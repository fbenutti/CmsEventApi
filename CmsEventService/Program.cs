using CmsEventService.Authentication;
using CmsEventService.Data;
using CmsEventService.Options;
using CmsEventService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
var databaseOptions = builder.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();

builder.Services.AddDbContext<CmsDbContext>(options =>
    options.UseSqlite(databaseOptions.WriterConnectionString));

builder.Services.AddDbContextFactory<CmsDbContext>(options =>
    options.UseSqlite(databaseOptions.ReaderConnectionString),
    ServiceLifetime.Scoped);

builder.Services
    .AddAuthentication()
    .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
        BasicAuthenticationDefaults.CmsScheme,
        _ => { })
    .AddScheme<BasicAuthenticationOptions, BasicAuthenticationHandler>(
        BasicAuthenticationDefaults.ApiScheme,
        _ => { });

builder.Services.Configure<BasicAuthenticationOptions>(
    BasicAuthenticationDefaults.CmsScheme,
    builder.Configuration.GetSection("Authentication:Cms"));
builder.Services.Configure<BasicAuthenticationOptions>(
    BasicAuthenticationDefaults.ApiScheme,
    builder.Configuration.GetSection("Authentication:Api"));

builder.Services.AddAuthorization();
builder.Services.AddScoped<ICmsEventProcessor, CmsEventProcessor>();
builder.Services.AddScoped<IEntityAdministrationService, EntityAdministrationService>();
builder.Services.AddScoped<IEntityQueryService, EntityQueryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        Description = "Basic Authentication using the credentials from appsettings.json."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Basic", document, null)] = []
    });
});
builder.Services.AddControllers();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", async (CmsDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "Healthy" })
        : Results.Problem("Database is not reachable.", statusCode: StatusCodes.Status503ServiceUnavailable);
})
.AllowAnonymous()
.WithName("HealthCheck")
.WithTags("Health");

app.MapControllers();

app.Run();

public partial class Program;

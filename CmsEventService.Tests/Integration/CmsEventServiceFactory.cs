using CmsEventService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CmsEventService.Tests.Integration;

public sealed class CmsEventServiceFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"cms-events-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Cms:Users:0:Username"] = "cmsPipeline01",
                ["Authentication:Cms:Users:0:Password"] = "3e8dc83f-4b8f-48c7-a2d4-44f89f476b65",
                ["Authentication:Cms:Users:0:UserId"] = "cms-pipeline",
                ["Authentication:Cms:Users:0:Role"] = "Cms",
                ["Authentication:Api:Users:0:Username"] = "entityReader1",
                ["Authentication:Api:Users:0:Password"] = "8e221201-a1cd-4f57-89c7-04d517651625",
                ["Authentication:Api:Users:0:UserId"] = "reader-user",
                ["Authentication:Api:Users:0:Role"] = "Reader",
                ["Authentication:Api:Users:1:Username"] = "entityAdmin01",
                ["Authentication:Api:Users:1:Password"] = "4f21956d-918a-4199-9787-e4bf9956363c",
                ["Authentication:Api:Users:1:UserId"] = "admin-user",
                ["Authentication:Api:Users:1:Role"] = "Admin"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<CmsDbContext>();
            services.RemoveAll<DbContextOptions<CmsDbContext>>();
            services.RemoveAll<IDbContextFactory<CmsDbContext>>();

            services.AddDbContext<CmsDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            services.AddDbContextFactory<CmsDbContext>(
                options => options.UseSqlite($"Data Source={_databasePath}"),
                ServiceLifetime.Scoped);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // SQLite can keep the test file locked briefly on Windows after host disposal.
        }
    }
}

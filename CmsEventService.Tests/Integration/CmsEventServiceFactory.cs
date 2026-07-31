using CmsEventService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

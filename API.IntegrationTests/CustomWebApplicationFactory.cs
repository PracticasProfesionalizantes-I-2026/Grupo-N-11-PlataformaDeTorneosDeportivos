using System.Linq;
using DataAccess.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace API.IntegrationTests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        private SqliteConnection? _connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext)).ToList();

                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }

                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                var sp = services.BuildServiceProvider();
                using (var scope = sp.CreateScope())
                {
                    var scopedServices = scope.ServiceProvider;
                    var db = scopedServices.GetRequiredService<AppDbContext>();
                    db.Database.EnsureCreated();
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _connection?.Dispose();
        }
    }
}

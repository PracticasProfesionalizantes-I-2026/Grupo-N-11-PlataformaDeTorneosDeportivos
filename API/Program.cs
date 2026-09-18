using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using API.Middlewares;
using DataAccess.Data;
using DataAccess.Repositories;
using BusinessLogic.Services;

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // DbContext
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=torneos.db"));

            // DI Repositories
            builder.Services.AddScoped<ITorneoRepository, TorneoRepository>();
            builder.Services.AddScoped<IEquipoRepository, EquipoRepository>();
            builder.Services.AddScoped<IJugadorRepository, JugadorRepository>();

            // DI Services
            builder.Services.AddScoped<ITorneoService, TorneoService>();
            builder.Services.AddScoped<IEquipoService, EquipoService>();
            builder.Services.AddScoped<IJugadorService, JugadorService>();

            var app = builder.Build();

            // Seed Data
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<AppDbContext>();
                DbInitializer.Initialize(context);
            }

            // Exception Middleware
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}

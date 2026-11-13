using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using API.Middlewares;
using DataAccess.Data;
using DataAccess.Repositories;
using BusinessLogic.Services;
using BusinessLogic.Security;

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                    {
                        var firstError = context.ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => !string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.ErrorMessage : e.Exception?.Message)
                            .FirstOrDefault(msg => !string.IsNullOrWhiteSpace(msg))
                            ?? "Los datos enviados en la petición son inválidos o tienen un formato incorrecto.";

                        return new BadRequestObjectResult(new { error = firstError });
                    };
                });

            // Authentication & Authorization
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
                ?? builder.Configuration["Jwt:SecretKey"]
                ?? "ClaveSecretaSuperSeguraParaTorneosDeportivos2026";
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            builder.Services.AddAuthorization();

            // DbContext
            var connectionString = Environment.GetEnvironmentVariable("TORNEOS_CONNECTION_STRING")
                ?? builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=torneos.db";

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString));

            // DI Repositories
            builder.Services.AddScoped<ITorneoRepository, TorneoRepository>();
            builder.Services.AddScoped<IEquipoRepository, EquipoRepository>();
            builder.Services.AddScoped<IJugadorRepository, JugadorRepository>();
            builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddScoped<IPartidoRepository, PartidoRepository>();
            builder.Services.AddScoped<IIncidenciaRepository, IncidenciaRepository>();
            builder.Services.AddScoped<IPublicidadRepository, PublicidadRepository>();
            builder.Services.AddScoped<ITablaPosicionRepository, TablaPosicionRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // DI Services
            builder.Services.AddScoped<IPasswordHasher, BusinessLogic.Security.PasswordHasher>();
            builder.Services.AddScoped<ICuentaRecaudadoraService, CuentaRecaudadoraService>();
            builder.Services.AddScoped<ITorneoService, TorneoService>();
            builder.Services.AddScoped<IEquipoService, EquipoService>();
            builder.Services.AddScoped<IJugadorService, JugadorService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IUsuarioService, UsuarioService>();
            builder.Services.AddScoped<IFixtureService, FixtureService>();
            builder.Services.AddScoped<IInscripcionService, InscripcionService>();
            builder.Services.AddScoped<IVeedorService, VeedorService>();
            builder.Services.AddScoped<IPartidoService, PartidoService>();
            builder.Services.AddScoped<IIncidenciaService, IncidenciaService>();
            builder.Services.AddScoped<IPublicidadService, PublicidadService>();

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

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}

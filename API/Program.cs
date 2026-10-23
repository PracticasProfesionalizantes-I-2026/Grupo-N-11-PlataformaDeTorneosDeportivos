using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
            builder.Services.AddControllers();

            // Authentication & Authorization
            var secretKey = builder.Configuration["Jwt:SecretKey"] ?? "ClaveSecretaSuperSeguraParaTorneosDeportivos2026";
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
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite("Data Source=torneos.db"));

            // DI Repositories
            builder.Services.AddScoped<ITorneoRepository, TorneoRepository>();
            builder.Services.AddScoped<IEquipoRepository, EquipoRepository>();
            builder.Services.AddScoped<IJugadorRepository, JugadorRepository>();
            builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddScoped<IPartidoRepository, PartidoRepository>();
            builder.Services.AddScoped<IIncidenciaRepository, IncidenciaRepository>();
            builder.Services.AddScoped<IPublicidadRepository, PublicidadRepository>();

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

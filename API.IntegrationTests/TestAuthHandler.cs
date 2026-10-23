using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace API.IntegrationTests
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string DefaultScheme = "TestScheme";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Anonymous"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // Si la petición viene con un token Bearer real, validamos el token real
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var authStr = authHeader.ToString();
                if (authStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var tokenStr = authStr.Substring("Bearer ".Length).Trim();
                    var tokenHandler = new JwtSecurityTokenHandler();
                    if (tokenHandler.CanReadToken(tokenStr))
                    {
                        var secretKey = "ClaveSecretaSuperSeguraParaTorneosDeportivos2026";
                        var validationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ClockSkew = TimeSpan.Zero
                        };

                        try
                        {
                            var principal = tokenHandler.ValidateToken(tokenStr, validationParameters, out _);
                            var ticket = new AuthenticationTicket(principal, DefaultScheme);
                            return Task.FromResult(AuthenticateResult.Success(ticket));
                        }
                        catch (Exception ex)
                        {
                            return Task.FromResult(AuthenticateResult.Fail(ex));
                        }
                    }
                }
            }

            // Fallback para tests de integración existentes sin cabecera de autenticación
            var roleHeader = Request.Headers["X-Test-Role"].ToString();
            var roles = string.IsNullOrWhiteSpace(roleHeader)
                ? new[] { "Organizador", "Administrador", "Capitan", "Veedor" }
                : roleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var userIdHeader = Request.Headers["X-Test-UserId"].ToString();
            var userId = string.IsNullOrWhiteSpace(userIdHeader)
                ? "00000000-0000-0000-0000-000000000001"
                : userIdHeader;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Email, "testuser@torneos.com")
            };

            foreach (var r in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }

            var identity = new ClaimsIdentity(claims, DefaultScheme);
            var defaultPrincipal = new ClaimsPrincipal(identity);
            var defaultTicket = new AuthenticationTicket(defaultPrincipal, DefaultScheme);

            return Task.FromResult(AuthenticateResult.Success(defaultTicket));
        }
    }
}

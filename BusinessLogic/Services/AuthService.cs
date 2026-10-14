using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Entities;
using DataAccess.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shared.DTOs;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUsuarioRepository usuarioRepository,
            IPasswordHasher passwordHasher,
            IConfiguration configuration)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        public async Task<LoginResponseDTO> LoginAsync(LoginRequestDTO dto)
        {
            // Paso 3 del CU-02: Buscar usuario por Email o DNI
            var usuario = await _usuarioRepository.GetByEmailOrDniAsync(dto.EmailOrDni);

            // Flujo Alternativo 3a: Credenciales incorrectas si el usuario no existe
            if (usuario == null)
            {
                throw new UnauthorizedException("Usuario o contraseña incorrectos.");
            }

            // Validar hash de la contraseña
            bool passwordValida = _passwordHasher.VerifyPassword(dto.Password, usuario.PasswordHash);
            if (!passwordValida)
            {
                throw new UnauthorizedException("Usuario o contraseña incorrectos.");
            }

            // Flujo Alternativo 4a: Cuenta inactiva o bloqueada
            if (!usuario.Activo)
            {
                throw new ForbiddenException("Cuenta no verificada o inhabilitada. Verifique su correo o contacte al administrador.");
            }

            // Flujo Principal: Generar Token JWT (HTTP 200)
            var expiration = DateTime.UtcNow.AddHours(4);
            var token = GenerarJwtToken(usuario, expiration);

            return new LoginResponseDTO
            {
                Token = token,
                Expiration = expiration,
                UsuarioId = usuario.Id,
                NombreCompleto = $"{usuario.Nombre} {usuario.Apellido}".Trim(),
                Rol = usuario.Rol.ToString()
            };
        }

        private string GenerarJwtToken(Usuario usuario, DateTime expiration)
        {
            var secretKey = _configuration["Jwt:SecretKey"] ?? "ClaveSecretaSuperSeguraParaTorneosDeportivos2026";
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var symmetricKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol.ToString())
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

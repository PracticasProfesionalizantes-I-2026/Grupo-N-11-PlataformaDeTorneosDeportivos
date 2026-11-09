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
            if (dto == null || string.IsNullOrWhiteSpace(dto.EmailOrDni) || string.IsNullOrWhiteSpace(dto.Password))
            {
                throw new ValidationException("El correo/DNI y la contraseña son obligatorios.");
            }

            var credential = dto.EmailOrDni.Trim();
            if (credential.Length > 150 || credential.Contains('<') || credential.Contains('>') || dto.Password.Length > 128)
            {
                throw new ValidationException("Las credenciales ingresadas exceden la longitud máxima permitida o contienen caracteres inválidos.");
            }

            bool isEmail = credential.Contains('@');
            if (isEmail)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(credential, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    throw new ValidationException("El formato del correo electrónico ingresado no es válido.");
                }
            }
            else
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(credential, @"^\d{7,8}$"))
                {
                    throw new ValidationException("El DNI ingresado debe contener exactamente 7 u 8 dígitos numéricos.");
                }
            }

            // Paso 3 del CU-02: Buscar usuario por Email o DNI
            var usuario = await _usuarioRepository.GetByEmailOrDniAsync(credential);

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
                Rol = usuario.Rol.ToString(),
                RequiereCambioPassword = usuario.RequiereCambioPassword
            };
        }

        public async Task<RecuperarPasswordResponseDTO> RecoverPasswordAsync(RecuperarPasswordRequestDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
            {
                throw new ValidationException("El correo electrónico es obligatorio.");
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            if (normalizedEmail.Length > 150 || normalizedEmail.Contains('<') || normalizedEmail.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                throw new ValidationException("El formato del correo electrónico no es válido.");
            }

            // Flujo Alternativo 3a: Silencioso si no existe (seguridad)
            var usuario = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuario == null)
            {
                return new RecuperarPasswordResponseDTO
                {
                    Mensaje = "Si el correo existe, se enviará un enlace de recuperación."
                };
            }

            // Flujo Principal: Generar token de recuperación con expiración a 24 hs
            var resetToken = Guid.NewGuid().ToString("N");
            usuario.PasswordResetToken = resetToken;
            usuario.PasswordResetTokenExpiration = DateTime.UtcNow.AddHours(24);

            await _usuarioRepository.UpdateAsync(usuario);

            return new RecuperarPasswordResponseDTO
            {
                Mensaje = "Si el correo existe, se enviará un enlace de recuperación.",
                Token = resetToken
            };
        }

        public async Task<ResetPasswordResponseDTO> ResetPasswordAsync(ResetPasswordRequestDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                throw new ValidationException("El token y la nueva contraseña son obligatorios.");
            }

            if (dto.NewPassword.Length < 8 || dto.NewPassword.Length > 128 ||
                !dto.NewPassword.Any(char.IsLetter) || !dto.NewPassword.Any(char.IsDigit))
            {
                throw new ValidationException("La nueva contraseña debe tener entre 8 y 128 caracteres e incluir letras y números.");
            }

            var token = dto.Token.Trim();
            if (token.Length > 150 || token.Contains('<') || token.Contains('>'))
            {
                throw new ValidationException("El formato del token de recuperación no es válido.");
            }

            // Flujo Alternativo 7a: Token Expirado o Usado (HTTP 400 Bad Request)
            var usuario = await _usuarioRepository.GetByPasswordResetTokenAsync(token);
            if (usuario == null ||
                !usuario.PasswordResetTokenExpiration.HasValue ||
                usuario.PasswordResetTokenExpiration.Value < DateTime.UtcNow)
            {
                throw new ValidationException("El enlace de recuperación ha expirado, intenta solicitar uno nuevo.");
            }

            // Flujo Principal: Actualizar contraseña y revocar el token
            usuario.PasswordHash = _passwordHasher.HashPassword(dto.NewPassword);
            usuario.PasswordResetToken = null;
            usuario.PasswordResetTokenExpiration = null;
            usuario.RequiereCambioPassword = false;

            await _usuarioRepository.UpdateAsync(usuario);

            return new ResetPasswordResponseDTO
            {
                Mensaje = "Contraseña restablecida exitosamente."
            };
        }

        public async Task<CambiarPasswordResponseDTO> CambiarPasswordInicialAsync(CambiarPasswordInicialDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos de cambio de contraseña son obligatorios.");
            }

            var credential = dto.EmailOrDni?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(credential) || string.IsNullOrWhiteSpace(dto.PasswordActual) || string.IsNullOrWhiteSpace(dto.NuevaPassword))
            {
                throw new ValidationException("El correo/DNI, la contraseña actual y la nueva contraseña son obligatorios.");
            }

            if (dto.PasswordActual == dto.NuevaPassword)
            {
                throw new ValidationException("La nueva contraseña no puede ser idéntica a la contraseña provisoria o actual.");
            }

            if (dto.NuevaPassword.Length < 8 || dto.NuevaPassword.Length > 128 ||
                !dto.NuevaPassword.Any(char.IsLetter) || !dto.NuevaPassword.Any(char.IsDigit))
            {
                throw new ValidationException("La nueva contraseña debe tener entre 8 y 128 caracteres e incluir letras y números.");
            }

            // Buscar usuario por Email o DNI
            var usuario = await _usuarioRepository.GetByEmailOrDniAsync(credential);
            if (usuario == null)
            {
                throw new UnauthorizedException("Usuario o contraseña incorrectos.");
            }

            if (!usuario.Activo)
            {
                throw new ForbiddenException("Cuenta no verificada o inhabilitada. Verifique su correo o contacte al administrador.");
            }

            // Validar contraseña actual
            bool passwordValida = _passwordHasher.VerifyPassword(dto.PasswordActual, usuario.PasswordHash);
            if (!passwordValida)
            {
                throw new UnauthorizedException("La contraseña actual es incorrecta.");
            }

            // Actualizar contraseña y desactivar bandera de cambio obligatorio
            usuario.PasswordHash = _passwordHasher.HashPassword(dto.NuevaPassword);
            usuario.RequiereCambioPassword = false;

            await _usuarioRepository.UpdateAsync(usuario);

            // Generar nuevo token para sesión inmediata
            var expiration = DateTime.UtcNow.AddHours(4);
            var token = GenerarJwtToken(usuario, expiration);

            return new CambiarPasswordResponseDTO
            {
                Exito = true,
                Mensaje = "Contraseña actualizada exitosamente. Ya puede iniciar sesión con su nueva clave.",
                Token = token
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

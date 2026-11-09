using System;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class UsuarioService : IUsuarioService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;

        public UsuarioService(
            IUsuarioRepository usuarioRepository,
            IPasswordHasher passwordHasher)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
        }

        public async Task<UsuarioResponseDTO> RegisterUsuarioAsync(UsuarioRegistroDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos de registro son obligatorios.");
            }

            if (!Enum.IsDefined(typeof(Shared.Enums.RolUsuario), dto.Rol))
            {
                throw new ValidationException("El rol de usuario especificado no es válido.");
            }

            var normalizedDni = dto.Dni?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedDni) || !normalizedDni.All(c => c >= '0' && c <= '9'))
            {
                throw new ValidationException("El DNI debe contener solo números.");
            }

            if (normalizedDni.Length < 7 || normalizedDni.Length > 8)
            {
                throw new ValidationException("El DNI debe tener entre 7 y 8 dígitos numéricos.");
            }

            var nombreTrim = dto.Nombre?.Trim() ?? string.Empty;
            var apellidoTrim = dto.Apellido?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombreTrim) ||
                nombreTrim.Length < 2 ||
                nombreTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(nombreTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El nombre debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (string.IsNullOrWhiteSpace(apellidoTrim) ||
                apellidoTrim.Length < 2 ||
                apellidoTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(apellidoTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El apellido debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > 150 ||
                normalizedEmail.Contains('<') || normalizedEmail.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                throw new ValidationException("El formato del correo electrónico no es válido.");
            }

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8 || dto.Password.Length > 128 ||
                !dto.Password.Any(char.IsLetter) || !dto.Password.Any(char.IsDigit))
            {
                throw new ValidationException("La contraseña debe tener entre 8 y 128 caracteres e incluir letras y números.");
            }

            if (dto.Telefono != null)
            {
                var telTrim = dto.Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !System.Text.RegularExpressions.Regex.IsMatch(telTrim, @"^\+?[0-9]{8,15}$"))
                {
                    throw new ValidationException("El teléfono debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
                }
            }

            // RN-10: Verificar que ni el email ni el DNI se encuentren duplicados en la base de datos
            var usuarioPorEmail = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuarioPorEmail != null)
            {
                throw new UsuarioDuplicadoException();
            }

            var usuarioPorDni = await _usuarioRepository.GetByDniAsync(normalizedDni);
            if (usuarioPorDni != null)
            {
                throw new UsuarioDuplicadoException();
            }

            // Hashear contraseña
            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // Prevención de Escalada de Privilegios (CU-01 / RNF-04):
            // En el registro público, un usuario nunca puede autoproclamarse con un rol administrativo o elevado
            // (Administrador, Organizador, Veedor). Se fuerza estrictamente al rol base (Capitan).
            var rolAsignado = (dto.Rol == Shared.Enums.RolUsuario.Administrador ||
                               dto.Rol == Shared.Enums.RolUsuario.Organizador ||
                               dto.Rol == Shared.Enums.RolUsuario.Veedor)
                ? Shared.Enums.RolUsuario.Capitan
                : dto.Rol;

            // Mapeo manual de DTO a entidad de dominio
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
                PasswordHash = passwordHash,
                Rol = rolAsignado,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<UsuarioResponseDTO> CreateUsuarioAdminAsync(UsuarioRegistroDTO dto, string? rolSolicitante = null)
        {
            if (rolSolicitante != "Administrador" && rolSolicitante != "Organizador")
            {
                throw new ForbiddenException("No tiene permisos para dar de alta usuarios con roles administrativos.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los datos de registro son obligatorios.");
            }

            if (!Enum.IsDefined(typeof(Shared.Enums.RolUsuario), dto.Rol))
            {
                throw new ValidationException("El rol de usuario especificado no es válido.");
            }

            var normalizedDni = dto.Dni?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedDni) || !normalizedDni.All(c => c >= '0' && c <= '9'))
            {
                throw new ValidationException("El DNI debe contener solo números.");
            }

            if (normalizedDni.Length < 7 || normalizedDni.Length > 8)
            {
                throw new ValidationException("El DNI debe tener entre 7 y 8 dígitos numéricos.");
            }

            var nombreTrim = dto.Nombre?.Trim() ?? string.Empty;
            var apellidoTrim = dto.Apellido?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombreTrim) ||
                nombreTrim.Length < 2 ||
                nombreTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(nombreTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El nombre debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (string.IsNullOrWhiteSpace(apellidoTrim) ||
                apellidoTrim.Length < 2 ||
                apellidoTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(apellidoTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El apellido debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > 150 ||
                normalizedEmail.Contains('<') || normalizedEmail.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                throw new ValidationException("El formato del correo electrónico no es válido.");
            }

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8 || dto.Password.Length > 128 ||
                !dto.Password.Any(char.IsLetter) || !dto.Password.Any(char.IsDigit))
            {
                throw new ValidationException("La contraseña debe tener entre 8 y 128 caracteres e incluir letras y números.");
            }

            if (dto.Telefono != null)
            {
                var telTrim = dto.Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !System.Text.RegularExpressions.Regex.IsMatch(telTrim, @"^\+?[0-9]{8,15}$"))
                {
                    throw new ValidationException("El teléfono debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
                }
            }

            var usuarioPorEmail = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuarioPorEmail != null)
            {
                throw new UsuarioDuplicadoException();
            }

            var usuarioPorDni = await _usuarioRepository.GetByDniAsync(normalizedDni);
            if (usuarioPorDni != null)
            {
                throw new UsuarioDuplicadoException();
            }

            var passwordHash = _passwordHasher.HashPassword(dto.Password);

            // En creación administrativa, el rol especificado por el administrador SÍ se respeta:
            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim(),
                PasswordHash = passwordHash,
                Rol = dto.Rol,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<UsuarioResponseDTO> CambiarRolAsync(Guid id, CambiarRolDTO dto, string? rolSolicitante = null)
        {
            if (rolSolicitante != "Administrador" && rolSolicitante != "Organizador")
            {
                throw new ForbiddenException("No tiene permisos para asignar o modificar roles de usuarios.");
            }

            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del usuario no puede ser vacío.");
            }

            if (dto == null || !Enum.IsDefined(typeof(Shared.Enums.RolUsuario), dto.NuevoRol))
            {
                throw new ValidationException("El nuevo rol especificado no es válido.");
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null)
            {
                throw new NotFoundException($"No se encontró el usuario con ID {id}");
            }

            usuario.Rol = dto.NuevoRol;
            await _usuarioRepository.UpdateAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<UsuarioResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del usuario no puede ser vacío.");
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null)
            {
                throw new NotFoundException($"No se encontró el usuario con ID {id}");
            }

            return MapToResponseDTO(usuario);
        }

        private static UsuarioResponseDTO MapToResponseDTO(Usuario usuario)
        {
            return new UsuarioResponseDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Dni = usuario.Dni,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Rol = usuario.Rol.ToString(),
                Activo = usuario.Activo,
                FechaCreacion = usuario.FechaCreacion,
                Mensaje = "Registro exitoso. Revise su correo para activar la cuenta."
            };
        }
    }
}

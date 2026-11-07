using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Security;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class VeedorService : IVeedorService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IPartidoRepository _partidoRepository;

        public VeedorService(
            IUsuarioRepository usuarioRepository,
            IPasswordHasher passwordHasher,
            IPartidoRepository partidoRepository)
        {
            _usuarioRepository = usuarioRepository;
            _passwordHasher = passwordHasher;
            _partidoRepository = partidoRepository;
        }

        public async Task<VeedorResponseDTO> CreateVeedorAsync(VeedorCreateDTO dto)
        {
            if (dto == null)
            {
                throw new ValidationException("Los datos del veedor son obligatorios.");
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
                throw new ValidationException("El nombre del veedor debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (string.IsNullOrWhiteSpace(apellidoTrim) ||
                apellidoTrim.Length < 2 ||
                apellidoTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(apellidoTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El apellido del veedor debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            var normalizedEmail = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedEmail.Length > 150 ||
                normalizedEmail.Contains('<') || normalizedEmail.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(normalizedEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                throw new ValidationException("El formato del correo electrónico no es válido.");
            }

            if (dto.Telefono != null)
            {
                var telTrim = dto.Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !System.Text.RegularExpressions.Regex.IsMatch(telTrim, @"^\+?[0-9]{8,15}$"))
                {
                    throw new ValidationException("El teléfono del veedor debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
                }
            }

            if (dto.SedeAsignada != null)
            {
                var sedeTrim = dto.SedeAsignada.Trim();
                if (string.IsNullOrWhiteSpace(sedeTrim) || sedeTrim.Length < 2 || sedeTrim.Length > 100 ||
                    sedeTrim.Contains('<') || sedeTrim.Contains('>') ||
                    !System.Text.RegularExpressions.Regex.IsMatch(sedeTrim, @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$"))
                {
                    throw new ValidationException("La sede asignada debe tener entre 2 y 100 caracteres válidos.");
                }
            }

            if (dto.TorneosAsignados != null)
            {
                if (dto.TorneosAsignados.Count > 50)
                {
                    throw new ValidationException("La lista de torneos asignados no puede superar los 50 torneos.");
                }
                if (dto.TorneosAsignados.Any(t => t == Guid.Empty))
                {
                    throw new ValidationException("La lista de torneos asignados no puede contener identificadores vacíos.");
                }
                if (dto.TorneosAsignados.Distinct().Count() != dto.TorneosAsignados.Count)
                {
                    throw new ValidationException("La lista de torneos asignados no puede contener identificadores duplicados.");
                }
            }

            // 3a. Verificar conflicto de DNI o Email existente en la plataforma
            var usuarioPorEmail = await _usuarioRepository.GetByEmailAsync(normalizedEmail);
            if (usuarioPorEmail != null)
            {
                throw new BusinessRuleConflictException("El DNI o Email ya están registrados en el sistema");
            }

            var usuarioPorDni = await _usuarioRepository.GetByDniAsync(normalizedDni);
            if (usuarioPorDni != null)
            {
                throw new BusinessRuleConflictException("El DNI o Email ya están registrados en el sistema");
            }

            // Generar contraseña inicial automática y hashearla
            var tempPassword = $"Veedor_{Guid.NewGuid():N}".Substring(0, 10) + "!";
            var passwordHash = _passwordHasher.HashPassword(tempPassword);

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = dto.Telefono?.Trim(),
                PasswordHash = passwordHash,
                Rol = RolUsuario.Veedor,
                Activo = true,
                SedeAsignada = dto.SedeAsignada?.Trim(),
                TorneosAsignadosIds = dto.TorneosAsignados != null && dto.TorneosAsignados.Any()
                    ? string.Join(",", dto.TorneosAsignados)
                    : null,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task<VeedorResponseDTO> UpdateVeedorAsync(Guid id, VeedorUpdateDTO dto)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del veedor no puede ser vacío.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los datos de actualización son obligatorios.");
            }

            var nombreTrim = dto.Nombre?.Trim() ?? string.Empty;
            var apellidoTrim = dto.Apellido?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(nombreTrim) ||
                nombreTrim.Length < 2 ||
                nombreTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(nombreTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El nombre del veedor debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (string.IsNullOrWhiteSpace(apellidoTrim) ||
                apellidoTrim.Length < 2 ||
                apellidoTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(apellidoTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El apellido del veedor debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (dto.Telefono != null)
            {
                var telTrim = dto.Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !System.Text.RegularExpressions.Regex.IsMatch(telTrim, @"^\+?[0-9]{8,15}$"))
                {
                    throw new ValidationException("El teléfono del veedor debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
                }
            }

            if (dto.SedeAsignada != null)
            {
                var sedeTrim = dto.SedeAsignada.Trim();
                if (string.IsNullOrWhiteSpace(sedeTrim) || sedeTrim.Length < 2 || sedeTrim.Length > 100 ||
                    sedeTrim.Contains('<') || sedeTrim.Contains('>') ||
                    !System.Text.RegularExpressions.Regex.IsMatch(sedeTrim, @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$"))
                {
                    throw new ValidationException("La sede asignada debe tener entre 2 y 100 caracteres válidos.");
                }
            }

            if (dto.TorneosAsignados != null)
            {
                if (dto.TorneosAsignados.Count > 50)
                {
                    throw new ValidationException("La lista de torneos asignados no puede superar los 50 torneos.");
                }
                if (dto.TorneosAsignados.Any(t => t == Guid.Empty))
                {
                    throw new ValidationException("La lista de torneos asignados no puede contener identificadores vacíos.");
                }
                if (dto.TorneosAsignados.Distinct().Count() != dto.TorneosAsignados.Count)
                {
                    throw new ValidationException("La lista de torneos asignados no puede contener identificadores duplicados.");
                }
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            usuario.Nombre = nombreTrim;
            usuario.Apellido = apellidoTrim;
            usuario.Telefono = dto.Telefono?.Trim();
            usuario.SedeAsignada = dto.SedeAsignada?.Trim();

            if (dto.TorneosAsignados != null)
            {
                usuario.TorneosAsignadosIds = string.Join(",", dto.TorneosAsignados);
            }

            await _usuarioRepository.UpdateAsync(usuario);

            return MapToResponseDTO(usuario);
        }

        public async Task DeactivateVeedorAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del veedor no puede ser vacío.");
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            usuario.Activo = false;
            await _usuarioRepository.UpdateAsync(usuario);
        }

        public async Task<VeedorResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                throw new ValidationException("El identificador del veedor no puede ser vacío.");
            }

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            return MapToResponseDTO(usuario);
        }

        public async Task<IEnumerable<VeedorResponseDTO>> GetAllAsync()
        {
            var veedores = await _usuarioRepository.FindAsync(u => u.Rol == RolUsuario.Veedor);
            return veedores.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<PartidoCronogramaResponseDTO>> GetPartidosByVeedorAsync(
            Guid veedorId, 
            DateTime? fecha = null, 
            Guid? torneoId = null, 
            Guid? currentUserId = null, 
            string? currentUserRol = null)
        {
            if (veedorId == Guid.Empty)
            {
                throw new ValidationException("El identificador del veedor no puede ser vacío.");
            }

            if (torneoId.HasValue && torneoId.Value == Guid.Empty)
            {
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            }
            // RN Paso 3a: Acceso no autorizado (HTTP 403 Forbidden)
            if (currentUserId.HasValue && 
                currentUserId.Value != veedorId && 
                !string.Equals(currentUserRol, RolUsuario.Organizador.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentUserRol, RolUsuario.Administrador.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentUserRol, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenException("No tiene permisos para consultar el cronograma de otro veedor.");
            }

            // Validar existencia del Veedor
            var veedor = await _usuarioRepository.GetByIdAsync(veedorId);
            if (veedor == null || veedor.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {veedorId}");
            }

            // Extraer los torneos asignados al veedor
            var torneosIds = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(veedor.TorneosAsignadosIds))
            {
                var ids = veedor.TorneosAsignadosIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var idStr in ids)
                {
                    if (Guid.TryParse(idStr, out var tId))
                    {
                        torneosIds.Add(tId);
                    }
                }
            }

            var partidos = await _partidoRepository.GetPartidosByVeedorAsync(veedorId, torneosIds, fecha, torneoId);

            return partidos.Select(MapToPartidoCronogramaDTO);
        }

        private static PartidoCronogramaResponseDTO MapToPartidoCronogramaDTO(Partido partido)
        {
            return new PartidoCronogramaResponseDTO
            {
                Id = partido.Id,
                TorneoId = partido.TorneoId,
                TorneoNombre = partido.Torneo?.Nombre ?? string.Empty,
                EquipoLocalId = partido.EquipoLocalId,
                EquipoLocalNombre = partido.EquipoLocal?.Nombre ?? "Equipo Local",
                EquipoVisitanteId = partido.EquipoVisitanteId,
                EquipoVisitanteNombre = partido.EquipoVisitante?.Nombre ?? "Equipo Visitante",
                FechaHora = partido.FechaHora,
                Sede = partido.Sede,
                Ronda = partido.Ronda,
                Estado = partido.Estado.ToString(),
                VeedorId = partido.VeedorId
            };
        }

        private static VeedorResponseDTO MapToResponseDTO(Usuario usuario)
        {
            var torneos = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(usuario.TorneosAsignadosIds))
            {
                var ids = usuario.TorneosAsignadosIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var idStr in ids)
                {
                    if (Guid.TryParse(idStr, out var torneoId))
                    {
                        torneos.Add(torneoId);
                    }
                }
            }

            return new VeedorResponseDTO
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Dni = usuario.Dni,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Rol = usuario.Rol.ToString(),
                Activo = usuario.Activo,
                TorneosAsignados = torneos,
                SedeAsignada = usuario.SedeAsignada,
                FechaCreacion = usuario.FechaCreacion
            };
        }
    }
}

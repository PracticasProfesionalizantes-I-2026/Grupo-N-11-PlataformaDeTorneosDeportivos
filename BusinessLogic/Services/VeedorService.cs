using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Common;
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

        public async Task<VeedorResponseDTO> CreateVeedorAsync(VeedorCreateDTO dto, string? rolSolicitante = null)
        {
            if (rolSolicitante != null && rolSolicitante != "Organizador" && rolSolicitante != "Administrador")
            {
                throw new ForbiddenException("No tiene permisos para dar de alta veedores.");
            }

            if (dto == null)
            {
                throw new ValidationException("Los datos del veedor son obligatorios.");
            }

            var normalizedDni = UsuarioValidationHelper.ValidateAndNormalizeDni(dto.Dni);
            var nombreTrim = UsuarioValidationHelper.ValidateAndNormalizeNombre(dto.Nombre, "veedor");
            var apellidoTrim = UsuarioValidationHelper.ValidateAndNormalizeApellido(dto.Apellido, "veedor");
            var normalizedEmail = UsuarioValidationHelper.ValidateAndNormalizeEmail(dto.Email);
            var telefonoTrim = UsuarioValidationHelper.ValidateAndNormalizeTelefono(dto.Telefono, "veedor");
            var sedeTrim = UsuarioValidationHelper.ValidateSedeAsignada(dto.SedeAsignada);
            UsuarioValidationHelper.ValidateTorneosAsignados(dto.TorneosAsignados);

            // 3a. Verificar conflicto de DNI o Email existente en la plataforma
            await UsuarioValidationHelper.CheckUniquenessAsync(_usuarioRepository, normalizedEmail, normalizedDni, "El DNI o Email ya están registrados en el sistema");

            // Generar contraseña inicial automática o usar la provista
            var tempPassword = UsuarioValidationHelper.ValidatePasswordInicial(dto.PasswordInicial);
            var passwordHash = _passwordHasher.HashPassword(tempPassword);

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                Email = normalizedEmail,
                Telefono = telefonoTrim,
                PasswordHash = passwordHash,
                Rol = RolUsuario.Veedor,
                Activo = true,
                RequiereCambioPassword = true,
                SedeAsignada = sedeTrim,
                TorneosAsignadosIds = dto.TorneosAsignados != null && dto.TorneosAsignados.Any()
                    ? string.Join(",", dto.TorneosAsignados)
                    : null,
                FechaCreacion = DateTime.UtcNow
            };

            await _usuarioRepository.AddAsync(usuario);

            var response = MapToResponseDTO(usuario);
            response.PasswordTemporal = tempPassword;
            response.RequiereCambioPassword = true;
            return response;
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

            var nombreTrim = UsuarioValidationHelper.ValidateAndNormalizeNombre(dto.Nombre, "veedor");
            var apellidoTrim = UsuarioValidationHelper.ValidateAndNormalizeApellido(dto.Apellido, "veedor");
            var telefonoTrim = UsuarioValidationHelper.ValidateAndNormalizeTelefono(dto.Telefono, "veedor");
            var sedeTrim = UsuarioValidationHelper.ValidateSedeAsignada(dto.SedeAsignada);
            UsuarioValidationHelper.ValidateTorneosAsignados(dto.TorneosAsignados);

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null || usuario.Rol != RolUsuario.Veedor)
            {
                throw new NotFoundException($"No se encontró el veedor con ID {id}");
            }

            usuario.Nombre = nombreTrim;
            usuario.Apellido = apellidoTrim;
            usuario.Telefono = telefonoTrim;
            usuario.SedeAsignada = sedeTrim;

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
                FechaCreacion = usuario.FechaCreacion,
                PasswordTemporal = null,
                RequiereCambioPassword = usuario.RequiereCambioPassword
            };
        }
    }
}

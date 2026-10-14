using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class TorneoService : ITorneoService
    {
        private readonly ITorneoRepository _torneoRepository;
        private readonly ICuentaRecaudadoraService? _cuentaRecaudadoraService;

        public TorneoService(
            ITorneoRepository torneoRepository,
            ICuentaRecaudadoraService? cuentaRecaudadoraService = null)
        {
            _torneoRepository = torneoRepository;
            _cuentaRecaudadoraService = cuentaRecaudadoraService;
        }

        public async Task<IEnumerable<TorneoResponseDTO>> GetAllAsync()
        {
            var torneos = await _torneoRepository.GetAllAsync();
            return torneos.Select(MapToResponseDTO);
        }

        public async Task<TorneoResponseDTO> GetByIdAsync(Guid id)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            return MapToResponseDTO(torneo);
        }

        public async Task<TorneoResponseDTO> CreateTorneoAsync(TorneoCreateDTO dto)
        {
            // Paso 3 del CU-04: Normalización y verificación de existencia de disciplina
            if (dto.DisciplinaId.HasValue)
            {
                if (!Enum.IsDefined(typeof(Disciplina), dto.DisciplinaId.Value))
                {
                    throw new DisciplinaNotFoundException("La disciplina especificada no existe");
                }
                dto.Disciplina = (Disciplina)dto.DisciplinaId.Value;
            }
            else if (!Enum.IsDefined(typeof(Disciplina), dto.Disciplina))
            {
                throw new DisciplinaNotFoundException("La disciplina especificada no existe");
            }

            var normalizedName = dto.Nombre?.Trim() ?? string.Empty;

            // Paso 3 del CU-04: Comprobar que no exista otro torneo con el mismo nombre
            var torneoExistente = await _torneoRepository.GetByNombreAsync(normalizedName);
            if (torneoExistente != null)
            {
                throw new TorneoNombreDuplicadoException("Ya existe un torneo con el nombre ingresado");
            }

            // Validar reglas de negocio
            await ValidateRulesAsync(dto);

            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = normalizedName,
                Disciplina = dto.Disciplina,
                Modalidad = dto.Modalidad,
                CupoMaximo = dto.CupoMaximo,
                EsPago = dto.EsPago || dto.CostoInscripcion > 0,
                CostoInscripcion = dto.CostoInscripcion,
                Estado = "Abierto para Inscripción"
            };

            await _torneoRepository.AddAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public Task<TorneoResponseDTO> CreateAsync(TorneoCreateDTO dto)
        {
            return CreateTorneoAsync(dto);
        }

        public async Task<TorneoResponseDTO> UpdateAsync(Guid id, TorneoUpdateDTO dto)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            var createEquivalent = new TorneoCreateDTO
            {
                Nombre = dto.Nombre,
                Disciplina = dto.Disciplina,
                DisciplinaId = dto.DisciplinaId,
                Modalidad = dto.Modalidad,
                CupoMaximo = dto.CupoMaximo,
                EsPago = dto.EsPago,
                CostoInscripcion = dto.CostoInscripcion,
                CuentaRecaudadoraActiva = dto.CuentaRecaudadoraActiva
            };

            await ValidateRulesAsync(createEquivalent);

            torneo.Nombre = dto.Nombre.Trim();
            torneo.Disciplina = dto.Disciplina;
            torneo.Modalidad = dto.Modalidad;
            torneo.CupoMaximo = dto.CupoMaximo;
            torneo.EsPago = dto.EsPago || dto.CostoInscripcion > 0;
            torneo.CostoInscripcion = dto.CostoInscripcion;

            await _torneoRepository.UpdateAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public async Task DeleteAsync(Guid id)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            await _torneoRepository.DeleteAsync(torneo);
        }

        private async Task ValidateRulesAsync(TorneoCreateDTO dto)
        {
            // RN-04: Eliminación directa requiere cupo mínimo de 4 y potencia de 2
            if (dto.Modalidad == Modalidad.EliminacionDirecta)
            {
                if (dto.CupoMaximo < 4)
                {
                    throw new BusinessRuleConflictException("Los torneos de Eliminación Directa requieren un cupo mínimo de 4 equipos.");
                }

                bool esPotenciaDe2 = (dto.CupoMaximo & (dto.CupoMaximo - 1)) == 0;
                if (!esPotenciaDe2)
                {
                    throw new CupoInvalidoEliminacionException("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)");
                }
            }

            // Validar monto no negativo
            if (dto.CostoInscripcion < 0)
            {
                throw new ValidationException("El costo de inscripción no puede ser negativo.");
            }

            // RN-05: Si el torneo es de pago, costo > 0 y cuenta recaudadora activa obligatoria
            if (dto.EsPago)
            {
                if (dto.CostoInscripcion <= 0)
                {
                    throw new CostoInvalidoException("El costo debe ser mayor a cero para torneos pagos");
                }

                bool cuentaActiva = true;
                if (_cuentaRecaudadoraService != null)
                {
                    cuentaActiva = await _cuentaRecaudadoraService.IsCuentaActivaAsync();
                }

                if (dto.CuentaRecaudadoraActiva.HasValue)
                {
                    cuentaActiva = dto.CuentaRecaudadoraActiva.Value;
                }

                if (!cuentaActiva)
                {
                    throw new CuentaRecaudadoraInactivaException("No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)");
                }
            }
        }

        private static TorneoResponseDTO MapToResponseDTO(Torneo torneo)
        {
            return new TorneoResponseDTO
            {
                Id = torneo.Id,
                Nombre = torneo.Nombre,
                Disciplina = torneo.Disciplina.ToString(),
                Modalidad = torneo.Modalidad.ToString(),
                CupoMaximo = torneo.CupoMaximo,
                EsPago = torneo.EsPago,
                CostoInscripcion = torneo.CostoInscripcion,
                Estado = torneo.Estado
            };
        }
    }
}

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
        private readonly IDisciplinaRepository _disciplinaRepository;
        private readonly ICuentaRecaudadoraRepository _cuentaRecaudadoraRepository;

        public TorneoService(
            ITorneoRepository torneoRepository,
            IDisciplinaRepository disciplinaRepository,
            ICuentaRecaudadoraRepository cuentaRecaudadoraRepository)
        {
            _torneoRepository = torneoRepository;
            _disciplinaRepository = disciplinaRepository;
            _cuentaRecaudadoraRepository = cuentaRecaudadoraRepository;
        }

        public TorneoService(ITorneoRepository torneoRepository)
            : this(torneoRepository, new DisciplinaRepository(), new CuentaRecaudadoraRepository())
        {
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
            // 1. Normalización de campos de texto
            var nombreNormalizado = dto.Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreNormalizado))
            {
                throw new ValidationException("El nombre del torneo es obligatorio.");
            }

            // 2. Verificar existencia de la disciplina
            int disciplinaId = dto.DisciplinaId ?? (dto.Disciplina.HasValue ? (int)dto.Disciplina.Value : -1);
            if (disciplinaId < 0 || !await _disciplinaRepository.ExistsAndIsActiveAsync(disciplinaId))
            {
                throw new DisciplinaNotFoundException("La disciplina especificada no existe");
            }

            // 3. Comprobar que no exista otro torneo registrado con el mismo nombre
            if (await _torneoRepository.ExistsByNameAsync(nombreNormalizado))
            {
                throw new TorneoNombreDuplicadoException("Ya existe un torneo con el nombre ingresado");
            }

            var modalidad = dto.Modalidad ?? Modalidad.Liga;
            var cupoMaximo = dto.CupoMaximo ?? 0;

            // 4. Regla RN-04: Eliminación directa requiere potencia de 2 y mínimo 4
            if (modalidad == Modalidad.EliminacionDirecta)
            {
                if (cupoMaximo < 4 || !EsPotenciaDeDos(cupoMaximo))
                {
                    throw new CupoInvalidoEliminacionException("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)");
                }
            }
            else if (cupoMaximo <= 0)
            {
                throw new ValidationException("El cupo máximo debe ser mayor a cero.");
            }

            // 5. Regla RN-05: Validación de costos y cuenta recaudadora para torneos pagos
            if (dto.EsPago)
            {
                if (dto.CostoInscripcion <= 0)
                {
                    throw new CostoInvalidoException("El costo debe ser mayor a cero para torneos pagos");
                }

                var cuentaActiva = await _cuentaRecaudadoraRepository.TieneCuentaActivaAsync();
                if (!cuentaActiva)
                {
                    throw new CuentaRecaudadoraInactivaException("No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)");
                }
            }
            else
            {
                if (dto.CostoInscripcion < 0)
                {
                    throw new ValidationException("El costo de inscripción no puede ser negativo.");
                }
            }

            // 6. Almacenamiento en Capa de Persistencia con estado "Abierto para Inscripción"
            var torneo = new Torneo
            {
                Id = Guid.NewGuid(),
                Nombre = nombreNormalizado,
                Disciplina = (Disciplina)disciplinaId,
                Modalidad = modalidad,
                CupoMaximo = cupoMaximo,
                CostoInscripcion = dto.EsPago ? dto.CostoInscripcion : 0,
                EsPago = dto.EsPago,
                Estado = "Abierto para Inscripción"
            };

            await _torneoRepository.AddAsync(torneo);

            // 7. Retorno de DTO resultante
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

            ValidateRules(dto.Modalidad, dto.CupoMaximo, dto.CostoInscripcion, dto.EsPago);

            torneo.Nombre = dto.Nombre.Trim();
            torneo.Disciplina = dto.Disciplina;
            torneo.Modalidad = dto.Modalidad;
            torneo.CupoMaximo = dto.CupoMaximo;
            torneo.EsPago = dto.EsPago;
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

        private static bool EsPotenciaDeDos(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }

        private void ValidateRules(Modalidad modalidad, int cupoMaximo, decimal costoInscripcion, bool esPago)
        {
            if (modalidad == Modalidad.EliminacionDirecta && (cupoMaximo < 4 || !EsPotenciaDeDos(cupoMaximo)))
                throw new CupoInvalidoEliminacionException("Para torneos de eliminación directa el cupo debe ser potencia de 2 y mínimo 4 (RN-04)");

            if (esPago && costoInscripcion <= 0)
                throw new CostoInvalidoException("El costo debe ser mayor a cero para torneos pagos");

            if (costoInscripcion < 0)
                throw new ValidationException("El costo de inscripción no puede ser negativo.");
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
                CostoInscripcion = torneo.CostoInscripcion,
                EsPago = torneo.EsPago,
                Estado = torneo.Estado
            };
        }
    }
}

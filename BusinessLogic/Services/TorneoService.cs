using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Services;
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

        public TorneoService(ITorneoRepository torneoRepository)
        {
            _torneoRepository = torneoRepository;
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

        public async Task<TorneoResponseDTO> CreateAsync(TorneoCreateDTO dto)
        {
            ValidateRules(dto.Modalidad, dto.CupoMaximo, dto.CostoInscripcion);

            var torneo = new Torneo
            {
                Nombre = dto.Nombre,
                Disciplina = dto.Disciplina,
                Modalidad = dto.Modalidad,
                CupoMaximo = dto.CupoMaximo,
                CostoInscripcion = dto.CostoInscripcion
            };

            await _torneoRepository.AddAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public async Task<TorneoResponseDTO> UpdateAsync(Guid id, TorneoUpdateDTO dto)
        {
            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            ValidateRules(dto.Modalidad, dto.CupoMaximo, dto.CostoInscripcion);

            torneo.Nombre = dto.Nombre;
            torneo.Disciplina = dto.Disciplina;
            torneo.Modalidad = dto.Modalidad;
            torneo.CupoMaximo = dto.CupoMaximo;
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

        private void ValidateRules(Modalidad modalidad, int cupoMaximo, decimal costoInscripcion)
        {
            if (modalidad == Modalidad.EliminacionDirecta && cupoMaximo < 4)
                throw new BusinessRuleConflictException("Los torneos de Eliminación Directa requieren un cupo mínimo de 4 equipos.");

            if (costoInscripcion < 0 || (costoInscripcion == 0 && costoInscripcion < 0)) 
                throw new ValidationException("El costo de inscripción no puede ser negativo.");

            if (costoInscripcion > 0 && costoInscripcion <= 0) // Logical catch if any other rule applies, though just cost > 0 when has cost
                throw new ValidationException("El monto debe ser mayor a cero.");
        }

        private TorneoResponseDTO MapToResponseDTO(Torneo torneo)
        {
            return new TorneoResponseDTO
            {
                Id = torneo.Id,
                Nombre = torneo.Nombre,
                Disciplina = torneo.Disciplina.ToString(),
                Modalidad = torneo.Modalidad.ToString(),
                CupoMaximo = torneo.CupoMaximo,
                CostoInscripcion = torneo.CostoInscripcion
            };
        }
    }
}

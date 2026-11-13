using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic.Common;
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
        private readonly IPartidoRepository? _partidoRepository;
        private readonly ITablaPosicionRepository? _tablaPosicionRepository;

        public TorneoService(
            ITorneoRepository torneoRepository,
            ICuentaRecaudadoraService? cuentaRecaudadoraService = null,
            IPartidoRepository? partidoRepository = null,
            ITablaPosicionRepository? tablaPosicionRepository = null)
        {
            _torneoRepository = torneoRepository;
            _cuentaRecaudadoraService = cuentaRecaudadoraService;
            _partidoRepository = partidoRepository;
            _tablaPosicionRepository = tablaPosicionRepository;
        }

        public async Task<IEnumerable<TorneoResponseDTO>> GetAllAsync()
        {
            var torneos = await _torneoRepository.GetAllAsync();
            return torneos.Select(MapToResponseDTO);
        }

        public async Task<TorneoResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(id) ?? await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            return MapToResponseDTO(torneo);
        }

        public async Task<TorneoResponseDTO> CreateTorneoAsync(TorneoCreateDTO dto)
        {
            if (dto == null)
                throw new ValidationException("Los datos del torneo son obligatorios.");

            // Paso 3 del CU-04: Normalización y verificación de existencia de disciplina
            if (!Enum.IsDefined(typeof(Disciplina), dto.Disciplina))
            {
                throw new DisciplinaNotFoundException("La disciplina especificada no existe");
            }

            if (dto.DisciplinaId.HasValue)
            {
                if (!Enum.IsDefined(typeof(Disciplina), dto.DisciplinaId.Value))
                {
                    throw new DisciplinaNotFoundException("La disciplina especificada no existe");
                }
                dto.Disciplina = (Disciplina)dto.DisciplinaId.Value;
            }

            if (!Enum.IsDefined(typeof(Modalidad), dto.Modalidad))
            {
                throw new ValidationException("La modalidad especificada no es válida.");
            }

            if (dto.ModalidadId.HasValue)
            {
                if (!Enum.IsDefined(typeof(Modalidad), dto.ModalidadId.Value))
                {
                    throw new ValidationException("La modalidad especificada no es válida.");
                }
                dto.Modalidad = (Modalidad)dto.ModalidadId.Value;
            }

            var normalizedName = dto.Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName) ||
                normalizedName.Length < 3 ||
                normalizedName.Length > 100 ||
                normalizedName.Contains('<') ||
                normalizedName.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(normalizedName, @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)\/]+$"))
            {
                throw new ValidationException("El nombre del torneo debe tener entre 3 y 100 caracteres válidos.");
            }

            if (dto.Descripcion != null)
            {
                var descTrim = dto.Descripcion.Trim();
                if (string.IsNullOrWhiteSpace(descTrim) || descTrim.Length < 3 || descTrim.Length > 500 || descTrim.Contains('<') || descTrim.Contains('>'))
                {
                    throw new ValidationException("La descripción del torneo debe tener entre 3 y 500 caracteres válidos y no puede estar vacía.");
                }
            }

            if (dto.Reglamento != null)
            {
                var regTrim = dto.Reglamento.Trim();
                if (string.IsNullOrWhiteSpace(regTrim) || regTrim.Length < 3 || regTrim.Length > 2000 || regTrim.Contains('<') || regTrim.Contains('>'))
                {
                    throw new ValidationException("El reglamento del torneo debe tener entre 3 y 2000 caracteres válidos y no puede estar vacío.");
                }
            }

            if (dto.FechaInicio.HasValue)
            {
                var f = dto.FechaInicio.Value.Date;
                if (f < DateTime.Today || f > DateTime.Today.AddYears(5))
                {
                    throw new ValidationException("La fecha de inicio del torneo no puede estar en el pasado ni superar los 5 años a futuro.");
                }
            }

            if (dto.FechaFin.HasValue)
            {
                var fFin = dto.FechaFin.Value.Date;
                var minDate = dto.FechaInicio.HasValue ? dto.FechaInicio.Value.Date : DateTime.Today;
                if (fFin < minDate || fFin > DateTime.Today.AddYears(5))
                {
                    throw new ValidationException("La fecha de finalización no puede ser anterior a la fecha de inicio ni estar en el pasado.");
                }
            }

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
                EsPago = dto.EsPago,
                CostoInscripcion = dto.CostoInscripcion,
                Estado = EstadoTorneo.Abierto
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
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            if (dto == null)
                throw new ValidationException("Los datos del torneo son obligatorios.");

            if (!Enum.IsDefined(typeof(Disciplina), dto.Disciplina))
            {
                throw new DisciplinaNotFoundException("La disciplina especificada no existe");
            }

            if (dto.DisciplinaId.HasValue)
            {
                if (!Enum.IsDefined(typeof(Disciplina), dto.DisciplinaId.Value))
                {
                    throw new DisciplinaNotFoundException("La disciplina especificada no existe");
                }
                dto.Disciplina = (Disciplina)dto.DisciplinaId.Value;
            }

            if (!Enum.IsDefined(typeof(Modalidad), dto.Modalidad))
            {
                throw new ValidationException("La modalidad especificada no es válida.");
            }

            if (dto.ModalidadId.HasValue)
            {
                if (!Enum.IsDefined(typeof(Modalidad), dto.ModalidadId.Value))
                {
                    throw new ValidationException("La modalidad especificada no es válida.");
                }
                dto.Modalidad = (Modalidad)dto.ModalidadId.Value;
            }

            var normalizedName = dto.Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName) ||
                normalizedName.Length < 3 ||
                normalizedName.Length > 100 ||
                normalizedName.Contains('<') ||
                normalizedName.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(normalizedName, @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)\/]+$"))
            {
                throw new ValidationException("El nombre del torneo debe tener entre 3 y 100 caracteres válidos.");
            }

            if (dto.Descripcion != null)
            {
                var descTrim = dto.Descripcion.Trim();
                if (string.IsNullOrWhiteSpace(descTrim) || descTrim.Length < 3 || descTrim.Length > 500 || descTrim.Contains('<') || descTrim.Contains('>'))
                {
                    throw new ValidationException("La descripción del torneo debe tener entre 3 y 500 caracteres válidos y no puede estar vacía.");
                }
            }

            if (dto.Reglamento != null)
            {
                var regTrim = dto.Reglamento.Trim();
                if (string.IsNullOrWhiteSpace(regTrim) || regTrim.Length < 3 || regTrim.Length > 2000 || regTrim.Contains('<') || regTrim.Contains('>'))
                {
                    throw new ValidationException("El reglamento del torneo debe tener entre 3 y 2000 caracteres válidos y no puede estar vacío.");
                }
            }

            if (dto.FechaInicio.HasValue)
            {
                var f = dto.FechaInicio.Value.Date;
                if (f < DateTime.Today || f > DateTime.Today.AddYears(5))
                {
                    throw new ValidationException("La fecha de inicio del torneo no puede estar en el pasado ni superar los 5 años a futuro.");
                }
            }

            if (dto.FechaFin.HasValue)
            {
                var fFin = dto.FechaFin.Value.Date;
                var minDate = dto.FechaInicio.HasValue ? dto.FechaInicio.Value.Date : DateTime.Today;
                if (fFin < minDate || fFin > DateTime.Today.AddYears(5))
                {
                    throw new ValidationException("La fecha de finalización no puede ser anterior a la fecha de inicio ni estar en el pasado.");
                }
            }

            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            var torneoConMismoNombre = await _torneoRepository.GetByNombreAsync(normalizedName);
            if (torneoConMismoNombre != null && torneoConMismoNombre.Id != id)
            {
                throw new TorneoNombreDuplicadoException("Ya existe un torneo con el nombre ingresado");
            }

            var createEquivalent = new TorneoCreateDTO
            {
                Nombre = normalizedName,
                Disciplina = dto.Disciplina,
                DisciplinaId = dto.DisciplinaId,
                Modalidad = dto.Modalidad,
                ModalidadId = dto.ModalidadId,
                CupoMaximo = dto.CupoMaximo,
                EsPago = dto.EsPago,
                CostoInscripcion = dto.CostoInscripcion,
                CuentaRecaudadoraActiva = dto.CuentaRecaudadoraActiva,
                Descripcion = dto.Descripcion,
                Reglamento = dto.Reglamento,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin
            };

            await ValidateRulesAsync(createEquivalent);

            torneo.Nombre = normalizedName;
            torneo.Disciplina = dto.Disciplina;
            torneo.Modalidad = dto.Modalidad;
            torneo.CupoMaximo = dto.CupoMaximo;
            torneo.EsPago = dto.EsPago;
            torneo.CostoInscripcion = dto.CostoInscripcion;

            await _torneoRepository.UpdateAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public async Task<TorneoResponseDTO> CerrarInscripcionesAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(id) ?? await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            if (torneo.Estado != EstadoTorneo.Abierto)
            {
                throw new TorneoEstadoInvalidoException("Solo se pueden cerrar las inscripciones de un torneo que se encuentra en estado Abierto.");
            }

            torneo.Estado = EstadoTorneo.InscripcionesCerradas;
            await _torneoRepository.UpdateAsync(torneo);
            return MapToResponseDTO(torneo);
        }

        public async Task DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            var torneo = await _torneoRepository.GetByIdAsync(id);
            if (torneo == null)
                throw new NotFoundException($"No se encontró el torneo con ID {id}");

            await _torneoRepository.DeleteAsync(torneo);
        }

        private async Task ValidateRulesAsync(TorneoCreateDTO dto)
        {
            if (!Enum.IsDefined(typeof(Disciplina), dto.Disciplina))
            {
                throw new DisciplinaNotFoundException("La disciplina especificada no existe");
            }

            if (!Enum.IsDefined(typeof(Modalidad), dto.Modalidad))
            {
                throw new ValidationException("La modalidad especificada no es válida.");
            }

            if (dto.CupoMaximo < 2 || dto.CupoMaximo > 128)
            {
                throw new ValidationException("El cupo máximo debe ser al menos 2 y no superar los 128 equipos.");
            }

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
            else if (dto.Modalidad == Modalidad.Liga)
            {
                if (dto.CupoMaximo < 2)
                {
                    throw new ValidationException("Los torneos de Liga requieren un cupo mínimo de 2 equipos.");
                }
            }

            // Validar monto no negativo ni excesivo
            if (dto.CostoInscripcion < 0 || dto.CostoInscripcion > 100_000_000m)
            {
                throw new ValidationException("El costo de inscripción no puede ser negativo ni superar los 100.000.000.");
            }

            // Coherencia estricta entre EsPago y CostoInscripcion (Caso 1: EsPago == false)
            if (!dto.EsPago && dto.CostoInscripcion != 0)
            {
                throw new CostoInvalidoException("No se puede ingresar un costo de inscripción si el torneo es gratuito (EsPago = false)");
            }

            // RN-05: Si el torneo es de pago (Caso 2: EsPago == true), costo > 0 y cuenta recaudadora activa obligatoria
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

        public async Task<IEnumerable<PosicionTablaDTO>> GetPosicionesAsync(Guid torneoId)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            // Si hay repositorio y ya cuenta con posiciones persistidas para este torneo, las devolvemos directamente
            if (_tablaPosicionRepository != null)
            {
                var persistidas = await _tablaPosicionRepository.GetByTorneoIdAsync(torneoId);
                var listaPersistidas = persistidas?.ToList() ?? new List<TablaPosicion>();
                if (listaPersistidas.Count > 0)
                {
                    return listaPersistidas.Select(StandingsCalculator.MapEntityToDTO).ToList();
                }
            }

            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId);
            if (torneo == null)
            {
                torneo = await _torneoRepository.GetByIdAsync(torneoId);
            }
            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            if (torneo.Modalidad != Modalidad.Liga)
            {
                throw new ValidationException("La vista solicitada no es compatible con el formato del torneo.");
            }

            IEnumerable<Partido> partidos = _partidoRepository != null
                ? await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId)
                : (torneo.Partidos ?? Enumerable.Empty<Partido>());

            var listaPartidos = partidos.ToList();
            if (listaPartidos.Count == 0 && torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                listaPartidos = torneo.Partidos.ToList();
            }

            var resultadoOrdenado = StandingsCalculator.CalculateStandingsDTOs(torneo.Equipos, listaPartidos);

            // Persistimos en la base de datos si contamos con el repositorio
            if (_tablaPosicionRepository != null && resultadoOrdenado.Count > 0)
            {
                var entidades = StandingsCalculator.CalculateStandingsEntities(torneoId, torneo.Equipos, listaPartidos);
                await _tablaPosicionRepository.UpsertPosicionesTorneoAsync(torneoId, entidades);
            }

            return resultadoOrdenado;
        }

        public async Task<BracketTorneoDTO> GetLlavesAsync(Guid torneoId)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            var torneo = await _torneoRepository.GetTorneoConEquiposAsync(torneoId);
            if (torneo == null)
            {
                torneo = await _torneoRepository.GetByIdAsync(torneoId);
            }
            if (torneo == null)
            {
                throw new NotFoundException($"No se encontró el torneo con ID {torneoId}");
            }

            if (torneo.Modalidad != Modalidad.EliminacionDirecta)
            {
                throw new ValidationException("La vista solicitada no es compatible con el formato del torneo.");
            }

            IEnumerable<Partido> partidos = _partidoRepository != null
                ? await _partidoRepository.GetPartidosByTorneoIdAsync(torneoId)
                : (torneo.Partidos ?? Enumerable.Empty<Partido>());

            var listaPartidos = partidos.ToList();
            if (listaPartidos.Count == 0 && torneo.Partidos != null && torneo.Partidos.Count > 0)
            {
                listaPartidos = torneo.Partidos.ToList();
            }

            var nombresEquipos = torneo.Equipos?.ToDictionary(e => e.Id, e => e.Nombre) ?? new Dictionary<Guid, string>();

            return MapToBracketTorneoDTO(torneo, listaPartidos, nombresEquipos);
        }

        private static BracketTorneoDTO MapToBracketTorneoDTO(Torneo torneo, List<Partido> partidos, Dictionary<Guid, string> nombresEquipos)
        {
            var rondasAgrupadas = partidos
                .GroupBy(p => p.Ronda)
                .OrderBy(g => g.Key)
                .ToList();

            int totalRondas = rondasAgrupadas.Any() ? rondasAgrupadas.Max(g => g.Key) : 1;

            var rondasDto = new List<RondaBracketDTO>();

            foreach (var grupo in rondasAgrupadas)
            {
                int numeroRonda = grupo.Key;
                string nombreRonda = ObtenerNombreRonda(numeroRonda, totalRondas);

                var partidosDto = grupo
                    .OrderBy(p => p.FechaHora)
                    .Select(p => MapToPartidoBracketDTO(p, nombresEquipos))
                    .ToList();

                rondasDto.Add(new RondaBracketDTO
                {
                    NumeroRonda = numeroRonda,
                    NombreRonda = nombreRonda,
                    Partidos = partidosDto
                });
            }

            string? campeonNombre = null;
            if (torneo.CampeonId.HasValue)
            {
                if (torneo.Campeon != null)
                {
                    campeonNombre = torneo.Campeon.Nombre;
                }
                else if (nombresEquipos.TryGetValue(torneo.CampeonId.Value, out var cn))
                {
                    campeonNombre = cn;
                }
            }

            return new BracketTorneoDTO
            {
                TorneoId = torneo.Id,
                TorneoNombre = torneo.Nombre,
                TotalRondas = totalRondas,
                Rondas = rondasDto,
                CampeonId = torneo.CampeonId,
                CampeonNombre = campeonNombre
            };
        }

        private static string ObtenerNombreRonda(int numeroRonda, int totalRondas)
        {
            int rondasRestantes = totalRondas - numeroRonda;
            return rondasRestantes switch
            {
                0 => "Final",
                1 => "Semifinal",
                2 => "Cuartos de Final",
                3 => "Octavos de Final",
                _ => $"Ronda {numeroRonda}"
            };
        }

        private static PartidoBracketDTO MapToPartidoBracketDTO(Partido p, Dictionary<Guid, string>? nombresEquipos = null)
        {
            Guid? ganadorId = null;
            string? ganadorNombre = null;

            string localNombre = p.EquipoLocal?.Nombre 
                ?? (p.EquipoLocalId.HasValue && nombresEquipos != null && nombresEquipos.TryGetValue(p.EquipoLocalId.Value, out var nl) ? nl : "Por Definir");

            string visNombre = p.EquipoVisitante?.Nombre 
                ?? (p.EquipoVisitanteId.HasValue && nombresEquipos != null && nombresEquipos.TryGetValue(p.EquipoVisitanteId.Value, out var nv) ? nv : "Por Definir");

            if (p.MarcadorLocal.HasValue && p.MarcadorVisitante.HasValue)
            {
                if (p.MarcadorLocal.Value > p.MarcadorVisitante.Value)
                {
                    ganadorId = p.EquipoLocalId;
                    ganadorNombre = localNombre;
                }
                else if (p.MarcadorVisitante.Value > p.MarcadorLocal.Value)
                {
                    ganadorId = p.EquipoVisitanteId;
                    ganadorNombre = visNombre;
                }
                else if (p.PenalesLocal.HasValue && p.PenalesVisitante.HasValue)
                {
                    if (p.PenalesLocal.Value > p.PenalesVisitante.Value)
                    {
                        ganadorId = p.EquipoLocalId;
                        ganadorNombre = localNombre;
                    }
                    else if (p.PenalesVisitante.Value > p.PenalesLocal.Value)
                    {
                        ganadorId = p.EquipoVisitanteId;
                        ganadorNombre = visNombre;
                    }
                }
            }

            return new PartidoBracketDTO
            {
                Id = p.Id,
                Ronda = p.Ronda,
                EquipoLocalId = p.EquipoLocalId,
                EquipoLocalNombre = localNombre,
                EquipoVisitanteId = p.EquipoVisitanteId,
                EquipoVisitanteNombre = visNombre,
                MarcadorLocal = p.MarcadorLocal,
                MarcadorVisitante = p.MarcadorVisitante,
                PenalesLocal = p.PenalesLocal,
                PenalesVisitante = p.PenalesVisitante,
                Estado = p.Estado.ToString(),
                GanadorId = ganadorId,
                GanadorNombre = ganadorNombre
            };
        }

        private static TorneoResponseDTO MapToResponseDTO(Torneo torneo)
        {
            string? campeonNombre = torneo.Campeon?.Nombre;
            if (campeonNombre == null && torneo.CampeonId.HasValue && torneo.Equipos != null)
            {
                campeonNombre = torneo.Equipos.FirstOrDefault(e => e.Id == torneo.CampeonId.Value)?.Nombre;
            }

            return new TorneoResponseDTO
            {
                Id = torneo.Id,
                Nombre = torneo.Nombre,
                Disciplina = torneo.Disciplina.ToString(),
                Modalidad = torneo.Modalidad.ToString(),
                CupoMaximo = torneo.CupoMaximo,
                EsPago = torneo.EsPago,
                CostoInscripcion = torneo.CostoInscripcion,
                Estado = torneo.Estado.ToString(),
                CampeonId = torneo.CampeonId,
                CampeonNombre = campeonNombre
            };
        }
    }
}

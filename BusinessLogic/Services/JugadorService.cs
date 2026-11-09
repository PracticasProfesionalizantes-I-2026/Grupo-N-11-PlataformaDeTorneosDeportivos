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
    public class JugadorService : IJugadorService
    {
        private readonly IJugadorRepository _jugadorRepository;
        private readonly IEquipoRepository _equipoRepository;
        private readonly IIncidenciaRepository? _incidenciaRepository;
        private readonly ITorneoRepository? _torneoRepository;

        public JugadorService(
            IJugadorRepository jugadorRepository, 
            IEquipoRepository equipoRepository,
            IIncidenciaRepository? incidenciaRepository = null,
            ITorneoRepository? torneoRepository = null)
        {
            _jugadorRepository = jugadorRepository;
            _equipoRepository = equipoRepository;
            _incidenciaRepository = incidenciaRepository;
            _torneoRepository = torneoRepository;
        }

        public async Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdAsync(Guid equipoId)
        {
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipoId);
            return jugadores.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<JugadorPublicResponseDTO>> GetByEquipoIdPublicAsync(Guid equipoId)
        {
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipoId);
            return jugadores.Select(j => new JugadorPublicResponseDTO
            {
                Id = j.Id,
                Nombre = j.Nombre,
                Apellido = j.Apellido,
                EquipoId = j.EquipoId,
                Inhabilitado = j.Inhabilitado
            });
        }

        public async Task<IEnumerable<JugadorResponseDTO>> GetByEquipoIdDetalladoAsync(Guid equipoId, Guid? usuarioId, string? rol)
        {
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var equipo = await _equipoRepository.GetByIdAsync(equipoId);
            if (equipo == null)
            {
                throw new EquipoNotFoundException("No se encontró el equipo especificado");
            }

            var isAuthorized = (rol == "Organizador" || rol == "Administrador") ||
                               (rol == "Capitan" && usuarioId.HasValue && equipo.CapitanId.HasValue && equipo.CapitanId.Value == usuarioId.Value);

            if (!isAuthorized)
            {
                throw new ForbiddenException("No tiene permisos para acceder a la nómina completa ni a los datos sensibles de este equipo.");
            }

            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipoId);
            return jugadores.Select(MapToResponseDTO);
        }

        public async Task<JugadorResponseDTO> CreateAsync(JugadorCreateDTO dto, Guid? solicitanteId = null, string? rolSolicitante = null)
        {
            if (dto == null)
                throw new ValidationException("Los datos del jugador son obligatorios.");

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
                throw new ValidationException("El nombre del jugador debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (string.IsNullOrWhiteSpace(apellidoTrim) ||
                apellidoTrim.Length < 2 ||
                apellidoTrim.Length > 60 ||
                !System.Text.RegularExpressions.Regex.IsMatch(apellidoTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
            {
                throw new ValidationException("El apellido del jugador debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
            }

            if (dto.Telefono != null)
            {
                var telTrim = dto.Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !System.Text.RegularExpressions.Regex.IsMatch(telTrim, @"^\+?[0-9]{8,15}$"))
                {
                    throw new ValidationException("El teléfono del jugador debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
                }
            }

            if (dto.Email != null)
            {
                var emailTrim = dto.Email.Trim();
                if (string.IsNullOrWhiteSpace(emailTrim) || emailTrim.Length > 150 || emailTrim.Contains('<') || emailTrim.Contains('>') ||
                    !System.Text.RegularExpressions.Regex.IsMatch(emailTrim, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    throw new ValidationException("El correo electrónico del jugador no tiene un formato válido.");
                }
            }

            if (dto.Edad.HasValue && (dto.Edad.Value < 5 || dto.Edad.Value > 90))
            {
                throw new ValidationException("La edad del jugador debe estar entre 5 y 90 años.");
            }

            if (dto.FechaNacimiento.HasValue)
            {
                var hoy = DateTime.Today;
                var fn = dto.FechaNacimiento.Value.Date;
                int edadCalc = hoy.Year - fn.Year;
                if (fn > hoy.AddYears(-edadCalc))
                    edadCalc--;
                if (fn > hoy || edadCalc < 5 || edadCalc > 90)
                {
                    throw new ValidationException("La fecha de nacimiento del jugador debe corresponder a una edad lógica entre 5 y 90 años.");
                }
            }

            if (dto.NumeroCamiseta.HasValue && (dto.NumeroCamiseta.Value < 1 || dto.NumeroCamiseta.Value > 99))
            {
                throw new ValidationException("El número de camiseta debe estar entre 1 y 99.");
            }

            if (dto.Dorsal.HasValue && (dto.Dorsal.Value < 1 || dto.Dorsal.Value > 99))
            {
                throw new ValidationException("El dorsal debe estar entre 1 y 99.");
            }

            if (dto.Posicion != null)
            {
                var posTrim = dto.Posicion.Trim();
                if (string.IsNullOrWhiteSpace(posTrim) || posTrim.Length < 2 || posTrim.Length > 40 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(posTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-]+$"))
                {
                    throw new ValidationException("La posición del jugador debe tener entre 2 y 40 caracteres y contener solo letras.");
                }
            }

            if (dto.DocumentacionUrl != null)
            {
                var docTrim = dto.DocumentacionUrl.Trim();
                if (string.IsNullOrWhiteSpace(docTrim) || docTrim.Length > 500 || docTrim.Contains('<') || docTrim.Contains('>'))
                {
                    throw new ValidationException("La URL de documentación no es válida o supera los 500 caracteres.");
                }
            }

            if (dto.FotoUrl != null)
            {
                var fotoTrim = dto.FotoUrl.Trim();
                if (string.IsNullOrWhiteSpace(fotoTrim) || fotoTrim.Length > 500 || fotoTrim.Contains('<') || fotoTrim.Contains('>'))
                {
                    throw new ValidationException("La URL de la foto no es válida o supera los 500 caracteres.");
                }
            }

            if (dto.EquipoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del equipo no puede ser vacío.");
            }

            var equipoDestino = await _equipoRepository.GetByIdAsync(dto.EquipoId);
            if (equipoDestino == null)
                throw new NotFoundException($"No se encontró el equipo con ID {dto.EquipoId}");

            // Mitigación IDOR / Titularidad:
            // Si el solicitante no es Organizador ni Administrador, solo el capitán dueño del equipo puede registrar jugadores.
            if (solicitanteId.HasValue && rolSolicitante != "Organizador" && rolSolicitante != "Administrador")
            {
                if (!equipoDestino.CapitanId.HasValue || equipoDestino.CapitanId.Value != solicitanteId.Value)
                {
                    throw new ForbiddenException("No tiene permisos para registrar jugadores en un equipo que pertenece a otro capitán.");
                }
            }

            // RN-03 / CU-05: Solo se pueden agregar jugadores si la edición de la lista de buena fe se encuentra habilitada
            // y la inscripción del equipo no está confirmada ni bloqueada.
            if (!equipoDestino.EdicionListaHabilitada || equipoDestino.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado)
            {
                throw new BusinessRuleConflictException("No se pueden agregar jugadores porque la edición de la lista de buena fe se encuentra deshabilitada o la inscripción del equipo ya fue confirmada/bloqueada.");
            }

            // Regla Estricta: Un jugador no puede estar inscrito en más de un equipo en un mismo torneo.
            var jugadoresEnTorneo = await _jugadorRepository.FindAsync(j => j.Dni == normalizedDni);
            
            foreach(var jugadorExistente in jugadoresEnTorneo)
            {
                var equipoExistente = await _equipoRepository.GetByIdAsync(jugadorExistente.EquipoId);
                if (equipoExistente != null && equipoExistente.TorneoId == equipoDestino.TorneoId)
                {
                    throw new BusinessRuleConflictException($"El jugador con DNI {normalizedDni} ya se encuentra inscrito en el equipo '{equipoExistente.Nombre}' del mismo torneo.");
                }
            }

            var jugador = new Jugador
            {
                Nombre = nombreTrim,
                Apellido = apellidoTrim,
                Dni = normalizedDni,
                DocumentacionUrl = dto.DocumentacionUrl?.Trim(),
                EquipoId = dto.EquipoId
            };

            await _jugadorRepository.AddAsync(jugador);
            return MapToResponseDTO(jugador);
        }

        public async Task DeleteAsync(Guid id, Guid? solicitanteId = null, string? rolSolicitante = null)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del jugador no puede ser vacío.");

            var jugador = await _jugadorRepository.GetByIdAsync(id);
            if (jugador == null)
                throw new NotFoundException($"No se encontró el jugador con ID {id}");

            var equipo = await _equipoRepository.GetByIdAsync(jugador.EquipoId);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo asociado al jugador con ID {id}");

            // Mitigación IDOR / Titularidad:
            // Si el solicitante no es Organizador ni Administrador, solo el capitán dueño del equipo puede eliminar jugadores.
            if (solicitanteId.HasValue && rolSolicitante != "Organizador" && rolSolicitante != "Administrador")
            {
                if (!equipo.CapitanId.HasValue || equipo.CapitanId.Value != solicitanteId.Value)
                {
                    throw new ForbiddenException("No tiene permisos para modificar o eliminar jugadores de un equipo que no le pertenece.");
                }
            }

            // RN-03: Solo se pueden eliminar jugadores si la edición de la lista de buena fe está habilitada
            // y el equipo no se encuentra ya confirmado.
            if (!equipo.EdicionListaHabilitada || equipo.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado)
            {
                throw new BusinessRuleConflictException("No se pueden eliminar ni modificar jugadores porque la edición de la lista de buena fe se encuentra deshabilitada.");
            }

            await _jugadorRepository.DeleteAsync(jugador);
        }

        public async Task<JugadorEstadisticasDTO> GetEstadisticasAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del jugador no puede ser vacío.");

            var jugador = await _jugadorRepository.GetByIdWithEquipoAndTorneoAsync(id);
            if (jugador == null)
            {
                jugador = await _jugadorRepository.GetByIdAsync(id);
            }
            if (jugador == null)
            {
                throw new NotFoundException($"No se encontró el jugador con ID {id}");
            }

            Equipo? equipoActual = jugador.Equipo;
            if (equipoActual == null && jugador.EquipoId != Guid.Empty)
            {
                equipoActual = await _equipoRepository.GetByIdAsync(jugador.EquipoId);
            }

            // Recuperar todas las incidencias del jugador (por Id o por DNI)
            var incidencias = new List<Incidencia>();
            if (_incidenciaRepository != null)
            {
                var porId = await _incidenciaRepository.GetByJugadorIdAsync(id);
                incidencias.AddRange(porId);

                if (!string.IsNullOrWhiteSpace(jugador.Dni))
                {
                    var porDni = await _incidenciaRepository.GetByJugadorDniAsync(jugador.Dni);
                    foreach (var inc in porDni)
                    {
                        if (!incidencias.Any(x => x.Id == inc.Id))
                        {
                            incidencias.Add(inc);
                        }
                    }
                }
            }

            // Historial de torneos disputados
            var torneosDisputados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (equipoActual?.Torneo != null && !string.IsNullOrWhiteSpace(equipoActual.Torneo.Nombre))
            {
                torneosDisputados.Add(equipoActual.Torneo.Nombre);
            }
            else if (equipoActual != null && equipoActual.TorneoId != Guid.Empty && _torneoRepository != null)
            {
                var t = await _torneoRepository.GetByIdAsync(equipoActual.TorneoId);
                if (t != null && !string.IsNullOrWhiteSpace(t.Nombre))
                {
                    torneosDisputados.Add(t.Nombre);
                }
            }

            foreach (var inc in incidencias)
            {
                if (inc.Partido?.Torneo != null && !string.IsNullOrWhiteSpace(inc.Partido.Torneo.Nombre))
                {
                    torneosDisputados.Add(inc.Partido.Torneo.Nombre);
                }
            }

            if (!string.IsNullOrWhiteSpace(jugador.Dni))
            {
                var otrosRegistros = await _jugadorRepository.FindAsync(j => j.Dni == jugador.Dni);
                foreach (var reg in otrosRegistros)
                {
                    if (reg.Equipo?.Torneo != null && !string.IsNullOrWhiteSpace(reg.Equipo.Torneo.Nombre))
                    {
                        torneosDisputados.Add(reg.Equipo.Torneo.Nombre);
                    }
                    else if (reg.EquipoId != Guid.Empty)
                    {
                        var eq = await _equipoRepository.GetByIdAsync(reg.EquipoId);
                        if (eq != null && eq.TorneoId != Guid.Empty && _torneoRepository != null)
                        {
                            var t = await _torneoRepository.GetByIdAsync(eq.TorneoId);
                            if (t != null && !string.IsNullOrWhiteSpace(t.Nombre))
                            {
                                torneosDisputados.Add(t.Nombre);
                            }
                        }
                    }
                }
            }

            int goles = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.Gol);
            int puntos = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.Punto);
            int amarillas = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.TarjetaAmarilla);
            int rojas = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.TarjetaRoja);
            int faltas = incidencias.Count(i => i.TipoIncidencia == TipoIncidencia.Falta);

            return MapToEstadisticasDTO(jugador, equipoActual, goles, puntos, amarillas, rojas, faltas, torneosDisputados);
        }

        private static JugadorEstadisticasDTO MapToEstadisticasDTO(
            Jugador jugador,
            Equipo? equipoActual,
            int goles,
            int puntos,
            int amarillas,
            int rojas,
            int faltas,
            IEnumerable<string> torneosDisputados)
        {
            var listaTorneos = torneosDisputados.ToList();
            return new JugadorEstadisticasDTO
            {
                JugadorId = jugador.Id,
                Nombre = jugador.Nombre,
                Apellido = jugador.Apellido,
                Dni = jugador.Dni,
                EquipoActualId = equipoActual?.Id ?? (jugador.EquipoId != Guid.Empty ? jugador.EquipoId : null),
                EquipoActual = equipoActual?.Nombre ?? "Sin Equipo Asignado",
                Goles = goles,
                Puntos = puntos,
                TarjetasAmarillas = amarillas,
                TarjetasRojas = rojas,
                Faltas = faltas,
                TorneosDisputados = listaTorneos.Count,
                HistorialTorneos = listaTorneos
            };
        }

        private JugadorResponseDTO MapToResponseDTO(Jugador jugador)
        {
            return new JugadorResponseDTO
            {
                Id = jugador.Id,
                Nombre = jugador.Nombre,
                Apellido = jugador.Apellido,
                Dni = jugador.Dni,
                DocumentacionUrl = jugador.DocumentacionUrl,
                EquipoId = jugador.EquipoId,
                Inhabilitado = jugador.Inhabilitado
            };
        }
    }
}

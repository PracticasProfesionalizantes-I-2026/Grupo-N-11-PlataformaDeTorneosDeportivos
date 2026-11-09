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
    public class EquipoService : IEquipoService
    {
        private readonly IEquipoRepository _equipoRepository;
        private readonly ITorneoRepository _torneoRepository;
        private readonly IJugadorRepository _jugadorRepository;
        private readonly IPartidoRepository? _partidoRepository;
        private readonly IInscripcionService? _inscripcionService;

        public EquipoService(
            IEquipoRepository equipoRepository,
            ITorneoRepository torneoRepository,
            IJugadorRepository jugadorRepository,
            IPartidoRepository? partidoRepository = null,
            IInscripcionService? inscripcionService = null)
        {
            _equipoRepository = equipoRepository;
            _torneoRepository = torneoRepository;
            _jugadorRepository = jugadorRepository;
            _partidoRepository = partidoRepository;
            _inscripcionService = inscripcionService;
        }

        public async Task<IEnumerable<EquipoResponseDTO>> GetByTorneoIdAsync(Guid torneoId)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            var equipos = await _equipoRepository.GetByTorneoIdWithJugadoresAsync(torneoId);
            return equipos.Select(MapToResponseDTO);
        }

        public async Task<IEnumerable<EquipoPublicResponseDTO>> GetByTorneoIdPublicAsync(Guid torneoId)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            var equipos = await _equipoRepository.GetByTorneoIdWithJugadoresAsync(torneoId);
            return equipos.Select(MapToPublicResponseDTO);
        }

        public async Task<IEnumerable<EquipoPublicResponseDTO>> GetAllPublicAsync()
        {
            var equipos = await _equipoRepository.GetAllWithJugadoresAsync();
            return equipos.Select(MapToPublicResponseDTO);
        }

        public async Task<EquipoPublicResponseDTO> GetByIdPublicAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var equipo = await _equipoRepository.GetByIdWithTorneoAndJugadoresAsync(id);
            if (equipo == null)
            {
                equipo = await _equipoRepository.GetByIdAsync(id);
                if (equipo == null)
                    throw new NotFoundException($"No se encontró el equipo con ID {id}");

                var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                equipo.Jugadores = jugadores.ToList();
            }

            return MapToPublicResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> GetByIdDetalladoAsync(Guid id, Guid? usuarioId, string? rol)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var equipo = await _equipoRepository.GetByIdWithTorneoAndJugadoresAsync(id);
            if (equipo == null)
            {
                equipo = await _equipoRepository.GetByIdAsync(id);
                if (equipo == null)
                    throw new NotFoundException($"No se encontró el equipo con ID {id}");

                var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                equipo.Jugadores = jugadores.ToList();
            }

            var isAuthorized = (rol == "Organizador" || rol == "Administrador") ||
                               (rol == "Capitan" && usuarioId.HasValue && equipo.CapitanId.HasValue && equipo.CapitanId.Value == usuarioId.Value);

            if (!isAuthorized)
            {
                throw new ForbiddenException("No tiene permisos para acceder a la nómina completa ni a los datos sensibles de este equipo.");
            }

            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var equipo = await _equipoRepository.GetByIdAsync(id);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo con ID {id}");

            var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
            equipo.Jugadores = jugadores.ToList();

            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> CreateAsync(EquipoCreateDTO dto)
        {
            if (dto == null)
                throw new ValidationException("Los datos del equipo son requeridos.");
            return await CreateEquipoAsync(dto.TorneoId, dto);
        }

        public async Task<EquipoResponseDTO> CreateEquipoAsync(EquipoCreateDTO dto)
        {
            if (dto == null)
                throw new ValidationException("Los datos del equipo son requeridos.");
            return await CreateEquipoAsync(dto.TorneoId, dto);
        }

        public async Task<EquipoResponseDTO> CreateEquipoAsync(Guid torneoId, EquipoCreateDTO dto, Guid? solicitanteId = null, string? rolSolicitante = null)
        {
            if (dto == null)
                throw new ValidationException("Los datos del equipo son requeridos.");

            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");

            if (solicitanteId.HasValue && rolSolicitante != "Organizador" && rolSolicitante != "Administrador")
            {
                if (dto.CapitanId.HasValue && dto.CapitanId.Value != solicitanteId.Value)
                {
                    throw new ForbiddenException("No tiene permisos para registrar un equipo a nombre de otro capitán.");
                }
                dto.CapitanId ??= solicitanteId.Value;
            }

            var nombreNormalizado = dto.Nombre?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nombreNormalizado) || nombreNormalizado.Length < 3 || nombreNormalizado.Length > 100)
                throw new ValidationException("El nombre del equipo es obligatorio y debe tener entre 3 y 100 caracteres.");

            if (nombreNormalizado.Contains('<') || nombreNormalizado.Contains('>') ||
                !System.Text.RegularExpressions.Regex.IsMatch(nombreNormalizado, @"^(?=.*[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ])[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.\(\)]+$"))
                throw new ValidationException("El nombre del equipo contiene caracteres no permitidos.");

            if (dto.Telefono != null)
            {
                var telTrim = dto.Telefono.Trim();
                if (string.IsNullOrWhiteSpace(telTrim) || !System.Text.RegularExpressions.Regex.IsMatch(telTrim, @"^\+?[0-9]{8,15}$"))
                    throw new ValidationException("El teléfono de contacto debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
            }

            if (dto.Email != null)
            {
                var emailTrim = dto.Email.Trim();
                if (string.IsNullOrWhiteSpace(emailTrim) || emailTrim.Length > 150 || emailTrim.Contains('<') || emailTrim.Contains('>') ||
                    !System.Text.RegularExpressions.Regex.IsMatch(emailTrim, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    throw new ValidationException("El formato del correo electrónico de contacto del equipo no es válido.");
            }

            if (dto.LogoUrl != null)
            {
                var logoUrlTrim = dto.LogoUrl.Trim();
                if (string.IsNullOrWhiteSpace(logoUrlTrim) || logoUrlTrim.Length > 500 || logoUrlTrim.Contains('<') || logoUrlTrim.Contains('>'))
                    throw new ValidationException("La URL del logo no es válida.");
            }

            if (dto.Logo != null)
            {
                var logoTrim = dto.Logo.Trim();
                if (string.IsNullOrWhiteSpace(logoTrim) || logoTrim.Length > 500 || logoTrim.Contains('<') || logoTrim.Contains('>'))
                    throw new ValidationException("El logo no es válido.");
            }

            if (dto.ComprobanteUrl != null)
            {
                var compUrlTrim = dto.ComprobanteUrl.Trim();
                if (string.IsNullOrWhiteSpace(compUrlTrim) || compUrlTrim.Length > 500 || compUrlTrim.Contains('<') || compUrlTrim.Contains('>'))
                    throw new ValidationException("El comprobante no es válido.");
            }

            if (dto.Comprobante != null)
            {
                var compTrim = dto.Comprobante.Trim();
                if (string.IsNullOrWhiteSpace(compTrim) || compTrim.Length > 500 || compTrim.Contains('<') || compTrim.Contains('>'))
                    throw new ValidationException("El comprobante no es válido.");
            }

            if (dto.Jugadores == null)
                throw new ValidationException("La lista de jugadores es obligatoria.");

            if (dto.Jugadores.Any(j => j == null))
                throw new ValidationException("La lista de jugadores no puede contener elementos nulos.");

            if (dto.Jugadores.Count > 30)
                throw new ValidationException("La lista de jugadores no puede superar el máximo de 30 integrantes.");

            // 3a. Verificar existencia del torneo
            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
                throw new TorneoNotFoundException("El torneo especificado no existe");

            // 3b. Verificar estado de inscripciones abiertas
            if (torneo.Estado != EstadoTorneo.Abierto)
            {
                throw new TorneoInscripcionCerradaException("El torneo no se encuentra disponible para nuevas inscripciones");
            }

            // 3g. RN-06: Verificar si el torneo ya alcanzó su cupo máximo de equipos confirmados
            if (torneo.CupoMaximo > 0)
            {
                var equiposConfirmados = await _equipoRepository.FindAsync(e =>
                    e.TorneoId == torneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);
                if (equiposConfirmados != null && equiposConfirmados.Count() >= torneo.CupoMaximo)
                {
                    throw new TorneoCupoLlenoException("No se puede registrar el equipo: el torneo ya ha alcanzado su cupo máximo de equipos.");
                }
            }

            // 3f. Verificar duplicidad de nombre en el mismo torneo
            var existeNombre = await _equipoRepository.ExistsNombreInTorneoAsync(torneoId, nombreNormalizado);
            if (existeNombre)
            {
                throw new EquipoNombreDuplicadoException("Ya existe un equipo con ese nombre en este torneo");
            }

            if (dto.Jugadores == null)
            {
                throw new ValidationException("La lista de jugadores es obligatoria.");
            }

            if (dto.Jugadores.Any(j => j == null))
            {
                throw new ValidationException("La lista de jugadores no puede contener elementos nulos.");
            }

            if (dto.Jugadores.Count > 30)
            {
                throw new ValidationException("La lista de jugadores no puede superar el máximo de 30 integrantes.");
            }

            // 3c. Validar mínimo reglamentario de jugadores según la disciplina
            var minimoRequerido = GetMinimoJugadoresPorDisciplina(torneo.Disciplina);
            if (dto.Jugadores.Count < minimoRequerido)
            {
                throw new MinimoJugadoresInsuficienteException("La lista debe contener al menos el número mínimo de jugadores requeridos para la disciplina");
            }

            // 3d. Validar duplicidad interna de DNI en la nómina enviada y formato de todas las propiedades del jugador
            foreach (var jugador in dto.Jugadores)
            {
                var dniTrim = jugador.Dni?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(dniTrim) || !dniTrim.All(c => c >= '0' && c <= '9'))
                {
                    throw new ValidationException("El DNI debe contener solo números.");
                }

                if (dniTrim.Length < 7 || dniTrim.Length > 8)
                {
                    throw new ValidationException("El DNI debe tener entre 7 y 8 dígitos numéricos.");
                }

                var nombreJugador = jugador.Nombre?.Trim() ?? string.Empty;
                var apellidoJugador = jugador.Apellido?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(nombreJugador) ||
                    nombreJugador.Length < 2 ||
                    nombreJugador.Length > 60 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(nombreJugador, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
                {
                    throw new ValidationException("El nombre del jugador debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
                }

                if (string.IsNullOrWhiteSpace(apellidoJugador) ||
                    apellidoJugador.Length < 2 ||
                    apellidoJugador.Length > 60 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(apellidoJugador, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$"))
                {
                    throw new ValidationException("El apellido del jugador debe tener entre 2 y 60 caracteres y contener únicamente letras y espacios.");
                }

                if (jugador.Telefono != null)
                {
                    var jTel = jugador.Telefono.Trim();
                    if (string.IsNullOrWhiteSpace(jTel) || !System.Text.RegularExpressions.Regex.IsMatch(jTel, @"^\+?[0-9]{8,15}$"))
                    {
                        throw new ValidationException("El teléfono del jugador debe contener únicamente entre 8 y 15 dígitos numéricos (opcionalmente precedidos por '+').");
                    }
                }

                if (jugador.Email != null)
                {
                    var jEmail = jugador.Email.Trim();
                    if (string.IsNullOrWhiteSpace(jEmail) || jEmail.Length > 150 || jEmail.Contains('<') || jEmail.Contains('>') ||
                        !System.Text.RegularExpressions.Regex.IsMatch(jEmail, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        throw new ValidationException("El correo electrónico del jugador no tiene un formato válido.");
                    }
                }

                if (jugador.Edad.HasValue && (jugador.Edad.Value < 5 || jugador.Edad.Value > 90))
                {
                    throw new ValidationException("La edad del jugador debe estar entre 5 y 90 años.");
                }

                if (jugador.FechaNacimiento.HasValue)
                {
                    var hoy = DateTime.Today;
                    var fn = jugador.FechaNacimiento.Value.Date;
                    int edadCalc = hoy.Year - fn.Year;
                    if (fn > hoy.AddYears(-edadCalc))
                        edadCalc--;
                    if (fn > hoy || edadCalc < 5 || edadCalc > 90)
                    {
                        throw new ValidationException("La fecha de nacimiento del jugador debe corresponder a una edad lógica entre 5 y 90 años.");
                    }
                }

                if (jugador.NumeroCamiseta.HasValue && (jugador.NumeroCamiseta.Value < 1 || jugador.NumeroCamiseta.Value > 99))
                {
                    throw new ValidationException("El número de camiseta debe estar entre 1 y 99.");
                }

                if (jugador.Dorsal.HasValue && (jugador.Dorsal.Value < 1 || jugador.Dorsal.Value > 99))
                {
                    throw new ValidationException("El dorsal debe estar entre 1 y 99.");
                }

                if (jugador.Posicion != null)
                {
                    var posTrim = jugador.Posicion.Trim();
                    if (string.IsNullOrWhiteSpace(posTrim) || posTrim.Length < 2 || posTrim.Length > 40 ||
                        !System.Text.RegularExpressions.Regex.IsMatch(posTrim, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-]+$"))
                    {
                        throw new ValidationException("La posición del jugador debe tener entre 2 y 40 caracteres y contener solo letras.");
                    }
                }

                if (jugador.DocumentacionUrl != null)
                {
                    var docTrim = jugador.DocumentacionUrl.Trim();
                    if (string.IsNullOrWhiteSpace(docTrim) || docTrim.Length > 500 || docTrim.Contains('<') || docTrim.Contains('>'))
                    {
                        throw new ValidationException("La URL de documentación no es válida o supera los 500 caracteres.");
                    }
                }

                if (jugador.FotoUrl != null)
                {
                    var fotoTrim = jugador.FotoUrl.Trim();
                    if (string.IsNullOrWhiteSpace(fotoTrim) || fotoTrim.Length > 500 || fotoTrim.Contains('<') || fotoTrim.Contains('>'))
                    {
                        throw new ValidationException("La URL de la foto no es válida o supera los 500 caracteres.");
                    }
                }
            }

            var dnisEnviados = dto.Jugadores.Select(j => j.Dni?.Trim() ?? string.Empty).ToList();
            if (dnisEnviados.Distinct(StringComparer.OrdinalIgnoreCase).Count() < dnisEnviados.Count)
            {
                throw new ValidationException("Se encontraron DNIs duplicados en la lista de jugadores enviada");
            }

            // 3e. RN-02: Validar que ningún jugador esté ya registrado en otro equipo del mismo torneo
            var jugadoresEnTorneo = await _jugadorRepository.GetJugadoresByTorneoIdAsync(torneoId);
            var dnisExistentes = jugadoresEnTorneo
                .Select(j => j.Dni.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var jugadorDto in dto.Jugadores)
            {
                var dni = jugadorDto.Dni.Trim();
                if (dnisExistentes.Contains(dni))
                {
                    throw new JugadorYaInscriptoException(dni);
                }
            }

            // 4. RN-03: Crear equipo con estado Pendiente de Validación
            var equipo = new Equipo
            {
                Id = Guid.NewGuid(),
                Nombre = nombreNormalizado,
                Logo = (dto.LogoUrl ?? dto.Logo)?.Trim(),
                ComprobanteUrl = (dto.ComprobanteUrl ?? dto.Comprobante)?.Trim(),
                PagoValidado = false,
                EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion,
                TorneoId = torneoId,
                CapitanId = dto.CapitanId,
                Jugadores = dto.Jugadores.Select(j => new Jugador
                {
                    Id = Guid.NewGuid(),
                    Nombre = j.Nombre.Trim(),
                    Apellido = j.Apellido.Trim(),
                    Dni = j.Dni.Trim(),
                    DocumentacionUrl = j.DocumentacionUrl?.Trim()
                }).ToList()
            };

            await _equipoRepository.AddAsync(equipo);

            return MapToResponseDTO(equipo);
        }

        public async Task<EquipoResponseDTO> CambiarEstadoAsync(Guid id, string nuevoEstadoStr)
        {
            if (_inscripcionService != null)
            {
                return await _inscripcionService.CambiarEstadoEquipoAsync(id, nuevoEstadoStr);
            }

            if (id == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            if (string.IsNullOrWhiteSpace(nuevoEstadoStr) ||
                !Enum.TryParse<EstadoInscripcion>(nuevoEstadoStr.Trim(), true, out var nuevoEstado) ||
                !Enum.IsDefined(typeof(EstadoInscripcion), nuevoEstado))
            {
                throw new ValidationException($"El estado '{nuevoEstadoStr}' no es válido.");
            }

            var equipo = await _equipoRepository.GetByIdAsync(id);
            if (equipo == null)
                throw new NotFoundException($"No se encontró el equipo con ID {id}");

            var torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
            if (torneo == null)
                throw new NotFoundException($"El torneo con ID {equipo.TorneoId} no existe.");

            var estadoActual = equipo.EstadoInscripcion;

            if (nuevoEstado == EstadoInscripcion.InscritoYConfirmado)
            {
                if (estadoActual != EstadoInscripcion.PendienteDeValidacion)
                {
                    throw new EstadoEquipoInvalidoException("Solo pueden dictaminarse equipos en estado Pendiente de Validación");
                }

                if (torneo.Estado != EstadoTorneo.Abierto)
                {
                    throw new TorneoEstadoInvalidoException("El torneo no se encuentra abierto para confirmar inscripciones.");
                }

                var esTorneoPago = torneo.EsPago || torneo.CostoInscripcion > 0;
                if (esTorneoPago && (!equipo.PagoValidado || string.IsNullOrWhiteSpace(equipo.ComprobanteUrl)))
                {
                    throw new PagoRequeridoException("No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)");
                }

                var equiposConfirmados = await _equipoRepository.FindAsync(e => e.TorneoId == equipo.TorneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);
                var cantidadConfirmados = equiposConfirmados.Count();

                if (cantidadConfirmados >= torneo.CupoMaximo)
                {
                    throw new TorneoCupoLlenoException("El torneo ya ha alcanzado su cupo máximo de equipos confirmados (RN-06).");
                }

                var jugadoresDelEquipo = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                if (!jugadoresDelEquipo.Any())
                {
                    throw new BusinessRuleConflictException("El equipo no cumple con el mínimo de jugadores requeridos (Mínimo 1) para ser confirmado.");
                }

                equipo.EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado;
                equipo.EdicionListaHabilitada = false;
                equipo.MotivoRechazo = null;

                if (cantidadConfirmados + 1 >= torneo.CupoMaximo && torneo.Estado == EstadoTorneo.Abierto)
                {
                    torneo.Estado = EstadoTorneo.InscripcionesCerradas;
                }
                await _torneoRepository.UpdateAsync(torneo);
            }
            else if (nuevoEstado == EstadoInscripcion.RechazadoObservado)
            {
                if (estadoActual != EstadoInscripcion.PendienteDeValidacion)
                {
                    throw new EstadoEquipoInvalidoException("Solo pueden dictaminarse equipos en estado Pendiente de Validación");
                }

                equipo.EstadoInscripcion = EstadoInscripcion.RechazadoObservado;
                equipo.EdicionListaHabilitada = true;
            }
            else if (nuevoEstado == EstadoInscripcion.PendienteDeValidacion)
            {
                if (estadoActual == EstadoInscripcion.InscritoYConfirmado)
                {
                    throw new EstadoEquipoInvalidoException("Un equipo ya confirmado no puede volver a estar pendiente de validación.");
                }

                if (estadoActual == EstadoInscripcion.PendienteDeValidacion)
                {
                    throw new EstadoEquipoInvalidoException("El equipo ya se encuentra en estado Pendiente de Validación.");
                }

                equipo.EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion;
                equipo.EdicionListaHabilitada = false;
            }

            await _equipoRepository.UpdateAsync(equipo);

            var jugadoresLista = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
            equipo.Jugadores = jugadoresLista.ToList();

            return MapToResponseDTO(equipo);
        }

        private static int GetMinimoJugadoresPorDisciplina(Disciplina disciplina) => disciplina switch
        {
            Disciplina.Futbol => 5,
            Disciplina.ESports => 3,
            _ => 1
        };

        public async Task<EquipoEstadisticasDTO> GetEstadisticasAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var equipo = await _equipoRepository.GetByIdWithTorneoAndJugadoresAsync(id);
            if (equipo == null)
            {
                equipo = await _equipoRepository.GetByIdAsync(id);
            }
            if (equipo == null)
            {
                throw new NotFoundException($"No se encontró el equipo con ID {id}");
            }

            Torneo? torneo = equipo.Torneo;
            if (torneo == null && equipo.TorneoId != Guid.Empty)
            {
                torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
            }

            var jugadores = equipo.Jugadores?.ToList() ?? new List<Jugador>();
            if (jugadores.Count == 0)
            {
                var recuperados = await _jugadorRepository.FindAsync(j => j.EquipoId == id);
                jugadores = recuperados.ToList();
            }

            IEnumerable<Partido> partidos = _partidoRepository != null
                ? await _partidoRepository.GetPartidosByEquipoIdAsync(id)
                : Enumerable.Empty<Partido>();

            var listaPartidos = partidos.ToList();
            if (listaPartidos.Count == 0 && _partidoRepository != null && equipo.TorneoId != Guid.Empty)
            {
                var partidosTorneo = await _partidoRepository.GetPartidosByTorneoIdAsync(equipo.TorneoId);
                listaPartidos = partidosTorneo
                    .Where(p => p.EquipoLocalId == id || p.EquipoVisitanteId == id)
                    .ToList();
            }

            int pj = 0;
            int pg = 0;
            int pe = 0;
            int pp = 0;
            int gf = 0;
            int gc = 0;

            var partidosFinalizados = listaPartidos
                .Where(p => p.Estado == EstadoPartido.Finalizado || (p.MarcadorLocal.HasValue && p.MarcadorVisitante.HasValue))
                .ToList();

            foreach (var p in partidosFinalizados)
            {
                pj++;
                bool esLocal = p.EquipoLocalId == id;
                int golesFavor = esLocal ? (p.MarcadorLocal ?? 0) : (p.MarcadorVisitante ?? 0);
                int golesContra = esLocal ? (p.MarcadorVisitante ?? 0) : (p.MarcadorLocal ?? 0);

                gf += golesFavor;
                gc += golesContra;

                if (golesFavor > golesContra)
                {
                    pg++;
                }
                else if (golesFavor < golesContra)
                {
                    pp++;
                }
                else
                {
                    if (p.PenalesLocal.HasValue && p.PenalesVisitante.HasValue)
                    {
                        int penFavor = esLocal ? p.PenalesLocal.Value : p.PenalesVisitante.Value;
                        int penContra = esLocal ? p.PenalesVisitante.Value : p.PenalesLocal.Value;
                        if (penFavor > penContra)
                            pg++;
                        else if (penFavor < penContra)
                            pp++;
                        else
                            pe++;
                    }
                    else
                    {
                        pe++;
                    }
                }
            }

            int dif = gf - gc;
            double efectividad = pj > 0 ? Math.Round(((double)(pg * 3 + pe) / (pj * 3)) * 100, 2) : 0.0;
            double porcentajeVictorias = pj > 0 ? Math.Round(((double)pg / pj) * 100, 2) : 0.0;

            var torneosHistorial = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (torneo != null && !string.IsNullOrWhiteSpace(torneo.Nombre))
            {
                torneosHistorial.Add(torneo.Nombre);
            }
            foreach (var p in listaPartidos)
            {
                if (p.Torneo != null && !string.IsNullOrWhiteSpace(p.Torneo.Nombre))
                {
                    torneosHistorial.Add(p.Torneo.Nombre);
                }
            }

            return MapToEstadisticasDTO(
                equipo, 
                torneo, 
                pj, 
                pg, 
                pe, 
                pp, 
                gf, 
                gc, 
                dif, 
                efectividad, 
                porcentajeVictorias, 
                torneosHistorial, 
                jugadores);
        }

        private static EquipoEstadisticasDTO MapToEstadisticasDTO(
            Equipo equipo,
            Torneo? torneo,
            int pj,
            int pg,
            int pe,
            int pp,
            int gf,
            int gc,
            int dif,
            double efectividad,
            double porcentajeVictorias,
            IEnumerable<string> torneosHistorial,
            IEnumerable<Jugador> jugadores)
        {
            var listaTorneos = torneosHistorial.ToList();
            var listaIntegrantes = jugadores.Select(j => new JugadorResponseDTO
            {
                Id = j.Id,
                Nombre = j.Nombre,
                Apellido = j.Apellido,
                Dni = j.Dni,
                DocumentacionUrl = j.DocumentacionUrl,
                EquipoId = j.EquipoId
            }).ToList();

            return new EquipoEstadisticasDTO
            {
                EquipoId = equipo.Id,
                Nombre = equipo.Nombre,
                Logo = equipo.Logo,
                TorneoActualId = equipo.TorneoId,
                TorneoActual = torneo?.Nombre ?? "Sin Torneo Asignado",
                PartidosJugados = pj,
                Victorias = pg,
                Empates = pe,
                Derrotas = pp,
                GolesAFavor = gf,
                GolesEnContra = gc,
                DiferenciaGoles = dif,
                Efectividad = efectividad,
                PorcentajeVictorias = porcentajeVictorias,
                TorneosDisputados = listaTorneos.Count,
                HistorialTorneos = listaTorneos,
                Integrantes = listaIntegrantes
            };
        }

        private static EquipoResponseDTO MapToResponseDTO(Equipo equipo)
        {
            return new EquipoResponseDTO
            {
                Id = equipo.Id,
                Nombre = equipo.Nombre,
                Logo = equipo.Logo,
                ComprobanteUrl = equipo.ComprobanteUrl,
                PagoValidado = equipo.PagoValidado,
                EdicionListaHabilitada = equipo.EdicionListaHabilitada,
                MotivoRechazo = equipo.MotivoRechazo,
                EstadoInscripcion = equipo.EstadoInscripcion.ToString(),
                TorneoId = equipo.TorneoId,
                CapitanId = equipo.CapitanId,
                CantidadJugadores = equipo.Jugadores?.Count ?? 0,
                Jugadores = equipo.Jugadores?.Select(j => new JugadorResponseDTO
                {
                    Id = j.Id,
                    Nombre = j.Nombre,
                    Apellido = j.Apellido,
                    Dni = j.Dni,
                    DocumentacionUrl = j.DocumentacionUrl,
                    EquipoId = j.EquipoId,
                    Inhabilitado = j.Inhabilitado
                }).ToList() ?? new List<JugadorResponseDTO>()
            };
        }

        private static EquipoPublicResponseDTO MapToPublicResponseDTO(Equipo equipo)
        {
            return new EquipoPublicResponseDTO
            {
                Id = equipo.Id,
                Nombre = equipo.Nombre,
                Logo = equipo.Logo,
                EstadoInscripcion = equipo.EstadoInscripcion.ToString(),
                TorneoId = equipo.TorneoId,
                CantidadJugadores = equipo.Jugadores?.Count ?? 0,
                Jugadores = equipo.Jugadores?.Select(j => new JugadorPublicResponseDTO
                {
                    Id = j.Id,
                    Nombre = j.Nombre,
                    Apellido = j.Apellido,
                    EquipoId = j.EquipoId,
                    Inhabilitado = j.Inhabilitado
                }).ToList() ?? new List<JugadorPublicResponseDTO>()
            };
        }
    }
}

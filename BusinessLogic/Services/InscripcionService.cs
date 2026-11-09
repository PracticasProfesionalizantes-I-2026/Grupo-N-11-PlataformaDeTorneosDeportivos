using System;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Entities;
using DataAccess.Repositories;
using Shared.DTOs;
using Shared.Enums;
using Shared.Exceptions;

namespace BusinessLogic.Services
{
    public class InscripcionService : IInscripcionService
    {
        private readonly ITorneoRepository _torneoRepository;
        private readonly IEquipoRepository _equipoRepository;
        private readonly IUnitOfWork? _unitOfWork;
        private readonly IJugadorRepository? _jugadorRepository;

        public InscripcionService(
            ITorneoRepository torneoRepository,
            IEquipoRepository equipoRepository,
            IUnitOfWork? unitOfWork = null,
            IJugadorRepository? jugadorRepository = null)
        {
            _torneoRepository = torneoRepository;
            _equipoRepository = equipoRepository;
            _unitOfWork = unitOfWork;
            _jugadorRepository = jugadorRepository;
        }

        public async Task<AprobarInscripcionResponseDTO> AprobarInscripcionAsync(Guid torneoId, Guid equipoId)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            // 2a. Verificar existencia del torneo
            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
            {
                throw new RecursoNotFoundException($"El torneo con ID {torneoId} no existe.");
            }

            // 2a. Verificar existencia del equipo
            var equipo = await _equipoRepository.GetByIdAsync(equipoId);
            if (equipo == null || equipo.TorneoId != torneoId)
            {
                throw new RecursoNotFoundException($"El equipo con ID {equipoId} no existe en este torneo.");
            }

            // 3a. Verificar que el equipo esté en estado Pendiente de Validación
            if (equipo.EstadoInscripcion != EstadoInscripcion.PendienteDeValidacion)
            {
                throw new EstadoEquipoInvalidoException("Solo pueden dictaminarse equipos en estado Pendiente de Validación");
            }

            // 3b. RN-06: Validar que el torneo no haya alcanzado el cupo máximo
            var equiposConfirmados = await _equipoRepository.FindAsync(e =>
                e.TorneoId == torneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);
            var cantidadConfirmados = equiposConfirmados.Count();

            if (cantidadConfirmados >= torneo.CupoMaximo)
            {
                throw new TorneoCupoLlenoException("No se puede confirmar la inscripción: el cupo del torneo se encuentra completo (RN-06)");
            }

            // 3c. RN-06: En torneos pagos, verificar comprobante de pago validado
            var esTorneoPago = torneo.EsPago || torneo.CostoInscripcion > 0;
            if (esTorneoPago && (!equipo.PagoValidado || string.IsNullOrWhiteSpace(equipo.ComprobanteUrl)))
            {
                throw new PagoRequeridoException("No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)");
            }

            // Actualizar estado del equipo a Inscrito y Confirmado
            equipo.EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado;
            equipo.EdicionListaHabilitada = false;

            var cuposRestantes = Math.Max(0, torneo.CupoMaximo - (cantidadConfirmados + 1));
            if (cuposRestantes == 0 && torneo.Estado == EstadoTorneo.Abierto)
            {
                torneo.Estado = EstadoTorneo.InscripcionesCerradas;
            }

            // Transacción ACID (Unit of Work): Asegura que la confirmación del equipo y la actualización del torneo
            // (cupo y token de concurrencia optimista) se persistan atómicamente con rollback ante cualquier fallo.
            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await _torneoRepository.UpdateAsync(torneo);
                    await _equipoRepository.UpdateAsync(equipo);
                    await _unitOfWork.CommitTransactionAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            else
            {
                await _torneoRepository.UpdateAsync(torneo);
                await _equipoRepository.UpdateAsync(equipo);
            }

            return MapToAprobarResponseDTO(equipo, torneo.Id, cuposRestantes);
        }

        public async Task<RechazarInscripcionResponseDTO> RechazarInscripcionAsync(Guid torneoId, Guid equipoId, RechazarInscripcionDTO dto)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            if (dto == null)
            {
                throw new ValidationException("El motivo de rechazo es obligatorio.");
            }

            var rawMotivo = !string.IsNullOrWhiteSpace(dto.MotivoRechazo) ? dto.MotivoRechazo : dto.Motivo;
            if (string.IsNullOrWhiteSpace(rawMotivo))
            {
                throw new ValidationException("El motivo de rechazo es obligatorio.");
            }

            var motivoTrim = rawMotivo.Trim();
            if (motivoTrim.Length < 3 || motivoTrim.Length > 500)
            {
                throw new ValidationException("El motivo de rechazo debe tener entre 3 y 500 caracteres.");
            }

            if (motivoTrim.Contains('<') || motivoTrim.Contains('>'))
            {
                throw new ValidationException("El motivo de rechazo contiene caracteres no permitidos.");
            }

            // 2a. Verificar existencia del torneo
            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
            {
                throw new RecursoNotFoundException($"El torneo con ID {torneoId} no existe.");
            }

            // 2a. Verificar existencia del equipo
            var equipo = await _equipoRepository.GetByIdAsync(equipoId);
            if (equipo == null || equipo.TorneoId != torneoId)
            {
                throw new RecursoNotFoundException($"El equipo con ID {equipoId} no existe en este torneo.");
            }

            // 3a. Verificar que el equipo esté en estado Pendiente de Validación
            if (equipo.EstadoInscripcion != EstadoInscripcion.PendienteDeValidacion)
            {
                throw new EstadoEquipoInvalidoException("Solo pueden dictaminarse equipos en estado Pendiente de Validación");
            }

            // 3d. RN-03: Rechazo con observaciones y liberación de edición de lista bajo transacción
            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    equipo.EstadoInscripcion = EstadoInscripcion.RechazadoObservado;
                    equipo.MotivoRechazo = motivoTrim;
                    equipo.EdicionListaHabilitada = true; // RN-03: libera edición para el capitán
                    await _equipoRepository.UpdateAsync(equipo);
                    await _unitOfWork.CommitTransactionAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            else
            {
                equipo.EstadoInscripcion = EstadoInscripcion.RechazadoObservado;
                equipo.MotivoRechazo = motivoTrim;
                equipo.EdicionListaHabilitada = true;
                await _equipoRepository.UpdateAsync(equipo);
            }

            return MapToRechazarResponseDTO(equipo, torneo.Id);
        }

        private static AprobarInscripcionResponseDTO MapToAprobarResponseDTO(Equipo equipo, Guid torneoId, int cuposDisponibles)
        {
            return new AprobarInscripcionResponseDTO
            {
                EquipoId = equipo.Id,
                NombreEquipo = equipo.Nombre,
                EstadoInscripcion = equipo.EstadoInscripcion.ToString(),
                TorneoId = torneoId,
                CuposDisponibles = cuposDisponibles,
                Mensaje = "La inscripción del equipo ha sido confirmada exitosamente."
            };
        }

        private static RechazarInscripcionResponseDTO MapToRechazarResponseDTO(Equipo equipo, Guid torneoId)
        {
            return new RechazarInscripcionResponseDTO
            {
                EquipoId = equipo.Id,
                NombreEquipo = equipo.Nombre,
                EstadoInscripcion = equipo.EstadoInscripcion.ToString(),
                TorneoId = torneoId,
                MotivoRechazo = equipo.MotivoRechazo ?? string.Empty,
                EdicionListaHabilitada = equipo.EdicionListaHabilitada,
                Mensaje = "La inscripción ha sido observada/rechazada y se habilitó la edición de la lista de buena fe para subsanación."
            };
        }

        public async Task<EquipoResponseDTO> CambiarEstadoEquipoAsync(Guid equipoId, string nuevoEstadoStr, string? motivoRechazo = null)
        {
            if (equipoId == Guid.Empty)
            {
                throw new ValidationException("El identificador del equipo no puede ser vacío.");
            }

            if (string.IsNullOrWhiteSpace(nuevoEstadoStr) ||
                !Enum.TryParse<EstadoInscripcion>(nuevoEstadoStr.Trim(), true, out var nuevoEstado) ||
                !Enum.IsDefined(typeof(EstadoInscripcion), nuevoEstado))
            {
                throw new ValidationException($"El estado '{nuevoEstadoStr}' no es válido.");
            }

            var equipo = await _equipoRepository.GetByIdAsync(equipoId);
            if (equipo == null)
            {
                throw new NotFoundException($"No se encontró el equipo con ID {equipoId}");
            }

            var torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
            if (torneo == null)
            {
                throw new NotFoundException($"El torneo con ID {equipo.TorneoId} no existe.");
            }

            var estadoActual = equipo.EstadoInscripcion;

            if (nuevoEstado == EstadoInscripcion.InscritoYConfirmado)
            {
                // Solo se puede dictaminar a InscritoYConfirmado si se encuentra en PendienteDeValidacion
                if (estadoActual != EstadoInscripcion.PendienteDeValidacion)
                {
                    throw new EstadoEquipoInvalidoException("Solo pueden dictaminarse equipos en estado Pendiente de Validación");
                }

                if (torneo.Estado != EstadoTorneo.Abierto)
                {
                    throw new TorneoEstadoInvalidoException("El torneo no se encuentra abierto para confirmar inscripciones.");
                }

                // RN-06: Torneo pago requiere comprobante validado
                var esTorneoPago = torneo.EsPago || torneo.CostoInscripcion > 0;
                if (esTorneoPago && (!equipo.PagoValidado || string.IsNullOrWhiteSpace(equipo.ComprobanteUrl)))
                {
                    throw new PagoRequeridoException("No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)");
                }

                // RN-06: Validar cupo máximo
                var confirmados = await _equipoRepository.FindAsync(e =>
                    e.TorneoId == equipo.TorneoId && e.EstadoInscripcion == EstadoInscripcion.InscritoYConfirmado);
                var cantidadConfirmados = confirmados.Count();

                if (cantidadConfirmados >= torneo.CupoMaximo)
                {
                    throw new TorneoCupoLlenoException("El torneo ya ha alcanzado su cupo máximo de equipos confirmados (RN-06).");
                }

                // Validar mínimo de jugadores si está disponible el repositorio
                if (_jugadorRepository != null)
                {
                    var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipo.Id);
                    if (!jugadores.Any())
                    {
                        throw new BusinessRuleConflictException("El equipo no cumple con el mínimo de jugadores requeridos (Mínimo 1) para ser confirmado.");
                    }
                }

                equipo.EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado;
                equipo.EdicionListaHabilitada = false;
                equipo.MotivoRechazo = null;

                if (cantidadConfirmados + 1 >= torneo.CupoMaximo && torneo.Estado == EstadoTorneo.Abierto)
                {
                    torneo.Estado = EstadoTorneo.InscripcionesCerradas;
                }
            }
            else if (nuevoEstado == EstadoInscripcion.RechazadoObservado)
            {
                // Solo se puede rechazar/observar si se encuentra en PendienteDeValidacion
                if (estadoActual != EstadoInscripcion.PendienteDeValidacion)
                {
                    throw new EstadoEquipoInvalidoException("Solo pueden dictaminarse equipos en estado Pendiente de Validación");
                }

                equipo.EstadoInscripcion = EstadoInscripcion.RechazadoObservado;
                equipo.EdicionListaHabilitada = true; // RN-03: habilita la edición para subsanar
                if (!string.IsNullOrWhiteSpace(motivoRechazo))
                {
                    equipo.MotivoRechazo = motivoRechazo.Trim();
                }
            }
            else if (nuevoEstado == EstadoInscripcion.PendienteDeValidacion)
            {
                // Un equipo ya confirmado no puede volver a estar pendiente
                if (estadoActual == EstadoInscripcion.InscritoYConfirmado)
                {
                    throw new EstadoEquipoInvalidoException("Un equipo ya confirmado no puede volver a estar pendiente de validación.");
                }

                if (estadoActual == EstadoInscripcion.PendienteDeValidacion)
                {
                    throw new EstadoEquipoInvalidoException("El equipo ya se encuentra en estado Pendiente de Validación.");
                }

                // Vuelve a enviar la lista corregida -> se bloquea la edición
                equipo.EstadoInscripcion = EstadoInscripcion.PendienteDeValidacion;
                equipo.EdicionListaHabilitada = false;
            }

            // Persistencia transaccional con IUnitOfWork
            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await _torneoRepository.UpdateAsync(torneo);
                    await _equipoRepository.UpdateAsync(equipo);
                    await _unitOfWork.CommitTransactionAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            else
            {
                await _torneoRepository.UpdateAsync(torneo);
                await _equipoRepository.UpdateAsync(equipo);
            }

            // Cargar jugadores si está disponible para el DTO de respuesta
            if (_jugadorRepository != null)
            {
                var jugadores = await _jugadorRepository.FindAsync(j => j.EquipoId == equipo.Id);
                equipo.Jugadores = jugadores.ToList();
            }

            return MapToEquipoResponseDTO(equipo);
        }

        public async Task<ValidarPagoResponseDTO> ValidarPagoAsync(Guid torneoId, Guid equipoId, ValidarPagoDTO? dto = null)
        {
            if (torneoId == Guid.Empty)
                throw new ValidationException("El identificador del torneo no puede ser vacío.");
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var torneo = await _torneoRepository.GetByIdAsync(torneoId);
            if (torneo == null)
            {
                throw new RecursoNotFoundException($"El torneo con ID {torneoId} no existe.");
            }

            var equipo = await _equipoRepository.GetByIdAsync(equipoId);
            if (equipo == null || equipo.TorneoId != torneoId)
            {
                throw new RecursoNotFoundException($"El equipo con ID {equipoId} no existe en este torneo.");
            }

            return await EjecutarValidacionPagoAsync(torneo, equipo, dto);
        }

        public async Task<ValidarPagoResponseDTO> ValidarPagoEquipoAsync(Guid equipoId, ValidarPagoDTO? dto = null)
        {
            if (equipoId == Guid.Empty)
                throw new ValidationException("El identificador del equipo no puede ser vacío.");

            var equipo = await _equipoRepository.GetByIdAsync(equipoId);
            if (equipo == null)
            {
                throw new RecursoNotFoundException($"No se encontró el equipo con ID {equipoId}");
            }

            var torneo = await _torneoRepository.GetByIdAsync(equipo.TorneoId);
            if (torneo == null)
            {
                throw new RecursoNotFoundException($"El torneo con ID {equipo.TorneoId} no existe.");
            }

            return await EjecutarValidacionPagoAsync(torneo, equipo, dto);
        }

        private async Task<ValidarPagoResponseDTO> EjecutarValidacionPagoAsync(Torneo torneo, Equipo equipo, ValidarPagoDTO? dto)
        {
            var rawComprobante = dto?.ComprobanteUrl ?? dto?.Comprobante;
            if (!string.IsNullOrWhiteSpace(rawComprobante))
            {
                var compTrim = rawComprobante.Trim();
                if (compTrim.Length > 500 || compTrim.Contains('<') || compTrim.Contains('>'))
                {
                    throw new ValidationException("La URL del comprobante no es válida.");
                }
                equipo.ComprobanteUrl = compTrim;
            }

            var esTorneoPago = torneo.EsPago || torneo.CostoInscripcion > 0;
            if (esTorneoPago && string.IsNullOrWhiteSpace(equipo.ComprobanteUrl))
            {
                throw new PagoRequeridoException("No es posible validar el pago sin un comprobante de pago cargado.");
            }

            equipo.PagoValidado = true;

            if (_unitOfWork != null)
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await _equipoRepository.UpdateAsync(equipo);
                    await _unitOfWork.CommitTransactionAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw;
                }
            }
            else
            {
                await _equipoRepository.UpdateAsync(equipo);
            }

            return new ValidarPagoResponseDTO
            {
                EquipoId = equipo.Id,
                NombreEquipo = equipo.Nombre,
                TorneoId = torneo.Id,
                ComprobanteUrl = equipo.ComprobanteUrl,
                PagoValidado = equipo.PagoValidado,
                Mensaje = "El comprobante de pago ha sido verificado y acreditado exitosamente."
            };
        }

        private static EquipoResponseDTO MapToEquipoResponseDTO(Equipo equipo)
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
                CantidadJugadores = equipo.Jugadores?.Count ?? 0,
                Jugadores = equipo.Jugadores?.Select(j => new JugadorResponseDTO
                {
                    Id = j.Id,
                    Nombre = j.Nombre,
                    Apellido = j.Apellido,
                    Dni = j.Dni,
                    DocumentacionUrl = j.DocumentacionUrl,
                    EquipoId = j.EquipoId
                }).ToList() ?? new List<JugadorResponseDTO>()
            };
        }
    }
}

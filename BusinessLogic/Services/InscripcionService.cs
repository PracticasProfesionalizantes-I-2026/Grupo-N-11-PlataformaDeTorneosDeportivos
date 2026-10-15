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

        public InscripcionService(
            ITorneoRepository torneoRepository,
            IEquipoRepository equipoRepository)
        {
            _torneoRepository = torneoRepository;
            _equipoRepository = equipoRepository;
        }

        public async Task<AprobarInscripcionResponseDTO> AprobarInscripcionAsync(Guid torneoId, Guid equipoId)
        {
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
            if (esTorneoPago && !equipo.PagoValidado)
            {
                throw new PagoRequeridoException("No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)");
            }

            // Actualizar estado del equipo a Inscrito y Confirmado
            equipo.EstadoInscripcion = EstadoInscripcion.InscritoYConfirmado;
            equipo.EdicionListaHabilitada = false;
            await _equipoRepository.UpdateAsync(equipo);

            var cuposRestantes = Math.Max(0, torneo.CupoMaximo - (cantidadConfirmados + 1));
            return MapToAprobarResponseDTO(equipo, torneo.Id, cuposRestantes);
        }

        public async Task<RechazarInscripcionResponseDTO> RechazarInscripcionAsync(Guid torneoId, Guid equipoId, RechazarInscripcionDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.MotivoRechazo))
            {
                throw new ValidationException("El motivo de rechazo es obligatorio.");
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

            // 3d. RN-03: Rechazo con observaciones y liberación de edición de lista
            equipo.EstadoInscripcion = EstadoInscripcion.RechazadoObservado;
            equipo.MotivoRechazo = dto.MotivoRechazo.Trim();
            equipo.EdicionListaHabilitada = true; // RN-03: libera edición para el capitán
            await _equipoRepository.UpdateAsync(equipo);

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
    }
}

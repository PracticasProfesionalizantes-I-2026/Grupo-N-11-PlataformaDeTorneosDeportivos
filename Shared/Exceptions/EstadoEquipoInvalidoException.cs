using System;

namespace Shared.Exceptions
{
    public class EstadoEquipoInvalidoException : BusinessRuleConflictException
    {
        public EstadoEquipoInvalidoException(string message = "Solo pueden dictaminarse equipos en estado Pendiente de Validación") 
            : base(message)
        {
        }
    }
}

using System;

namespace Shared.Exceptions
{
    public class JugadorInhabilitadoException : BusinessRuleConflictException
    {
        public JugadorInhabilitadoException(string message = "El jugador seleccionado se encuentra inhabilitado para este partido por sanción previa") 
            : base(message)
        {
        }
    }
}

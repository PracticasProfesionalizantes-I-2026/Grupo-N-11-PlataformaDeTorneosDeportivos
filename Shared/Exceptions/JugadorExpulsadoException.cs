using System;

namespace Shared.Exceptions
{
    public class JugadorExpulsadoException : BusinessRuleConflictException
    {
        public JugadorExpulsadoException(string message = "No es posible registrar incidencias para un jugador expulsado (RN-07)") 
            : base(message)
        {
        }
    }
}

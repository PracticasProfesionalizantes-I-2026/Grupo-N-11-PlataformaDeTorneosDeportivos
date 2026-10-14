using System;

namespace Shared.Exceptions
{
    public class TorneoEstadoInvalidoException : BusinessRuleConflictException
    {
        public TorneoEstadoInvalidoException(string message = "El torneo no se encuentra en un estado apto para generar el fixture.")
            : base(message)
        {
        }
    }
}

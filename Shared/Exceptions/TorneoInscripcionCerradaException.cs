using System;

namespace Shared.Exceptions
{
    public class TorneoInscripcionCerradaException : BusinessRuleConflictException
    {
        public TorneoInscripcionCerradaException(string message = "El torneo no se encuentra disponible para nuevas inscripciones") 
            : base(message)
        {
        }
    }
}

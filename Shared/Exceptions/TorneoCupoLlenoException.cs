using System;

namespace Shared.Exceptions
{
    public class TorneoCupoLlenoException : BusinessRuleConflictException
    {
        public TorneoCupoLlenoException(string message = "No se puede confirmar la inscripción: el cupo del torneo se encuentra completo (RN-06)") 
            : base(message)
        {
        }
    }
}

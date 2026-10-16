using System;

namespace Shared.Exceptions
{
    public class EmpateNoPermitidoException : BusinessRuleConflictException
    {
        public EmpateNoPermitidoException(string message = "Esta modalidad no admite empates. Debe definir el ganador (Penales/Tiempo Extra)") 
            : base(message)
        {
        }
    }
}

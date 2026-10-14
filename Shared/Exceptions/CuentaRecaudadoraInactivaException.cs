using System;

namespace Shared.Exceptions
{
    public class CuentaRecaudadoraInactivaException : BusinessRuleConflictException
    {
        public CuentaRecaudadoraInactivaException(string message = "No se puede publicar un torneo pago sin una cuenta recaudadora activa (RN-05)") : base(message)
        {
        }
    }
}

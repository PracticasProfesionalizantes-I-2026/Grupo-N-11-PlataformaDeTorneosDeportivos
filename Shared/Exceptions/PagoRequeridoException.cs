using System;

namespace Shared.Exceptions
{
    public class PagoRequeridoException : Exception
    {
        public PagoRequeridoException(string message = "No es posible confirmar la inscripción sin acreditar el pago correspondiente (RN-06)") 
            : base(message)
        {
        }
    }
}

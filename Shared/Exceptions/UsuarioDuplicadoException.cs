using System;

namespace Shared.Exceptions
{
    public class UsuarioDuplicadoException : BusinessRuleConflictException
    {
        public UsuarioDuplicadoException(string message = "El correo o DNI ya se encuentra registrado en la plataforma.") 
            : base(message)
        {
        }
    }
}

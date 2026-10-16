using System;

namespace Shared.Exceptions
{
    public class PublicidadNotFoundException : NotFoundException
    {
        public PublicidadNotFoundException(string message = "El banner publicitario especificado no existe.") 
            : base(message)
        {
        }
    }
}

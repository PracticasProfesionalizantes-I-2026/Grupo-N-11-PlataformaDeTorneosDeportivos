using System;

namespace Shared.Exceptions
{
    public class JugadorNoAsignadoException : Exception
    {
        public JugadorNoAsignadoException(string message = "El jugador no pertenece a los planteles de este partido") 
            : base(message)
        {
        }
    }

    public class JugadorNoAsignadoAPartidoException : JugadorNoAsignadoException
    {
        public JugadorNoAsignadoAPartidoException(string message = "El jugador no pertenece a los planteles de este partido") 
            : base(message)
        {
        }
    }
}

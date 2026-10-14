using System;

namespace Shared.Exceptions
{
    public class JugadorYaInscriptoException : BusinessRuleConflictException
    {
        public string Dni { get; }

        public JugadorYaInscriptoException(string dni) 
            : base($"El jugador con DNI {dni} ya se encuentra inscripto en otro equipo de este torneo (RN-02)")
        {
            Dni = dni;
        }
    }
}

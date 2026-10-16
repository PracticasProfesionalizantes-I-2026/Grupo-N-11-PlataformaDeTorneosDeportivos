using System;

namespace Shared.Exceptions
{
    public class DisponibilidadHorariaInsuficienteException : Exception
    {
        public DisponibilidadHorariaInsuficienteException(string message = "Disponibilidad horaria insuficiente para programar los partidos respetando la RN-09. Amplíe días u horarios")
            : base(message)
        {
        }
    }
}

namespace Shared.Enums
{
    public enum Disciplina
    {
        Futbol,
        ESports
    }

    public enum Modalidad
    {
        Liga,
        EliminacionDirecta
    }

    public enum EstadoInscripcion
    {
        PendienteDeValidacion,
        InscritoYConfirmado,
        RechazadoObservado
    }

    public enum RolUsuario
    {
        Organizador,
        Capitan,
        Veedor,
        Administrador,
        Espectador
    }

    public enum EstadoTorneo
    {
        Abierto,
        InscripcionesCerradas,
        EnDesarrollo,
        Finalizado
    }

    public enum EstadoPartido
    {
        Pendiente,
        EnJuego,
        Finalizado,
        Cancelado
    }

    public enum TipoIncidencia
    {
        Gol = 1,
        Punto = 2,
        TarjetaAmarilla = 3,
        TarjetaRoja = 4,
        Falta = 5,
        Sustitucion = 6
    }
}


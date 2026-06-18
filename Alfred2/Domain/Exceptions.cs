namespace Alfred2.Domain.Exceptions;

/// <summary>Se lanza cuando el médico referenciado no existe.</summary>
public class MedicoNoEncontradoException : Exception
{
    public MedicoNoEncontradoException(Guid medicoId)
        : base($"No existe un médico con id {medicoId}.") { }
}

/// <summary>Se lanza cuando el horario pedido se solapa con un turno existente del médico.</summary>
public class HorarioOcupadoException : Exception
{
    public HorarioOcupadoException(DateTime inicioUtc)
        : base($"Ya hay un turno que se solapa con el horario {inicioUtc:o}.") { }
}

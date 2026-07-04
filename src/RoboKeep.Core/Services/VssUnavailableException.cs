namespace RoboKeep.Core.Services;

/// <summary>Lo snapshot VSS non si può creare (UAC negato, WMI in errore, timeout, volume
/// non idoneo). Il chiamante degrada a copia normale con avviso: mai bloccare il backup.</summary>
public sealed class VssUnavailableException : Exception
{
    public VssUnavailableException(string message) : base(message) { }
    public VssUnavailableException(string message, Exception inner) : base(message, inner) { }
}

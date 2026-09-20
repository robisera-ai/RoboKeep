using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RoboKeep.Core.Services;

/// <summary>
/// Tiene sveglio il PC finché l'oggetto è vivo: un backup notturno di ore non deve essere
/// interrotto dalla sospensione automatica, che a un disco USB toglie l'alimentazione nel mezzo
/// di una scrittura (settori scritti a metà = falsi "settori danneggiati", file system sporco).
/// Usa una power request (PowerCreateRequest) invece di SetThreadExecutionState: quest'ultima è
/// legata al thread che la chiama, e il codice async cambia thread a ogni await.
/// Blocca solo la sospensione per INATTIVITÀ: chiudere il coperchio o premere il tasto di
/// sospensione resta una scelta dell'utente che Windows non lascia scavalcare.
/// Best-effort: se la richiesta fallisce il backup gira lo stesso.
/// </summary>
public sealed class SleepBlocker : IDisposable
{
    private readonly SafeFileHandle? _request;
    private bool _systemRequired;
    private bool _executionRequired;

    /// <summary>true se la richiesta "sistema necessario" è attiva (diagnostica e test).</summary>
    public bool IsActive => _systemRequired;

    private SleepBlocker(string reason)
    {
        try
        {
            var context = new ReasonContext
            {
                Version = 0,            // POWER_REQUEST_CONTEXT_VERSION
                Flags = 0x1,            // POWER_REQUEST_CONTEXT_SIMPLE_STRING
                SimpleReasonString = reason,
            };
            var handle = PowerCreateRequest(ref context);
            if (handle.IsInvalid) { handle.Dispose(); return; }

            _request = handle;
            _systemRequired = PowerSetRequest(handle, PowerRequestSystemRequired);
            // Sui PC con Modern Standby tiene in esecuzione il processo anche a schermo spento.
            _executionRequired = PowerSetRequest(handle, PowerRequestExecutionRequired);
        }
        catch { /* API non disponibile: si prosegue senza protezione */ }
    }

    /// <summary>Attiva il blocco; <paramref name="reason"/> compare in <c>powercfg /requests</c>.</summary>
    public static SleepBlocker Acquire(string reason) => new(reason);

    public void Dispose()
    {
        if (_request is null || _request.IsClosed) return;
        try
        {
            if (_systemRequired) PowerClearRequest(_request, PowerRequestSystemRequired);
            if (_executionRequired) PowerClearRequest(_request, PowerRequestExecutionRequired);
        }
        catch { /* la chiusura dell'handle rilascia comunque la richiesta */ }
        _systemRequired = _executionRequired = false;
        _request.Dispose();
    }

    private const int PowerRequestSystemRequired = 1;
    private const int PowerRequestExecutionRequired = 3;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        [MarshalAs(UnmanagedType.LPWStr)] public string SimpleReasonString;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(SafeFileHandle powerRequest, int requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(SafeFileHandle powerRequest, int requestType);
}

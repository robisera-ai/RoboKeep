using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace RobocopySW.Core.Services;

/// <summary>
/// Gestisce le credenziali per le share di rete:
/// cifratura/decifratura password con DPAPI e apertura/chiusura della connessione UNC.
/// </summary>
public sealed class CredentialService
{
    // DPAPI a livello macchina: consente la decifratura anche all'utente dell'attività
    // pianificata (potenzialmente diverso da chi ha salvato la password).
    private const DataProtectionScope Scope = DataProtectionScope.LocalMachine;

    /// <summary>Cifra una password in chiaro restituendo una stringa Base64 (DPAPI).</summary>
    public string Protect(string plain)
    {
        var bytes = Encoding.UTF8.GetBytes(plain ?? "");
        var enc = ProtectedData.Protect(bytes, optionalEntropy: null, Scope);
        return Convert.ToBase64String(enc);
    }

    /// <summary>Decifra una password cifrata con <see cref="Protect"/>.</summary>
    public string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
            return "";
        var enc = Convert.FromBase64String(protectedBase64);
        var dec = ProtectedData.Unprotect(enc, optionalEntropy: null, Scope);
        return Encoding.UTF8.GetString(dec);
    }

    /// <summary>
    /// Apre una connessione autenticata alla share di rete (equivalente a <c>net use</c>).
    /// Idempotente: se già connessa non solleva eccezioni.
    /// </summary>
    public void Connect(string remoteName, string user, string password)
    {
        var nr = new NetResource
        {
            dwType = ResourceTypeDisk,
            lpRemoteName = remoteName,
        };

        var result = WNetAddConnection2(nr, password, user, ConnectFlags: 0);
        // 0 = successo; 1219 (ERROR_SESSION_CREDENTIAL_CONFLICT) e 85 (già connessa) sono tollerabili.
        if (result is not (0 or 1219 or 85))
            throw new InvalidOperationException(
                $"Impossibile connettersi a '{remoteName}' (codice errore {result}).");
    }

    /// <summary>Chiude la connessione alla share, se presente.</summary>
    public void Disconnect(string remoteName)
    {
        // 0x00000001 = aggiorna il profilo; force = true.
        WNetCancelConnection2(remoteName, 1, fForce: true);
    }

    private const int ResourceTypeDisk = 0x00000001;

    [StructLayout(LayoutKind.Sequential)]
    private sealed class NetResource
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(NetResource netResource, string? password, string? username, int ConnectFlags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string name, int flags, bool fForce);
}

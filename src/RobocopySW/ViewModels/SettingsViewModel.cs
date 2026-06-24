using RobocopySW.Core.Models;
using RobocopySW.Core.Services;
using RobocopySW.Infra;
using RobocopySW.Localization;

namespace RobocopySW.ViewModels;

/// <summary>ViewModel per la finestra Impostazioni: percorsi/log, email e credenziali.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _s;
    private readonly CredentialService _credentials;

    private readonly CredentialProtectionScope _originalScope;

    public SettingsViewModel(AppSettings settings, IList<CredentialEntry> credentials, CredentialService credentialService)
    {
        _s = settings;
        _credentials = credentialService;
        CredentialList = credentials;
        _originalScope = settings.CredentialScope;
    }

    /// <summary>true = cifra le password solo per l'utente Windows corrente (DPAPI CurrentUser).</summary>
    public bool CredentialScopeUser
    {
        get => _s.CredentialScope == CredentialProtectionScope.User;
        set
        {
            _s.CredentialScope = value ? CredentialProtectionScope.User : CredentialProtectionScope.Machine;
            OnPropertyChanged();
        }
    }

    public IList<CredentialEntry> CredentialList { get; }

    /// <summary>Impostazioni email correnti (per l'invio di prova).</summary>
    public EmailSettings EmailSettings => _s.Email;

    /// <summary>Lingua selezionata: "auto", "it" o "en". Il cambio si applica subito (live).</summary>
    public string LanguageSetting
    {
        get => _s.Language ?? "auto";
        set
        {
            _s.Language = value == "auto" ? null : value;
            Loc.Instance.ApplyFromSetting(_s.Language);
            OnPropertyChanged();
        }
    }

    public string LogRoot { get => _s.LogRoot; set { _s.LogRoot = value; OnPropertyChanged(); } }
    public string TempRoot { get => _s.TempRoot; set { _s.TempRoot = value; OnPropertyChanged(); } }
    public bool CompressLogs { get => _s.CompressLogs; set { _s.CompressLogs = value; OnPropertyChanged(); } }
    public int LogRetentionDays { get => _s.LogRetentionDays; set { _s.LogRetentionDays = value; OnPropertyChanged(); } }

    public bool EmailEnabled { get => _s.Email.Enabled; set { _s.Email.Enabled = value; OnPropertyChanged(); } }
    public string SmtpHost { get => _s.Email.SmtpHost; set { _s.Email.SmtpHost = value; OnPropertyChanged(); } }
    public int SmtpPort { get => _s.Email.SmtpPort; set { _s.Email.SmtpPort = value; OnPropertyChanged(); } }
    public bool UseSsl { get => _s.Email.UseSsl; set { _s.Email.UseSsl = value; OnPropertyChanged(); } }
    public string EmailFrom { get => _s.Email.From; set { _s.Email.From = value; OnPropertyChanged(); } }
    public string EmailTo { get => _s.Email.To; set { _s.Email.To = value; OnPropertyChanged(); } }
    public string? EmailUser { get => _s.Email.Username; set { _s.Email.Username = value; OnPropertyChanged(); } }
    public bool OnlyOnError { get => _s.Email.OnlyOnError; set { _s.Email.OnlyOnError = value; OnPropertyChanged(); } }

    /// <summary>Password SMTP in chiaro inserita dall'utente; verrà cifrata DPAPI al salvataggio.</summary>
    public string EmailPasswordPlain { get; set; } = "";

    /// <summary>Applica le modifiche che richiedono elaborazione (cifratura/migrazione password).</summary>
    public void Commit()
    {
        // Se l'ambito di cifratura è cambiato, ri-cifra i segreti esistenti dal vecchio
        // ambito al nuovo (best-effort: ciò che non si decifra resta com'è e andrà reinserito).
        if (_s.CredentialScope != _originalScope)
        {
            foreach (var c in CredentialList)
                c.PasswordProtected = Reencrypt(c.PasswordProtected);
            _s.Email.PasswordProtected = Reencrypt(_s.Email.PasswordProtected);
            _credentials.Scope = _s.CredentialScope;
        }

        // Nuova password email digitata: cifrala con l'ambito (ormai) corrente.
        if (!string.IsNullOrEmpty(EmailPasswordPlain))
            _s.Email.PasswordProtected = _credentials.Protect(EmailPasswordPlain);
    }

    private string Reencrypt(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64))
            return protectedBase64 ?? "";
        try
        {
            var plain = CredentialService.UnprotectWith(protectedBase64, _originalScope);
            return CredentialService.ProtectWith(plain, _s.CredentialScope);
        }
        catch
        {
            return protectedBase64; // non decifrabile col vecchio ambito: lascia invariato
        }
    }

    /// <summary>Crea/aggiorna una credenziale di rete cifrandone la password.</summary>
    public void UpsertCredential(string id, string host, string user, string plainPassword)
    {
        var existing = CredentialList.FirstOrDefault(c => c.Id == id);
        var protectedPwd = string.IsNullOrEmpty(plainPassword)
            ? existing?.PasswordProtected ?? ""
            : _credentials.Protect(plainPassword);

        if (existing is null)
        {
            CredentialList.Add(new CredentialEntry { Id = id, Host = host, User = user, PasswordProtected = protectedPwd });
        }
        else
        {
            existing.Host = host;
            existing.User = user;
            existing.PasswordProtected = protectedPwd;
        }
        OnPropertyChanged(nameof(CredentialList));
    }

    public void RemoveCredential(CredentialEntry entry)
    {
        CredentialList.Remove(entry);
        OnPropertyChanged(nameof(CredentialList));
    }
}

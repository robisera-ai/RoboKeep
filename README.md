# RobocopySW

Applicazione desktop Windows (.NET 10, WPF) per backup/mirroring configurabili, basata sul
motore **robocopy** di sistema. Sostituisce i vecchi script batch (`Vecchi CMD/`) con una
configurazione centralizzata in un unico file, gestibile da interfaccia grafica.

Vedi [ANALISI.md](ANALISI.md) per l'analisi del sistema precedente e le scelte tecniche.

## Comportamento

Per ogni job (coppia sorgente → destinazione, ricorsivo sulle sottocartelle):

- **salta** i file identici (stessa data/ora e dimensione);
- **sovrascrive** in destinazione i file la cui sorgente è più recente;
- in **mirror** (`/MIR`, default) **rimuove** dalla destinazione i file/cartelle non più presenti in sorgente;
- con mirror disattivato (`/E`) copia e aggiorna soltanto, senza mai cancellare.

## Funzioni

- GUI: lista job, editor con **anteprima del comando**, log live.
- **Anteprima / dry-run** (`/L`): mostra cosa verrebbe copiato/cancellato senza modificare nulla.
- **Multi-thread** (`/MT`), **esclusioni** file/cartelle per job.
- **Log** per job, compressi in `.zip` e archiviati per data, con **pulizia automatica**.
- **Notifiche email** (SMTP) con esito; **report** conteggi (copiati/extra/falliti).
- **Credenziali** per share di rete UNC, cifrate con DPAPI.
- **Pianificazione** via Utilità di pianificazione di Windows.

## Requisiti

- Windows 10/11 (robocopy di sistema).
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) per l'esecuzione,
  oppure .NET 10 SDK per compilare.

## Compilazione

```powershell
dotnet build src/RobocopySW.sln -c Release
dotnet test  src/RobocopySW.Tests/RobocopySW.Tests.csproj
```

## Uso

### Interfaccia grafica

Avvia `RobocopySW.exe` senza argomenti.

### Riga di comando (per la schedulazione)

```text
RobocopySW.exe --run-all              Esegue tutti i job abilitati
RobocopySW.exe --job "Documenti"      Esegue un singolo job
RobocopySW.exe --run-all --dry-run    Anteprima (nessuna modifica)
RobocopySW.exe --job "Foto" --config "D:\percorso\config.json"
```

Exit code: `0` tutti i job riusciti, `1` almeno un errore, `2` job non trovato.

## Configurazione

Il file `config.json` risiede di default in `%ProgramData%\RobocopySW\config.json`
(modificabile dalla GUI). Esempio in [config/config.example.json](config/config.example.json).

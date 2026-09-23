# Salute dischi (SMART) — design

Data: 2026-09-23. Stato: approvato.

## Scopo

Il registro eventi avvisa quando il danno è già in corso; lo SMART lo anticipa (i 17 settori
pendenti del disco Samsung erano visibili settimane prima). Oggi per leggerlo serve un programma
esterno e saperlo interpretare. RoboKeep deve mostrare la salute di ogni disco del PC con un
verdetto in italiano e i pochi valori che contano, spiegati.

## Cosa si è verificato sull'hardware dell'utente (sonde elevate del 23/09/2026)

- Disco USB in box UASP (Samsung HD103SI): i metodi standard di Windows non funzionano (WMI
  `MSStorageDriver_FailurePredictData` «non supportato», `IOCTL_STORAGE_PREDICT_FAILURE` errore
  1). Funziona l'**ATA PASS-THROUGH (16) via `IOCTL_SCSI_PASS_THROUGH_DIRECT`** (CDB `85h`,
  SMART READ DATA `B0h/D0h`), con handle in lettura+scrittura su `\\.\PhysicalDriveN`: richiede
  **elevazione**. Restituisce i 24 attributi, identici a CrystalDiskInfo.
- NVMe interno: il log SMART/Health (log page 2) via `IOCTL_STORAGE_QUERY_PROPERTY` con
  `StorageDeviceProtocolSpecificProperty` funziona **senza elevazione**.
- Attenzione: l'ATA passthrough su un NVMe torna con `ScsiStatus = 2` (check condition) e buffer
  non valido: si accettano solo risposte con `ScsiStatus = 0` e si azzera sempre il buffer prima.
- `Get-StorageReliabilityCounter` non basta: non espone 05/C5/C6/C7.

## Cosa fa

- **Pulsante «Salute dischi»** nella barra della finestra principale, accanto a «Cronologia».
  Apre una finestra non modale.
- All'apertura (e con **Aggiorna**): legge tutti i dischi fisici. NVMe subito, nel processo
  dell'app. Per SATA/USB avvia **una volta** l'helper elevato `RoboKeep.exe --smart-helper
  <cartella>` (prompt UAC), che legge lo SMART ATA di ogni disco e scrive `smart.json` nella
  cartella; l'app lo legge e cancella la cartella. Se l'utente rifiuta l'UAC o l'helper fallisce,
  la finestra mostra gli NVMe e, per gli altri, «serve l'autorizzazione di amministratore».
- **Per ogni disco**: modello, collegamento (NVMe / SATA / USB), lettere delle unità che ospita,
  **verdetto** con colore, e la tabella dei valori con una riga di spiegazione ciascuno.
- **Verdetto** (regole pure, testate):
  - ATA/HDD: **Pericolo** se 05 (riallocati) > 0 o C6 (non correggibili) > 0; **Attenzione** se
    C5 (in attesa) > 0 («settori scritti a metà: una formattazione completa li riscrive; se
    dopo 05 sale, il disco sta cedendo») o temperatura > 50 °C; **Collegamento** (nota, non
    verdetto) se C7 > 0 («errori sul collegamento tra box e disco, non sul disco»); altrimenti
    **Buono**. BB (errori non correggibili segnalati) mostrato con spiegazione, senza pesare da
    solo sul verdetto (è uno storico cumulativo).
  - NVMe: **Pericolo** se avviso critico ≠ 0, riserva disponibile < soglia, errori del supporto
    > 0; **Attenzione** se percentuale usata ≥ 90 o temperatura > 70 °C; altrimenti **Buono**.
  - **Non leggibile**: disco senza dati (UAC negato, bridge che non risponde).
- **Registro eventi**: accanto a ogni disco, gli errori degli ultimi 14 giorni già raccolti da
  `DiskEventLog` per le sue lettere (blocchi danneggiati / I/O / file system).
- Solo letture. Nessun test SMART avviato, niente salvato su disco (a parte il JSON temporaneo
  dell'helper, cancellato subito).

## Come è fatto

- `RoboKeep.Core/Services/Smart/SmartAttributes.cs` — parse puro del blocco SMART ATA da 512
  byte (`IReadOnlyDictionary<byte, SmartAttribute(Id, Value, Worst, Raw)>`), e del log NVMe
  (`NvmeHealth(CriticalWarning, TemperatureC, AvailableSpare, SpareThreshold, PercentUsed,
  MediaErrors, UnsafeShutdowns, PowerOnHours)`). Testati con buffer costruiti a mano e con la
  fotografia reale del Samsung (valori noti: 05=0, C5=0, C7=9, BB=1138, 09=387).
- `RoboKeep.Core/Services/Smart/DiskVerdict.cs` — `enum DiskHealthLevel { Good, Warning,
  Danger, Unreadable }`, `record DiskFinding(string Key, string Value, DiskHealthLevel Level)`
  e `DiskVerdict.Evaluate(SmartAttributes)` / `Evaluate(NvmeHealth)` → livello + elenco di
  righe (chiave di localizzazione + valore). Puro, testato caso per caso.
- `RoboKeep.Core/Services/Smart/SmartReader.cs` — P/Invoke: `ReadAta(int drive)` (SCSI
  pass-through, handle R/W, richiede admin), `ReadNvme(int drive)` (query property, nessun
  admin), `ListPhysicalDisks()` (`IOCTL_STORAGE_QUERY_PROPERTY` StorageDeviceProperty per
  modello/bus/serial; `IOCTL_STORAGE_GET_DEVICE_NUMBER` sulle lettere per l'associazione).
  Best-effort: null su errore, mai eccezioni.
- `RoboKeep.Core/Services/Smart/SmartReport.cs` — `record DiskReport(int Number, string Model,
  string Bus, string[] Letters, SmartAttributes? Ata, NvmeHealth? Nvme)`; JSON per lo scambio
  con l'helper.
- `RoboKeep/App.xaml.cs` — modalità `--smart-helper <dir>`: elenca i dischi, legge ATA per
  quelli non NVMe, scrive `smart.json`, esce (exit 0). Come `--vss-helper`.
- `RoboKeep.Core/Services/Smart/SmartSession.cs` — orchestrazione dal lato app: NVMe nel
  processo, helper elevato per il resto (stesso meccanismo di avvio di `VssSession`: `runas`,
  attesa con timeout 60 s), fusione dei risultati.
- `RoboKeep/DiskHealthWindow.xaml(.cs)` + `ViewModels/DiskHealthViewModel.cs` — lista di
  schede disco: intestazione (modello, bus, lettere, verdetto colorato), tabella valori, riga
  registro eventi; pulsante Aggiorna; testo «serve l'autorizzazione» per i non leggibili.
- Testi in 5 lingue (`Loc.cs`): etichette dei valori e spiegazioni, verdetti, titolo finestra.

## Errori e casi limite

- UAC negato: helper non parte → dischi ATA «non leggibili», resto normale. Nessun errore a video.
- Helper che non termina entro 60 s: si abbandona, si mostra ciò che c'è.
- Bridge USB che non supporta SAT: `ScsiStatus ≠ 0` → «non leggibile» con nota «il box USB non
  permette di leggere lo SMART».
- Disco che non espone alcuni attributi: le righe mancanti non compaiono; il verdetto usa solo
  ciò che c'è.
- Più dischi con la stessa lettera nel tempo (rotazione): la finestra mostra i dischi
  **collegati ora**; il registro eventi per lettera può includere errori di un altro disco, e la
  riga lo dice (come già nel pre-avvio).

## Guida e changelog

- Cap. 16: sezione «Leggere la salute del disco» con il significato del verdetto e delle voci,
  in particolare C5 vs 05 e C7 (collegamento).
- Cap. 14: menzione del pulsante e del prompt UAC.
- Cap. 17 (privacy): la lettura SMART è locale, nessun dato esce dal PC.
- Changelog: voce in Unreleased.

## Test

- Parse ATA su buffer costruito (attributi noti) e su una fotografia reale; parse NVMe su
  buffer costruito.
- `DiskVerdict`: tabella di casi (buono, C5>0, 05>0, C6>0, C7>0 solo nota, temp alta; NVMe
  avviso critico, riserva sotto soglia, usura 95 %).
- `SmartReport` round-trip JSON.
- `SmartReader.ListPhysicalDisks()` sulla macchina reale: non lancia, ≥ 1 disco.
- UI e helper elevato: prova manuale (con il Samsung collegato i valori devono coincidere con
  CrystalDiskInfo).

## Fuori scopo

Test SMART (short/long), monitoraggio continuo in background, controllo automatico prima del
backup (l'UAC a ogni run non è accettabile), dischi in RAID hardware.

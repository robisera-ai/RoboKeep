# Attività pianificate di Windows — design

Data: 2026-09-24. Stato: approvato.

## Scopo

RoboKeep crea un'attività dell'Utilità di pianificazione per ogni job con una pianificazione
(`RoboKeep - <nome job>`) e una per «Avvia tutti» (`RoboKeep_AvviaTutti`). Le cancella quando il
job viene cancellato, rinominato o quando la configurazione importata non lo contiene più. Ma
non può arrivarci quando `config.json` è stato cancellato a mano, quando una copia portatile
gira da un'altra cartella, quando un job è stato rinominato nel file, o quando `schtasks` ha
fallito in silenzio: le attività restano **orfane** e partono di notte con un job che non esiste
più o con un percorso dell'exe vecchio. L'utente deve poterle vedere e cancellare dall'app.

## Cosa fa

- **Pulsante «Attività pianificate»** nella barra della finestra principale, dopo «Salute
  dischi». Attivo solo se esiste almeno un'attività con prefisso RoboKeep: l'elenco si legge in
  sottofondo all'avvio e dopo ogni salvataggio di job/impostazioni, mai sul thread della UI.
- **Finestra** (modale, come Cronologia) con una riga per attività:
  nome, **job collegato** o **«orfana»** in evidenza (nessun job con quel nome, oppure comando
  che punta a un exe diverso da quello in esecuzione → «punta a un'altra copia»), prossima
  esecuzione, ultima esecuzione ed esito (0 = ok), comando registrato.
- Pulsanti: **Elimina** (con conferma, nomina l'attività), **Aggiorna**. Nessuna modifica:
  l'attività è generata dal job e l'editor del job è l'unica fonte; un ritocco fatto in Windows
  verrebbe sovrascritto al salvataggio.
- *Rivisto in prova (24/09/2026):* eliminare l'attività di un job ancora esistente **toglie la
  pianificazione al job** (altrimenti l'editor direbbe «giornaliera» senza che nulla parta, e al
  primo salvataggio l'attività ricomparirebbe); la conferma lo dice. Tolto il pulsante «Apri
  Utilità di pianificazione»: invitava a cancellare o ritoccare in Windows, cioè a disallineare
  job e attività, e la finestra mostra già tutto ciò che serve per decidere.
- Solo attività con prefisso `RoboKeep - ` o `RoboKeep_`: le altre attività di Windows non si
  vedono e non si toccano.

## Come è fatto

- `RoboKeep.Core/Services/ScheduledTaskInfo.cs` — `record ScheduledTaskInfo(string Name,
  string? NextRun, string? LastRun, int? LastResult, string State, string Command, string
  Arguments)` più le funzioni pure: `IsRoboKeep(name)`, `JobNameOf(name)` (null per
  `RoboKeep_AvviaTutti`), `Classify(task, jobNames, currentExe)` →
  `TaskLink { Job, RunAll, OrphanNoJob, OrphanOtherExe }`. Il confronto job↔attività usa
  `SchtasksArgs.TaskName(job.Name)` (il nome dell'attività è sanificato).
- `RoboKeep.Core/Services/TaskSchedulerCatalog.cs` — lettura ed eliminazione tramite l'API COM
  dell'Utilità di pianificazione (`Schedule.Service`, late binding `dynamic`): niente parsing
  del CSV di `schtasks`, che non protegge le virgolette dentro «Task To Run». `List()` →
  `IReadOnlyList<ScheduledTaskInfo>` delle sole attività RoboKeep nella cartella radice;
  `Delete(name)`. Best-effort: su qualunque errore `List()` torna vuoto, `Delete` torna false.
  `SchedulerService` resta com'è per la creazione.
- `RoboKeep/ViewModels/ScheduledTasksViewModel.cs` + `ScheduledTasksWindow.xaml(.cs)`:
  righe con `Name`, `LinkText` (nome job / «Avvia tutti» / «Orfana: nessun job con questo
  nome» / «Orfana: punta a un'altra copia di RoboKeep»), `IsOrphan`, `NextRun`, `LastRunText`,
  `Command`; `RefreshAsync` in `Task.Run`; `DeleteAsync(row)`.
- `MainViewModel.HasScheduledTasks` (bool, aggiornato da `RefreshScheduledTasksAsync()` in
  `Task.Run`); `MainWindow`: chiamata all'avvio, dopo `TrySyncJobTask`, `TryRemoveJobTask`,
  chiusura Impostazioni e della finestra stessa. Pulsante `IsEnabled="{Binding HasScheduledTasks}"`.
- Testi in 5 lingue (`Loc.cs`), parità garantita da `LocParityTests`.

## Errori e casi limite

- API COM non disponibile o accesso negato: pulsante disattivato, nessun errore a video.
- Eliminazione fallita (attività già sparita, permessi): riga di stato nella finestra, elenco
  ricaricato.
- Attività creata da un'altra copia di RoboKeep (portatile): non orfana per nome ma il comando
  punta altrove → segnalata come «punta a un'altra copia». L'utente decide.
- Nomi di job con caratteri non validi: il confronto passa da `SchtasksArgs.TaskName`.

## Guida e changelog

- Cap. 14: pulsante e a cosa serve. Cap. 16: «Un'attività parte ma il job non esiste più».
- Changelog: voce in Unreleased/Added.

## Test

- `ScheduledTaskInfo`: `IsRoboKeep`, `JobNameOf`, `Classify` (job presente, run-all, nessun
  job, exe diverso, nome sanificato).
- `TaskSchedulerCatalog`: integrazione sulla macchina reale — crea con `schtasks` un'attività
  `RoboKeep - _prova_<guid>` che lancia `cmd.exe /c exit`, la trova in `List()`, la cancella con
  `Delete`, non la trova più; pulizia in `finally`.
- UI: prova manuale (creare un job pianificato, vederlo; rinominare il job nel config.json a
  mano, vedere l'orfana; eliminarla).

## Fuori scopo

Modifica delle attività, attività in sottocartelle, attività di altre app.

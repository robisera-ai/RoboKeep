# RoboKeep — Roadmap di prodotto

**Stato:** approvata (brainstorming) · **Branch:** `roadmap` · **Data:** 2026-06-24

> Documento di visione e decomposizione. La v1.1 è solo l'inizio: qui c'è il prodotto
> globale verso cui RoboKeep cresce. Ogni tappa avrà poi il suo spec → piano →
> implementazione dedicati.

---

## North star (la visione)

**Un backup vero per Windows, di cui fidarsi — costruito sul `robocopy` di sistema, per chiunque.**

RoboKeep oggi è un'ottima GUI di *mirroring*. La direzione è trasformarlo in uno strumento
di **backup affidabile**: che conserva uno **storico**, **avverte** quando qualcosa non va,
copre anche i **file aperti** e permette di **verificare** che i dati siano integri — senza
abbandonare ciò che lo rende solido (robocopy, locale, trasparente, portatile).

### Utente di riferimento

**Generico** — un buon backup per chiunque su Windows: utente domestico/power user,
piccolo ufficio/professionista, IT. Conseguenza sull'ordine: **prima le fondamenta comuni a
tutti** (sicurezza dei dati, allerte, versioning), **poi le feature "pro"** più pesanti (VSS,
integrità avanzata).

### Confini di scopo

**Dentro:** tutto ciò che si appoggia a strumenti nativi Windows (robocopy, VSS, Task
Scheduler, DPAPI) e resta locale/LAN.

**Fuori (per ora):** backup su **cloud** o `rclone`; versioni **macOS/Linux**; copia
**delta/a blocchi** (robocopy ricopia il file intero — è un limite accettato).

---

## I 4 pilastri

1. **Fiducia / non perdere dati** — versioning con storico, verifica integrità, allerte sui
   backup fermi o falliti, pre-check prima dell'avvio.
2. **Copertura** — copiare anche i file **aperti/bloccati** (Volume Shadow Copy).
3. **Operatività** — pianificazione per-job, cronologia run + visualizzatore log in-app,
   notifiche/tray, limite di banda, export/import configurazione.
4. **Qualità del progetto** — CI, build release automatiche, winget, repo pubblico
   (binario trasversale, non una tappa a sé).

---

## Ordine e razionale

Sequenza **"sicurezza prima"**: prima i colpi facili che eliminano i **fallimenti
silenziosi** (allerte, pre-check, dati stabili), poi la feature di punta (**versioning**),
poi la copertura pesante (**VSS**), infine la rifinitura pro (operatività + integrità).

Motivo: le fondamenta di fiducia costano poco e mettono in sicurezza tutto il resto. Un
versioning costruito su un'app che fallisce in silenzio varrebbe poco.

---

## Le tappe

### v1.1 — Fondamenta di affidabilità
*Colpi facili, stop ai fallimenti silenziosi.*

- **Allerta "backup fermo/fallito"** — rileva job non riusciti o non eseguiti da N giorni e
  destinazioni irraggiungibili; avviso ben visibile in GUI (e via email se configurata).
  *Risolve il dolore vissuto con la task pianificata rotta.*
- **Pre-check pre-avvio** — destinazione raggiungibile **e** spazio libero ≥ dimensione
  della sorgente, prima di lanciare robocopy.
- **Cartella dati stabile** — `config.json`/log fuori da `bin/`, con scelta della cartella
  dati al primo avvio. *Risolve la fragilità che è costata config e task quando si pulisce
  `bin/obj`.*
- **Notifiche toast** a fine job / su errore, con opzione di avvio minimizzato in tray.

### v1.2 — Versioning
*Lo storico = "backup vero". La feature di punta.*

- **Snapshot datati con hard-link**: ogni data appare come un albero completo ma occupa solo
  i file effettivamente cambiati (modello Time Machine / rsnapshot).
- **Ritenzione** configurabile (es. ultimi N snapshot / N giorni).
- **Recupero da una data** dalla GUI.

### v1.3 — File aperti (VSS)
*Copertura. Ingegneria più pesante.*

- **Volume Shadow Copy**: esegue il backup di file in uso (PST di Outlook, DB aperti, file
  bloccati) creando uno snapshot del volume prima della copia.

### v1.4 — Operatività & integrità
*Rifinitura "pro".*

- **Pianificazione per-job** — orari/frequenze diversi per job, non solo un'unica "Avvia tutti".
- **Cronologia run + visualizzatore log** in-app (oggi i log sono `.zip` sul disco).
- **Verifica integrità** opzionale — hash della destinazione per i job critici.
- **Limite di banda** (`/IPG`) e **export/import** della configurazione.

---

## Track trasversale — Qualità del progetto
*Parte presto, in parallelo alle tappe.*

- **CI** (build + test a ogni push).
- **Build release automatiche** sul tag (self-contained + framework-dependent).
- **winget** per l'installazione.
- Repo **pubblico** quando il prodotto è pronto.

---

## Cosa NON è in questa roadmap

Cloud/`rclone`, cross-platform, copia delta/a blocchi. Restano fuori scopo per mantenere
RoboKeep semplice, locale e basato sugli strumenti nativi di Windows.

---

## Prossimo passo

Spec dettagliato della **v1.1** (brainstorming mirato → spec → piano → implementazione),
una tappa alla volta.

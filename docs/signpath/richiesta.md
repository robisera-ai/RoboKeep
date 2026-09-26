# Firma del codice: richiesta a SignPath Foundation

Questo documento serve al maintainer per chiedere la firma gratuita del codice a
**SignPath Foundation** e per collegarla al workflow di release che è già nel repo.

Tutto ciò che è dentro un blocco in **inglese** è pronto da copiare e incollare nel
modulo di domanda o nella configurazione su SignPath. Il resto è spiegazione in italiano.

Fonti verificate il **26 settembre 2026**:

- condizioni: <https://signpath.org/terms.html>
- domanda: <https://signpath.org/apply.html> (uguale a <https://signpath.org/apply>)
- integrazione GitHub Actions: <https://docs.signpath.io/trusted-build-systems/github>
- artifact configuration: <https://docs.signpath.io/artifact-configuration/syntax> e
  <https://docs.signpath.io/artifact-configuration/reference>

> Le pagine di SignPath cambiano: prima di inviare la domanda riguardale, e se una regola
> è diversa da come è scritta qui vale la loro pagina, non questa.

## 1. Cosa vuole SignPath Foundation, e come sta RoboKeep

La pagina delle condizioni distingue due livelli. Il primo è per avere un abbonamento
SignPath.io gratuito (anche portandosi un certificato proprio), il secondo aggiunge i
vincoli per usare il **certificato di SignPath Foundation**: è quello che ci interessa.

### Condizioni per l'abbonamento gratuito

| Condizione (testo di SignPath) | RoboKeep |
|---|---|
| **No malware**: nessun malware o programma potenzialmente indesiderato | ok |
| **OSS License**: licenza approvata OSI, senza doppia licenza commerciale, per tutti i componenti | ok, MIT (`LICENSE`), dipendenze tutte MIT (`THIRD-PARTY-NOTICES.md`) |
| **No proprietary code**: nessun componente proprietario, in particolare del maintainer. Le *System Libraries* (definizione della sezione 1 della GPL v3) possono stare nei pacchetti firmati | ok. Il pacchetto self-contained include il runtime .NET di Microsoft: è MIT, non è nostro e **non va firmato** (vedi § 5) |
| **Maintained**: progetto mantenuto attivamente | ok, release regolari |
| **Released**: già pubblicato nella forma che si vuole firmare | ok, zip su GitHub Releases dalla 1.0 |
| **Documented**: funzionalità descritte nella pagina di download o nella scheda dello store | ok, README in 5 lingue + guida in `docs/guide` |

### Condizioni aggiuntive per il certificato di SignPath Foundation

- **Sign your own projects only** / **Sign your own binaries only**: chi firma deve essere
  chi sviluppa e possedere il repository; si firmano solo binari costruiti dal proprio
  codice. Ok: repo di `robisera-ai`, nessun fork, nessuna libreria di terzi da firmare.
- **No hacking tools**: niente software che cerca o sfrutta vulnerabilità o aggira le
  misure di sicurezza dell'ambiente in cui gira. Ok. Va detto in modo esplicito nella
  domanda che la finestra **Salute del disco** legge lo SMART: è diagnostica *hardware*,
  non una scansione di vulnerabilità, e le condizioni escludono esplicitamente le
  funzioni che rilevano problemi reali.
- **Respect user privacy and security**: chi manda dati a sistemi non scelti dall'utente
  deve dichiararlo in una privacy policy, mostrarla durante l'installazione e permettere
  di disattivare la funzione. Ok: nessuna telemetria; il controllo aggiornamenti chiede
  il consenso una volta e si può rifiutare, le email usano il server SMTP dell'utente.
  Il README ha già la sezione *Privacy*.
- **Announce system changes** / **Provide uninstallation**: ok, RoboKeep si estrae e si
  cancella, non c'è installer né modifica di sistema.
- **Follow security best practices**: **autenticazione a due fattori obbligatoria** su
  SignPath e su GitHub, per tutti i membri del team. Da verificare prima di fare domanda:
  <https://github.com/settings/security>.
- **Assign code signing roles**: servono i ruoli *Authors*, *Reviewers*, *Approvers*. Con
  un solo maintainer sono tutti la stessa persona, ma vanno dichiarati.
- **Specify a code signing policy**: sulla home del progetto (e sulla pagina di
  download/release) deve comparire la voce **«Code signing policy»** con la frase di
  attribuzione, i ruoli e la privacy policy. È l'unico requisito che tocca il README:
  testo pronto al § 4.
- **Artifact configuration**: i binari firmati devono avere i **metadati obbligatori**
  (*product name* e *product version*) impostati e *imposti* via metadata restrictions.
  Al § 5 c'è l'XML che lo fa.
- **Don't fight the system**: i binari devono nascere da build verificabili dal codice, e
  **ogni release va approvata a mano**. Vale la pena saperlo prima: ogni pubblicazione
  richiederà un clic di approvazione sul sito di SignPath mentre il workflow attende.

Da tenere presente, perché è scritto nero su bianco nelle condizioni: **il certificato è
intestato a SignPath Foundation**, che diventa formalmente l'editore del software;
la Foundation non è obbligata ad accettare il progetto, chiede «una certa reputazione
verificabile» per i programmi eseguibili e può sospendere l'abbonamento o revocare il
certificato se le regole vengono violate.

Nelle condizioni **non** si parla di un *code of conduct* da pubblicare nel repo: la
pagina `terms.html` si intitola «Code of conduct» ed è il codice di condotta *loro*, che
il progetto accetta. Non serve aggiungere un `CODE_OF_CONDUCT.md`.

## 2. Come si fa domanda

1. Aprire <https://signpath.org/apply.html>. La pagina ospita un modulo (HubSpot) con i
   campi visibili solo a JavaScript attivo: non si può vedere l'elenco esatto dei campi
   senza aprirla in un browser.
2. Compilare in inglese e inviare. La domanda è gratuita e non impegna a nulla.
3. La valutazione è manuale: **mettere in conto settimane**. Possono arrivare domande per
   email; rispondere in fretta accorcia i tempi.
4. All'approvazione arriva l'accesso a SignPath.io con l'organizzazione già creata.

I campi che il modulo ha chiesto finora (ordine e nomi possono cambiare) sono: dati di
contatto del maintainer, URL del repository, home page del progetto, pagina di download,
privacy policy, descrizione del progetto, elementi di reputazione, sistema di build. Al
§ 3 c'è il testo pronto per ognuno: se un campo non esiste, si ignora; se ce n'è uno in
più, si risponde con lo stesso materiale.

## 3. Testo della domanda (inglese, pronto da incollare)

**Project name**

```text
RoboKeep
```

**Repository URL**

```text
https://github.com/robisera-ai/RoboKeep
```

**Project home page / download page**

```text
https://github.com/robisera-ai/RoboKeep
https://github.com/robisera-ai/RoboKeep/releases
```

**License**

```text
MIT (OSI-approved), see https://github.com/robisera-ai/RoboKeep/blob/main/LICENSE
No dual licensing. All third-party dependencies are MIT as well, listed in
https://github.com/robisera-ai/RoboKeep/blob/main/THIRD-PARTY-NOTICES.md
```

**Maintainer**

```text
Roberto Serafini, sole maintainer (GitHub: robisera-ai). Two-factor authentication is
enabled on the GitHub account.
```

**Project description**

```text
RoboKeep is a free, open-source backup application for Windows 10 and 11, written in C#
on .NET 10 (WPF). It is a complete graphical front end for robocopy, the copy engine that
already ships with Windows: a wizard asks a few plain-language questions and turns the
answers into backup jobs that mirror folders to an external or network disk, keep dated
versions using NTFS hard links, verify copies with SHA-256, copy open files through VSS,
and run on their own schedule through the Windows Task Scheduler.

RoboKeep is entirely local. It has no cloud service, no account, no subscription and no
telemetry. The only outbound network traffic is what the user explicitly enables: an
optional update check (a single request to the GitHub releases API, which the user must
accept once and can refuse or turn off), and optional email reports through the user's own
SMTP server. The README documents this in its "Privacy" section, and the in-app guide
repeats it.

The application is distributed as two ZIP archives per release: a framework-dependent
build (needs the free .NET 10 Desktop Runtime) and a self-contained build (bundles the
Microsoft .NET runtime, MIT-licensed). There is no installer: the user extracts the ZIP
into a writable folder and runs RoboKeep.exe. Uninstalling means deleting that folder;
settings and logs live in %APPDATA%\RoboKeep and are documented.

The user interface, the wizard and the built-in guide are available in five languages
(English, Italian, Spanish, French, German).
```

**Why code signing matters for this project**

```text
RoboKeep is a backup tool, so the very first thing a new user does is download an
executable and grant it access to all of their personal files, and to an external disk
that is supposed to hold the only copy of those files. Right now Windows SmartScreen
greets that executable with "Windows protected your PC - unknown publisher", and the
README has to tell people to click through the warning. For a backup application that is
exactly the wrong lesson to teach: users who learn to dismiss the warning for RoboKeep
will dismiss it for the next thing they download, and users who do not dismiss it simply
have no backups. A signature that names a real publisher, and that proves the binary was
built by CI from the public repository, turns "trust me, click Run anyway" into something
verifiable. It also lets users detect a tampered or repackaged download, which matters for
software that runs with full access to their documents and, for open-file copying, with
administrator rights.
```

**Build process**

```text
Every release is built by GitHub Actions from a tag on the main branch, with no manual
build step and no local build ever published:
https://github.com/robisera-ai/RoboKeep/blob/main/.github/workflows/release.yml

The workflow (a) refuses to run if the tag does not match the <Version> property in
src/Directory.Build.props, (b) runs the unit test suite and stops on failure, (c) runs
"dotnet publish" twice, framework-dependent and self-contained, both win-x64, (d) [once
signing is enabled] uploads the publish output as a GitHub Actions artifact and submits it
to SignPath with signpath/github-action-submit-signing-request, waits for the signed
artifact and verifies the Authenticode signature of every file that had to be signed,
(e) packs the two ZIP archives, (f) submits them to VirusTotal, (g) creates the GitHub
Release with the two archives, the CHANGELOG section for that version and the VirusTotal
report links.

Pull requests and pushes are also built and tested by .github/workflows/ci.yml. Nothing
else in the repository produces published binaries.
```

**Artifacts to be signed**

```text
Three files, in both builds: RoboKeep.exe (the .NET apphost), RoboKeep.dll (the WPF
application) and RoboKeep.Core.dll (the backup engine). All three carry the product name
"RoboKeep" and the release version in their PE metadata, enforced through metadata
restrictions in the artifact configuration.

Microsoft's own files in the self-contained build (the .NET and Windows Desktop runtime)
and the third-party MIT libraries (WPF-UI, H.NotifyIcon.Wpf) are not signed by us; they
are only included in the archive.
```

**Reputation / evidence of the project being real and maintained**

```text
- Public repository with the full history and the release workflow, active since 2026:
  https://github.com/robisera-ai/RoboKeep
- Regular tagged releases with a hand-written CHANGELOG:
  https://github.com/robisera-ai/RoboKeep/releases
- Download counts are public on the releases page and on the README badge.
- A complete user guide shipped with the application and kept in the repository in five
  languages: https://github.com/robisera-ai/RoboKeep/tree/main/docs/guide
- Every release archive is scanned by VirusTotal and the report links are published in
  the release notes.
```

**Build system**

```text
GitHub Actions on github.com (hosted windows-latest runners).
```

**Privacy policy**

```text
https://github.com/robisera-ai/RoboKeep#privacy
"RoboKeep collects nothing. No telemetry, no analytics, no account. The only network
traffic is what you enable yourself: the optional update check (one request to GitHub) and
email reports (your SMTP server, TLS by default)."
```

## 4. Code signing policy da pubblicare

Le condizioni pretendono che la voce **«Code signing policy»** (proprio con queste parole)
sia sulla home del progetto e sulla pagina di download. In pratica: una sezione in fondo
al `README.md`, ripetuta nelle quattro traduzioni, e citata nelle note di release.

**Questa modifica non è stata fatta.** Il README si tocca solo quando il maintainer decide
(e la frase su SmartScreen cambia solo dopo la prima release firmata). Testo pronto:

```markdown
## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by
[SignPath Foundation](https://signpath.org).

- **Committers and reviewers**: [Roberto Serafini](https://github.com/robisera-ai)
- **Approvers**: [Roberto Serafini](https://github.com/robisera-ai)

Only the release workflow
([`.github/workflows/release.yml`](.github/workflows/release.yml)) submits builds for
signing, from a tag on `main`; a build made on a developer machine is never signed and
never published. Every signing request is approved by hand.

Privacy policy: RoboKeep collects nothing. See [Privacy](#privacy) above: the only
network traffic is the optional update check (one request to GitHub, off until you accept
it) and the email reports you configure yourself, sent through your own SMTP server.
```

Il link in `Approvers` può diventare la pagina dei collaboratori se un giorno il team
cresce.

## 5. Artifact configuration

L'*artifact configuration* è un XML che si crea su SignPath.io (progetto → Artifact
configurations) e che descrive **cosa** dentro il pacchetto va firmato. Tutto ciò che non
è elencato viene lasciato com'è: è così che il runtime di Microsoft resta fuori.

Il workflow carica **una** cartella (`out`) che contiene i due build, quindi lo zip che
arriva a SignPath ha `fd/` e `sc/` nella radice. `actions/upload-artifact` crea uno zip,
e per questo l'elemento radice dell'XML deve essere `<zip-file>`
(<https://docs.signpath.io/trusted-build-systems/github>).

XML da incollare:

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <parameters>
    <parameter name="version" required="true" />
  </parameters>
  <zip-file>
    <pe-file-set product-name="RoboKeep" product-version="${version}">
      <include path="fd/RoboKeep.exe" />
      <include path="fd/RoboKeep.dll" />
      <include path="fd/RoboKeep.Core.dll" />
      <include path="sc/RoboKeep.exe" />
      <include path="sc/RoboKeep.dll" />
      <include path="sc/RoboKeep.Core.dll" />
      <for-each>
        <authenticode-sign />
      </for-each>
    </pe-file-set>
  </zip-file>
</artifact-configuration>
```

Perché è scritto così:

- `<pe-file-set>` + `<include>` + `<for-each>` è la forma documentata per trattare più
  file allo stesso modo; equivale a sei `<pe-file>` con dentro `<authenticode-sign/>`.
- `product-name` e `product-version` su `<pe-file-set>` sono le *metadata restrictions*:
  la firma **fallisce** se un file non ha quei valori nell'header PE. È il requisito
  «metadata attributes set and enforced» delle condizioni. I valori arrivano da
  `src/Directory.Build.props` (`<Product>RoboKeep</Product>` e `<Version>`), e il tag
  della release deve combaciare con `<Version>`: il workflow lo controlla già al primo
  passo.
- `${version}` è un parametro definito nel blocco `<parameters>` e valorizzato dal
  workflow (`parameters: version: …`). `required="true"` vuol dire che una richiesta di
  firma senza versione viene rifiutata. **Se si cambia il nome del parametro qui, va
  cambiato anche nel workflow**, altrimenti la richiesta fallisce.
- `min-matches` vale 1 per default, quindi se uno dei sei file non c'è la firma fallisce
  invece di passare in silenzio. Voluto.
- Niente `file-version`: .NET scrive `1.8.2.0` (quattro parti) mentre il tag è `1.8.2`,
  non combacerebbero.
- Attenzione a `product-version`: vale la `ProductVersion` dell'header PE, che .NET
  ricava da `AssemblyInformationalVersion`. Oggi è esattamente `<Version>`; se un giorno
  si aggiunge SourceLink o `<SourceRevisionId>`, diventa `1.8.2+<commit>` e la firma
  fallisce. In quel caso si toglie `product-version` o si passa il valore completo.

Se il nome dato all'artifact configuration non è quella predefinita del progetto, va
aggiunto l'input `artifact-configuration-slug` al passo SignPath nel workflow: come è
adesso, il workflow usa la configurazione predefinita.

## 6. Cosa arriva dopo l'approvazione, e dove metterlo

Dalla pagina dei dettagli della *signing policy* su SignPath.io si leggono tutti gli id
che servono (la documentazione dice: «All necessary IDs can be found on the signing policy
details page»). L'API token si crea per un utente con permesso di *submitter* sul progetto
e sulla policy — meglio un utente CI dedicato che il proprio account
(<https://docs.signpath.io/build-system-integration>).

Nel repo, **Settings → Secrets and variables → Actions**:

| Dove | Nome | Valore |
|---|---|---|
| Secrets | `SIGNPATH_API_TOKEN` | l'API token dell'utente con permesso di submitter |
| Variables | `SIGNPATH_ORGANIZATION_ID` | l'id (GUID) dell'organizzazione SignPath |
| Variables | `SIGNPATH_PROJECT_SLUG` | lo slug del progetto (es. `robokeep`) |
| Variables | `SIGNPATH_POLICY_SLUG` | lo slug della signing policy (es. `release-signing`) |
| Variables | `SIGNPATH_ENABLED` | `true` — **da mettere per ultima**: è l'interruttore |

Prerequisito lato SignPath, da fare una volta: usare il *Trusted Build System*
predefinito «GitHub.com», aggiungerlo all'organizzazione e collegarlo al progetto;
installare la *SignPath GitHub App* sul repo (serve per la valutazione dell'audit log).

Finché `SIGNPATH_ENABLED` non esiste o non vale esattamente `true`, i tre passi di firma
nel workflow vengono saltati e la release esce identica a oggi. Per tornare indietro dopo
un problema basta cancellare la variabile: non serve toccare il workflow.

### Prima release firmata, in ordine

1. Mettere secret e variabili tranne `SIGNPATH_ENABLED`.
2. Creare l'artifact configuration del § 5.
3. Mettere `SIGNPATH_ENABLED=true`.
4. Taggare una versione di prova e **stare davanti al computer**: il workflow si ferma ad
   attendere l'approvazione della firma (fino a 30 minuti, poi va in timeout e la release
   non parte). Approvare su SignPath.io.
5. Scaricare gli zip della release e controllare: clic destro su `RoboKeep.exe` →
   Proprietà → **Firme digitali** deve mostrare *SignPath Foundation*.
6. Solo allora aggiornare README (5 lingue) con la sezione *Code signing policy* del § 4 e
   la frase su SmartScreen, e il capitolo 16 della guida.

## 7. Cosa fa il workflow

In `.github/workflows/release.yml`, fra i due `dotnet publish` e «Crea gli zip», ci sono
tre passi tutti sotto `if: vars.SIGNPATH_ENABLED == 'true'`:

1. **Carica i pacchetti da firmare** — `actions/upload-artifact@v7` con `path: out`.
   SignPath firma solo artefatti che stanno sui server di GitHub: vanno caricati prima e
   passati per **id**, non per nome (`steps.unsigned.outputs.artifact-id`). Un artefatto
   unico con le due cartelle vuol dire **una** richiesta di firma e quindi **una** sola
   approvazione manuale per release.
2. **Chiedi la firma a SignPath** — `signpath/github-action-submit-signing-request@v3`
   con `wait-for-completion: true`, timeout di 1800 s e `output-artifact-directory:
   signed`. L'artefatto firmato torna estratto in `signed/`, con la stessa struttura
   `fd/` + `sc/`.
3. **Sostituisci i file non firmati** — `out/fd` e `out/sc` vengono buttati e rimpiazzati
   con `signed/fd` e `signed/sc`, poi `Get-AuthenticodeSignature` controlla tutti e sei i
   file: se uno non ha una firma valida il workflow si ferma e **la release non esce**.
   Da qui in poi il workflow è quello di sempre: «Crea gli zip» prende i file firmati.

Al blocco `permissions` del workflow è stato aggiunto `actions: read`, che serve al token
dell'azione SignPath per leggere i dettagli del job e scaricare l'artefatto.

## 8. Onestà su SmartScreen

La firma **non** fa sparire l'avviso di Windows dall'oggi al domani.

- Quello che cambia subito: l'avviso non dice più *«Editore: sconosciuto»* ma
  **«SignPath Foundation»**, e il file ha un'identità verificabile. Chi arriva sul
  progetto non deve più fidarsi di una riga nel README.
- Quello che non cambia subito: SmartScreen ha una **reputazione** per file e per
  certificato, che si accumula con i download. Con un certificato OV (quello che SignPath
  Foundation usa) un eseguibile nuovo può ancora far comparire il pannello blu «Windows ha
  protetto il PC» finché quella reputazione non si forma. Solo i certificati EV — che a un
  progetto open source non sono accessibili, perché richiedono una persona giuridica —
  partono con reputazione immediata.
- Conseguenza pratica: la frase del README va riscritta, non cancellata. Qualcosa come
  «firmato da SignPath Foundation; se SmartScreen avverte comunque, il pannello mostra
  l'editore e si può controllare la firma nelle proprietà del file».

Vale comunque la pena: l'editore verificabile, la firma che prova che il binario viene da
questo repo, e il fatto che la reputazione **cresce** invece di ripartire da zero a ogni
release.

## 9. Cose che non si sono potute verificare

- **L'elenco esatto dei campi del modulo**: `signpath.org/apply.html` incorpora un modulo
  HubSpot generato da JavaScript, quindi i nomi dei campi non si leggono dall'HTML. Quelli
  del § 2 vengono da resoconti pubblici di progetti che hanno fatto domanda nel 2025-2026,
  non dalla pagina: aprirla in un browser e adattare.
- **I tempi di risposta**: le condizioni non promettono nulla. «Settimane» è quello che
  raccontano altri progetti.
- **Gli slug tipici** delle signing policy (`release-signing`, `test-signing`) e i nomi
  che SignPath assegna al progetto: si vedono solo dentro l'account, dopo l'approvazione.
- **Se la Foundation consideri RoboKeep abbastanza «reputato»**: le condizioni dicono che
  per i programmi eseguibili serve «a certain verifiable reputation» e che la decisione è
  discrezionale. Non c'è una soglia pubblica di stelle o download.

# RoboKeep

*Lire en : [English](README.md) · [Italiano](readmeita.md) · [Español](readmees.md) · Français · [Deutsch](readmede.md)*

**Configurez-le une fois — chaque fichier, chaque version, à l'abri sur votre propre disque.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Langues](https://img.shields.io/badge/langues-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Téléchargements](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep est une application Windows conviviale qui transforme `robocopy` — le moteur de copie
solide comme un roc déjà intégré à chaque PC Windows — en un **véritable outil de sauvegarde** :
configuration en quelques clics, **versions datées** de vos fichiers, copie des **fichiers que
vous utilisez encore**, chaque tâche selon **son propre horaire**, un **historique** complet et
la **preuve mathématique** que vos copies sont intactes. Vous alternez deux disques externes que
Windows appelle tous deux `E:` ? RoboKeep garde chaque tâche sur **son propre disque** et ne fait
jamais le miroir sur le mauvais. **Pas de cloud, pas de compte, pas d'abonnement** — vos fichiers
ne quittent jamais vos disques.

![Fenêtre principale de RoboKeep : tâches avec résultats en un coup d'œil et journal d'exécution en direct](docs/images/main-window.png)

## Pourquoi vous l'aimerez

- 🧙 **Répondez à quelques questions, obtenez la bonne sauvegarde.** Aucune connaissance de
  robocopy requise : la configuration guidée pose des questions sur vos disques et vos données en
  langage clair et choisit pour vous les réglages optimaux. Les experts peuvent tout ajuster à la
  main.
- 🛡️ **Vous alternez des disques de sauvegarde ? Il n'écrit jamais sur le mauvais** *(nouveauté
  en 1.5)*. Si vous alternez deux disques externes, Windows leur attribue souvent la **même
  lettre** (`E:`) — et une tâche en miroir dirigée vers le mauvais pourrait l'effacer. RoboKeep
  reconnaît chaque disque par sa véritable **identité**, pas par sa lettre, et **ignore**
  simplement la tâche quand le disque présent n'est pas celui auquel elle appartient : aucune
  erreur, rien de supprimé. Rebranchez le bon disque et tout reprend où vous en étiez — les
  icônes se mettent à jour à l'instant même où vous le branchez.
- 🕰️ **Une machine à remonter le temps pour vos fichiers.** Chaque exécution peut enregistrer une
  **version datée** de votre sauvegarde. Vous avez supprimé un paragraphe mardi dernier ? Ouvrez
  la version de mardi et récupérez-le. L'astuce maligne : les fichiers inchangés sont *partagés*
  entre les versions, donc dix versions ne coûtent pas dix fois l'espace — seulement ce qui a
  réellement changé.
- 🔓 **Copie les fichiers même pendant que vous les utilisez** *(nouveauté en 1.3)*. Archives
  Outlook, bases de données, fichiers verrouillés par d'autres programmes : d'une case à cocher,
  RoboKeep photographie le disque un instant (un « cliché instantané » de Windows) et copie
  depuis cette image figée. Et si l'instantané n'est pas possible, la sauvegarde continue
  simplement de façon normale — elle ne se bloque jamais.
- ✅ **La preuve mathématique que votre sauvegarde est intacte** *(nouveauté en 1.4)*. Le bouton
  **Vérifier** relit chaque fichier des deux côtés et compare les empreintes numériques
  (SHA-256) : la corruption silencieuse du disque — invisible à tout contrôle de date ou de
  taille — est débusquée. Et intelligemment : un fichier que vous avez modifié *après* la
  sauvegarde est signalé comme tel, jamais comme une fausse alerte. À la demande, ou automatique
  après chaque sauvegarde pour vos tâches critiques.
- ⏰ **Chaque tâche selon son propre horaire** *(nouveauté en 1.4)*. Documents chaque soir, photos
  le dimanche, archives une fois par mois : chaque tâche possède sa propre tâche planifiée
  Windows et s'exécute même l'application fermée.
- 📜 **La mémoire de chaque exécution** *(nouveauté en 1.4)*. La fenêtre **Historique** répertorie
  chaque sauvegarde et chaque vérification avec résultat, décomptes et durée — et un double-clic
  ouvre le journal complet directement dans l'application, sans fouiller dans des fichiers zip.
- 🚨 **Il vous prévient quand quelque chose ne va pas — et seulement alors.** Une sauvegarde qui
  échoue en silence est pire que pas de sauvegarde. RoboKeep marque chaque tâche problématique
  d'une icône colorée — rouge pour échouée, ambre pour « pas exécutée depuis trop longtemps »,
  orange pour « a été interrompue », grise pour « en attente de son disque » — avec une
  explication en clair au survol. Et il ne vous harcèle pas pour une tâche dont vous avez
  simplement débranché le disque : c'est un sablier, pas une alarme.
- 🔍 **Rien de caché.** L'éditeur affiche toujours la **commande exacte** qui sera exécutée. Vous
  pouvez prévisualiser n'importe quelle sauvegarde (une « exécution à blanc ») pour voir ce qui
  serait copié ou supprimé, avant de toucher à quoi que ce soit.
- 🏠 **Vraiment à vous.** Gratuit et open source (MIT), entièrement local, sans télémétrie. Parle
  italien, anglais, espagnol, français et allemand. Portable, si vous le souhaitez.

## Voyez-le à l'œuvre

| | |
|---|---|
| ![Configuration guidée](docs/images/wizard.png) | **Configuration guidée.** Quelques questions en langage clair — y compris si des fichiers restent ouverts pendant que vous travaillez — et l'assistant configure la tâche pour vous. |
| ![Éditeur de tâche](docs/images/editor.png) | **Tout sous contrôle.** Miroir ou accumulation, copie des fichiers ouverts, options pour les gros fichiers : chaque choix expliqué en une ligne, avec une astuce là où ça compte. |
| ![Aperçu de la commande et versions](docs/images/editor-preview.png) | **Transparence totale.** Versions datées avec nettoyage automatique, exclusions par tâche, et la commande robocopy exacte toujours à l'écran. |
| ![Historique des exécutions](docs/images/history.png) | **Chaque exécution consignée.** Sauvegardes et vérifications côte à côte, filtrables par tâche ; double-cliquez sur une entrée pour lire son journal complet sans toucher un zip. |
| ![Planification par tâche](docs/images/editor-schedule.png) | **Réglez et oubliez.** Chaque tâche peut avoir son propre horaire — quotidien, hebdomadaire ou mensuel — plus la vérification d'intégrité automatique après chaque exécution. |
| ![Parcourir les versions](docs/images/versions.png) | **Remontez le temps.** Choisissez une date, un clic, et la sauvegarde de ce jour-là s'ouvre dans l'Explorateur de fichiers. Récupérez ce dont vous avez besoin. |

## Démarrez en deux minutes

1. Récupérez la dernière version depuis la page **[Releases](../../releases)** :
   - **`…-selfcontained.zip`** — extrayez et lancez, **rien à installer** (inclut .NET, téléchargement plus volumineux) ;
   - **`…-framework-dependent.zip`** — bien plus léger, nécessite le
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) gratuit, à installer une fois.
2. Extrayez le zip dans un **dossier accessible en écriture** (p. ex. `D:\Programmes\RoboKeep` —
   évitez `C:\Program Files`).
3. Lancez **`RoboKeep.exe`**, cliquez sur **Nouveau**, répondez aux questions de l'assistant, puis
   **Tout exécuter**. Terminé.

Vous voulez qu'il tourne tout seul ? Donnez à chaque tâche son propre horaire directement dans
l'éditeur (quotidien, hebdomadaire ou mensuel), ou utilisez **Paramètres → Planification** pour
une unique tâche « tout exécuter ». Dans tous les cas, elle s'exécute même l'application fermée.

## Comment se comporte une sauvegarde

Pour chaque tâche (une paire dossier source → dossier destination, sous-dossiers inclus),
RoboKeep :

- **ignore** les fichiers inchangés (c'est pourquoi la deuxième exécution dure quelques secondes) ;
- **met à jour** les fichiers plus récents dans la source ;
- en mode **miroir** (par défaut), **supprime** aussi de la destination ce que vous avez effacé
  de la source — la destination reste une copie exacte ;
- avec le miroir désactivé, il ajoute et met à jour seulement, **sans jamais supprimer**.

Et si vous avez activé les versions, chaque exécution enregistre d'abord l'état précédent sous
forme d'instantané daté.

## Toutes les fonctionnalités

**Sauvegarde**
mode miroir ou accumulation · **protection contre la rotation des disques : une tâche ne
s'exécute que sur son disque, identifié par volume — jamais de miroir sur le mauvais ou l'absent**
· versions datées avec liens physiques et rétention configurable · copie des fichiers
ouverts/verrouillés via VSS · **vérification d'intégrité (SHA-256), à la demande ou après chaque
exécution** · copie multithread · exclusions de fichiers et dossiers par tâche · « forcer la
copie » pour les fichiers dont la date/taille ne change jamais (conteneurs chiffrés, certaines
bases de données), avec un mode facultatif de comparaison par contenu · mode redémarrable pour
les fichiers énormes · aperçu/exécution à blanc

**Il vous tient informé**
**historique des exécutions avec visionneuse de journaux intégrée** · icônes de santé par tâche
avec explications en langage clair, dont un état neutre « en attente de son disque » · **icônes
qui se mettent à jour à l'instant où vous branchez ou débranchez un disque** · contrôles
préalables (destination joignable, espace disque, éligibilité VSS et versions) · journal en temps
réel · archive des journaux compressés par tâche avec nettoyage automatique · notifications et
zone de notification · rapports par e-mail (SMTP), au besoin seulement en cas d'erreurs

**Il s'adapte à vous**
assistant guidé ou éditeur manuel complet · aperçu exact de la commande · **planification par
tâche (quotidienne / hebdomadaire / mensuelle)** · partages réseau avec identifiants chiffrés
(DPAPI de Windows) · **limitation du débit de copie pour les sauvegardes réseau** ·
**export/import de la configuration** · ligne de commande pour l'automatisation · réorganisation
des tâches par glisser-déposer · 5 langues · mode portable

## Bon à savoir

- **Windows 10/11 uniquement** — RoboKeep s'appuie sur robocopy et d'autres fonctions natives de
  Windows.
- **Les fichiers modifiés sont recopiés entièrement** (pas de copie par blocs/delta) : parfait
  pour documents et photos, coûteux pour de gros fichiers uniques qui changent chaque jour.
- **Les disques en rotation sont reconnus par identité, pas par lettre.** Liez une tâche à son
  disque dans l'éditeur (**Protéger avec ce disque**) ; dès lors la tâche est ignorée dès qu'un
  disque différent — ou aucun disque — est présent, pour qu'elle ne fasse jamais le miroir sur le
  mauvais. Les tâches sur des destinations internes ou réseau n'en ont pas besoin et ne sont pas
  affectées.
- **Les versions requièrent une destination NTFS locale** (les liens physiques n'existent pas sur
  exFAT ni sur les partages réseau).
- **La copie des fichiers ouverts requiert une source NTFS locale** et demande une confirmation
  administrateur (UAC) par exécution.
- **Les vérifications d'intégrité relisent chaque fichier des deux côtés** : minutieuses par
  conception, donc attendez-vous à ce qu'une vérification dure à peu près autant qu'une première
  sauvegarde. N'activez « vérifier après chaque sauvegarde » que là où ça compte.
- Vos réglages, résultats et journaux vivent dans `%APPDATA%\RoboKeep`, ils survivent donc aux
  mises à jour de l'application. Les mots de passe sont chiffrés avec la DPAPI de Windows, jamais
  stockés en clair. Note : la portée de chiffrement par défaut est **à l'échelle de la machine**
  (pour que les tâches planifiées puissent aussi les déchiffrer) — sur un PC partagé, passez le
  réglage à la portée par utilisateur si d'autres comptes ne doivent pas pouvoir les lire.

## Confidentialité

RoboKeep ne collecte **rien**. Pas de télémétrie, pas d'analytique, pas de vérification de mises
à jour, pas de compte, aucun trafic réseau à moins que *vous* ne configuriez les rapports par
e-mail (serveur SMTP de votre choix — TLS activé par défaut). Tout ce que l'application sait —
réglages des tâches, résultats, historique, journaux — vit dans des fichiers locaux sur votre PC,
du JSON lisible que vous pouvez inspecter à tout moment. Les journaux contiennent les chemins des
fichiers copiés et sont nettoyés automatiquement après 30 jours (configurable).

## Pour les utilisateurs avancés

```text
RoboKeep.exe --run-all              exécute toutes les tâches activées (code de sortie 0 = tout va bien)
RoboKeep.exe --job "Documents"      exécute une seule tâche
RoboKeep.exe --run-all --dry-run    aperçu seulement, aucune modification
RoboKeep.exe --job "Photos" --config "D:\chemin\config.json"
```

Compiler depuis les sources : `dotnet build src/RoboKeep.sln -c Release` (nécessite le SDK
.NET 10) — la suite de tests s'exécute avec `dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`.
Mode portable : créez un fichier vide `portable.flag` à côté de l'exe et tout (configuration,
journaux, résultats) reste dans le dossier de l'application. Configuration d'exemple :
[config/config.example.json](config/config.example.json). Contexte du projet et décisions
techniques : [ANALISI.md](ANALISI.md) *(en italien)*.

## Contribuer

Les traductions dans les autres langues sont maintenues par la communauté. Vous voyez une
tournure qui sonnerait plus naturellement, ou un terme qu'un locuteur natif dirait autrement ?
[Les pull requests sont les bienvenues](../../pulls) — même une correction d'une ligne aide.

## Licence

Distribué sous **licence MIT** — voir [LICENSE](LICENSE). Les composants tiers sont listés dans
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.

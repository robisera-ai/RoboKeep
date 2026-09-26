# RoboKeep

*Lire en : [English](README.md) · [Italiano](readmeita.md) · [Español](readmees.md) · Français · [Deutsch](readmede.md)*

**Configurez-le une fois. Chaque fichier, chaque version, en sécurité sur votre propre disque.**

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6)
![Langues](https://img.shields.io/badge/languages-5-success)
[![License: MIT](https://img.shields.io/badge/license-MIT-yellow)](LICENSE)
[![Téléchargements](https://img.shields.io/github/downloads/robisera-ai/RoboKeep/total)](../../releases)

RoboKeep est une application Windows de sauvegarde construite sur `robocopy`, le moteur de copie
déjà intégré à chaque PC. Répondez à quelques questions, et il garde vos dossiers en miroir sur un
disque externe, avec des versions datées auxquelles revenir. **Pas de cloud, pas de compte, pas
d'abonnement.**

![Fenêtre principale de RoboKeep : tâches avec résultats en un coup d'œil et journal d'exécution en direct](docs/images/main-window.png)

## Pourquoi RoboKeep

| | |
|---|---|
| 🧙 **Configuration simple** | L'assistant pose des questions sur vos données en langage clair et choisit les bons réglages. Les experts peuvent tout ajuster à la main. |
| 🕰️ **Remontez le temps** | Chaque exécution peut conserver une version datée. Les fichiers inchangés sont partagés entre les versions, donc dix versions ne coûtent pas dix fois l'espace. |
| 💿 **Doux avec vos disques** | S'arrête à la première erreur matérielle au lieu de s'acharner pendant des heures, maintient le PC éveillé pendant la sauvegarde, ménage les disques mécaniques. |
| 🩺 **État des disques en un coup d'œil** | Un clic lit le SMART de chaque disque et donne un verdict clair : Bon, Attention, Danger — avec chaque valeur expliquée. *Nouveauté de la 1.8.* |
| 🛡️ **Jamais le mauvais disque** | Vous alternez deux disques externes que Windows appelle tous deux `E:` ? Chaque tâche reconnaît son propre disque par son identité et attend simplement qu'il soit branché. |
| ✅ **La preuve que ça a marché** | Vérifier relit les deux côtés et compare les empreintes SHA-256. La corruption silencieuse est débusquée. |
| 🔓 **Même les fichiers ouverts** | Archives Outlook, bases de données, tout ce qui est verrouillé : une case à cocher suffit pour copier depuis un cliché instantané Windows. |
| ⏰ **Fonctionne tout seul** | Chaque tâche a son propre horaire (quotidien, hebdomadaire, mensuel, même le dernier jour du mois) et s'exécute même l'application fermée. |
| 🚨 **Ne parle que quand c'est important** | Icônes colorées et infobulles claires pour les tâches en échec, périmées ou interrompues. Un disque débranché est un sablier, pas une alarme. |
| 🏠 **Vraiment à vous** | Gratuit, open source (MIT), entièrement local, sans télémétrie. Cinq langues. Portable si vous le souhaitez. |

## Démarrez en deux minutes

1. Téléchargez la dernière version depuis **[Releases](../../releases)** :
   - **`…-selfcontained.zip`** : extrayez et lancez, rien à installer (téléchargement plus
     volumineux) ;
   - **`…-framework-dependent.zip`** : bien plus léger, nécessite le
     [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) gratuit.
2. Extrayez-le dans un **dossier accessible en écriture**, par ex. `D:\Programmes\RoboKeep` (pas
   `C:\Program Files`).
3. Lancez **`RoboKeep.exe`** → **Nouveau** → répondez à l'assistant → **Tout lancer**. Terminé.

> **Windows SmartScreen vous avertit ?** RoboKeep n'est pas encore signé numériquement. Cliquez sur
> *Informations complémentaires* → *Exécuter quand même*, ou faites un clic droit sur le zip →
> Propriétés → **Débloquer** avant de l'extraire.

## Voyez-le à l'œuvre

| | |
|---|---|
| ![Configuration guidée](docs/images/wizard.png) | **Configuration guidée.** Quelques questions, y compris si des fichiers restent ouverts pendant que vous travaillez, et la tâche est configurée pour vous. |
| ![Éditeur de tâche](docs/images/editor.png) | **Tout sous contrôle.** Miroir ou accumulation, copie des fichiers ouverts, options pour les gros fichiers, chaque choix expliqué en une ligne. |
| ![Aperçu de la commande et versions](docs/images/editor-preview.png) | **Rien de caché.** Versions datées, exclusions par tâche, et la commande robocopy exacte toujours affichée. |
| ![Historique des exécutions](docs/images/history.png) | **Chaque exécution consignée.** Sauvegardes et vérifications d'intégrité côte à côte ; un double-clic ouvre le journal complet. |
| ![Planification par tâche](docs/images/editor-schedule.png) | **Réglez et oubliez.** Quotidienne, hebdomadaire ou mensuelle, plus des vérifications d'intégrité périodiques. |
| ![Parcourir les versions](docs/images/versions.png) | **Choisissez une date.** La sauvegarde de ce jour s'ouvre dans l'Explorateur de fichiers ; recopiez ce dont vous avez besoin. |

## Comment se comporte une sauvegarde

Chaque tâche est une paire dossier source → dossier destination. À chaque exécution, RoboKeep :

- **ignore** ce qui n'a pas changé (la deuxième exécution ne prend que quelques secondes) ;
- **met à jour** ce qui est plus récent dans la source ;
- en mode **miroir** (par défaut), **supprime** aussi de la destination ce que vous avez effacé ;
- miroir désactivé, il ajoute et met à jour seulement, **sans jamais supprimer**.

Avec les versions activées, l'état précédent est d'abord enregistré sous forme d'instantané daté.

## Nouveautés de la 1.8

- **Fenêtre État des disques** : SMART de chaque disque, expliqué en mots simples. NVMe sans
  confirmation ; SATA et USB avec une confirmation administrateur, seul moyen de traverser les
  boîtiers USB.
- **Recherche de mises à jour facultative** : RoboKeep demande une fois s'il peut chercher de
  nouvelles versions sur GitHub. Quand il y en a une, une bannière propose *Nouveautés*,
  *Télécharger*, *Ignorer*. C'est vous qui remplacez les fichiers.
- **Assistant plus simple** : ne demande que ce qui compte, enregistre la tâche dès que vous avez
  terminé, gère les identifiants réseau tout seul.
- Activer les versions sur une tâche existante **ne recopie plus tout** ; une tâche annulée laisse
  quand même un journal ; les planifications mensuelles peuvent s'exécuter le **dernier jour du
  mois**.

Historique complet dans le [CHANGELOG](CHANGELOG.md).

<details>
<summary><b>Toutes les fonctionnalités</b></summary>

**Sauvegarde** : miroir ou accumulation · protection contre la rotation des disques (tâche liée à
son disque par l'identité du volume) · versions datées avec liens physiques et rétention
configurable, pas de version en double si rien n'a changé · copie des fichiers ouverts/verrouillés
via VSS · vérification d'intégrité SHA-256, à la demande ou périodique · arrêt sur erreur
matérielle qui met le disque au repos · plafond automatique de threads sur les disques mécaniques ·
PC maintenu éveillé · copie multithread · exclusions par tâche · « forcer la copie » pour les
fichiers dont la date/taille ne change jamais, avec un mode facultatif de comparaison par contenu ·
mode redémarrable pour les fichiers énormes · aperçu/exécution à blanc.

**Il vous tient informé** : historique des exécutions avec un journal pour chaque entrée, ouvert
dans le Bloc-notes · bouton Dossier des journaux · icônes de santé par tâche avec infobulles en
langage clair · icônes qui s'actualisent à l'instant où vous branchez ou débranchez un disque ·
contrôles préalables (destination joignable, espace disque, éligibilité VSS et versions, erreurs
disque récentes dans le journal des événements Windows) · fenêtre État des disques (SMART) ·
journal en temps réel · archive des journaux compressés avec nettoyage automatique · notifications
toast et zone de notification · rapports par e-mail (SMTP), au besoin seulement en cas d'erreurs.

**Il s'adapte à vous** : assistant ou éditeur manuel complet · aperçu exact de la commande ·
planification par tâche (quotidienne / hebdomadaire / mensuelle / dernier jour du mois) · partages
réseau avec identifiants chiffrés par DPAPI · limitation du débit de copie pour les sauvegardes
réseau · export/import de la configuration · ligne de commande pour l'automatisation ·
réorganisation des tâches par glisser-déposer · recherche de mises à jour facultative · 5 langues ·
mode portable.

</details>

<details>
<summary><b>Bon à savoir</b></summary>

- **Windows 10/11 uniquement.** RoboKeep s'appuie sur robocopy et d'autres fonctions natives de
  Windows.
- **Les fichiers modifiés sont recopiés en entier** (pas de copie par blocs/delta) : parfait pour
  documents et photos, coûteux pour un gros fichier unique qui change chaque jour.
- **Les versions requièrent une destination NTFS locale** (les liens physiques n'existent pas sur
  exFAT ni sur les partages réseau).
- **La copie des fichiers ouverts requiert une source NTFS locale** et une confirmation
  administrateur (UAC) par exécution.
- **Les vérifications d'intégrité relisent chaque fichier des deux côtés**, elles durent donc à peu
  près aussi longtemps qu'une première sauvegarde. C'est pourquoi la vérification automatique
  s'exécute tous les 7 jours par défaut (0 = après chaque exécution).
- **Une erreur matérielle arrête la tâche volontairement.** Avant de relancer, vérifiez câble,
  boîtier USB et alimentation (sur les disques externes ils provoquent exactement les mêmes
  erreurs qu'un disque défaillant), puis ouvrez **État des disques**.
- Les réglages, résultats et journaux vivent dans `%APPDATA%\RoboKeep` et survivent aux mises à
  jour. Les mots de passe sont chiffrés avec la DPAPI de Windows ; la portée par défaut est à
  l'échelle de la machine afin que les tâches planifiées puissent aussi les lire. Sur un PC
  partagé, passez à la portée par utilisateur.

</details>

<details>
<summary><b>Pour les utilisateurs avancés</b></summary>

```text
RoboKeep.exe --run-all              exécute toutes les tâches activées (code de sortie 0 = tout va bien)
RoboKeep.exe --job "Documents"      exécute une seule tâche
RoboKeep.exe --run-all --dry-run    aperçu seulement, aucune modification
RoboKeep.exe --job "Photos" --config "D:\path\config.json"
```

Compiler depuis les sources : `dotnet build src/RoboKeep.sln -c Release` (SDK .NET 10) ; tests :
`dotnet test src/RoboKeep.Tests/RoboKeep.Tests.csproj`. Mode portable : un fichier vide
`portable.flag` à côté de l'exe garde tout dans le dossier de l'application. Configuration
d'exemple : [config/config.example.json](config/config.example.json). Contexte du projet et
décisions techniques : [ANALISI.md](ANALISI.md) *(en italien)*.

</details>

## Confidentialité

RoboKeep ne collecte **rien**. Pas de télémétrie, pas d'analytique, aucun compte. Le seul trafic
réseau est celui que vous activez vous-même : la recherche de mises à jour facultative (une requête
à GitHub) et les rapports par e-mail (votre serveur SMTP, TLS par défaut). Tout ce qu'il sait vit
dans des fichiers JSON lisibles sur votre PC.

## Code signing policy

Signature de code gratuite fournie par [SignPath.io](https://about.signpath.io), certificat de
[SignPath Foundation](https://signpath.org) (*Free code signing provided by SignPath.io,
certificate by SignPath Foundation*). *État : demande envoyée ; les versions jusqu'à la 1.8.2
ne sont pas signées, la première version signée le précisera dans le changelog.*

- **Auteurs et relecteurs** : [Roberto Serafini](https://github.com/robisera-ai)
- **Approbateurs** : [Roberto Serafini](https://github.com/robisera-ai)

Seul le workflow de publication ([`.github/workflows/release.yml`](.github/workflows/release.yml))
soumet les builds à la signature, depuis un tag sur `main` ; un build réalisé sur un poste de
développement n'est jamais signé ni publié. Chaque demande de signature est approuvée à la main.

Politique de confidentialité : RoboKeep ne collecte rien, voir [Confidentialité](#confidentialité)
ci-dessus et le [chapitre du guide](docs/guide/en/17-privacy-security.md).

## Contribuer

Les traductions sont maintenues par la communauté : vous avez repéré une tournure qu'un locuteur
natif formulerait mieux ? [Les pull requests sont les bienvenues](../../pulls), même une correction
d'une ligne.

## Licence

**Licence MIT**, voir [LICENSE](LICENSE). Composants tiers dans
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). © 2026 Roberto Serafini.

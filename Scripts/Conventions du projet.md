---
tags: [space-engineers, conventions]
---
# Conventions du projet

> [!summary] À quoi sert cette note ?
> Elle fixe les règles communes à **tous** les scripts du dossier `Scripts` : pour le **joueur** (noms de blocs réservés, rôles des écrans, commandes), pour le **développeur** et pour les **agents IA** (contraintes du Bloc Programmable, règles de performance et d'écriture). Un script qui les respecte reste compatible avec le reste du « Data Center ».

Retour au [[00 - Sommaire]] · Agents IA : voir aussi le [[Playbook IA]].

---

## 1. Contraintes du Bloc Programmable (PB)

| Contrainte | Conséquence pour le code |
|---|---|
| Le code est collé tel quel dans l'éditeur du PB | Pas de `namespace`, pas de `using`, pas de `class Program` : on écrit directement les champs, `public Program()`, `public void Main(string argument, UpdateType updateSource)` et `public void Save()`. |
| Le compilateur du jeu est en **C# 6** | Interdit : `out var`, `if (x is IMyDoor porte)`, tuples `(a, b)`, fonctions locales, `default` littéral. Autorisé : `$"..."`, `?.`, `nameof`. |
| Limite d'instructions par exécution (~50 000) | Traitement par lots sur plusieurs exécutions pour les gros scans (inventaires, dégâts). |
| Taille max d'un script : 100 000 caractères | Les commentaires comptent ! Les scripts actuels restent largement en dessous. |
| Écran : `ContentType.TEXT_AND_IMAGE` | L'écriture `SpaceEngineers.Game.ModAPI.Ingame.ContentType` des anciens scripts est un mauvais espace de noms. |

## 2. Règles de performance (« optimisé » veut dire ça)

1. **Cache** des blocs : on ne rescanne pas toute la grille à chaque exécution, mais périodiquement ou avec l'argument `rescan`.
2. **Même construction** : `IsSameConstructAs(Me)` pour ne pas piloter / vider un vaisseau amarré (constante `SEULEMENT_CETTE_CONSTRUCTION`).
3. **Pas d'allocation inutile** : `List<>`, `StringBuilder`, `MyIni` sont des champs réutilisés.
4. **Custom Data** reparsé seulement s'il a changé.
5. **Écritures conditionnelles** : `WriteText`, `Enabled`, `Color`… seulement si la valeur change.
6. **Minuteries** avec `Runtime.TimeSinceLastRun` (temps de jeu), pas `DateTime.Now`.
7. **Echo** court : état, nombre de blocs gérés, temps d'exécution (`Runtime.LastRunTimeMs`), erreurs de configuration.

## 3. Règles d'écriture

- **Nommage en français**, sans accents dans les identifiants : `listeConteneurs`, `CalculerAutonomie()`, `SEUIL_VIDE`. Les mots-clés C# et les API du jeu restent en anglais (`GetBlocksOfType`, `CustomData`…).
- **Constantes de configuration** regroupées en haut du script.
- **Commentaires abondants** : `/// <summary>` sur chaque classe / méthode, et le *pourquoi* des choix.
- **API réelles uniquement** : seulement des API Ingame qui existent ; en cas de doute, ne pas l'utiliser et le noter dans « Limites connues ».

## 4. Conventions partagées entre scripts

### 4.1 Noms de blocs réservés

| Nom / balise | Type | Utilisé par |
|---|---|---|
| `SERVEUR_DICTIONNAIRE` | n'importe quel bloc (Custom Data) | [[Tri Automatique des Inventaires]], [[Affichage Data Center]] — voir [[Serveur Dictionnaire]] |
| `SERVEUR_ALARMES` | n'importe quel bloc (Custom Data), optionnel | [[Controle des Avaries]], [[Surveillance Carburant Uranium]] — voir [[Bus d'Alarmes]] |
| Groupe `ALARMES_CRITIQUES` | gyrophares, lumières, blocs sonores | [[Controle des Avaries]] |
| Groupe `ALARMES_URANIUM` | gyrophares, lumières, blocs sonores | [[Surveillance Carburant Uranium]] |
| Groupe `ALARMES` | gyrophares, lumières, blocs sonores (filtre optionnel `[Alarme] Sources=`) | [[Centrale d'Alarmes]] |
| `[alarme-line]`, `[alarme-quant]` (`:N` = surface) | écrans, dans le **nom** du bloc | [[Centrale d'Alarmes]] |
| `PROJECTEUR_REPARATION`, `LCD_SCANNER` | projecteur, écran | [[Diagnostic Structurel par Projecteur]] |
| `[SAS-Nom-int/ext/vent/lcd]` | portes, ventilations, écrans | [[Sas Automatique (Airlock)]] (ignorés par [[Fermeture Automatique des Portes]]) |
| `{categorie}[liste]priorite` | conteneurs, connecteurs | [[Tri Automatique des Inventaires]] |
| `[NoTri]`, `[NoAuto]` | tout bloc | exclusions du tri / de la fermeture auto |

### 4.2 Écrans : section `[Ecran]` et clé `Role`

Un écran ne doit être piloté que par **un seul** script. La clé `Role` décide lequel :

```ini
[Ecran]
; Role absent ou Stocks -> Affichage Data Center
; Avaries   -> Contrôle des Avaries
; Carburant -> Surveillance Carburant
; Structure -> Diagnostic Structurel
Role=Stocks
; Index de la surface pour un cockpit / un PB (0 par défaut)
Surface=0
```

> [!warning] Bug corrigé
> Dans les scripts d'origine, l'écran `Role=Avaries` possédait une section `[Ecran]` : le script d'affichage le capturait aussi, et les deux scripts écrivaient chacun leur tour sur la même dalle.

### 4.3 Commandes standard

| Argument | Effet (dans tous les scripts qui le proposent) |
|---|---|
| `rescan` | Relit immédiatement tous les blocs (après construction / renommage). |
| `ACK` | Acquitte une alarme (Avaries, Carburant). |

## 5. Ajouter un script

1. Partir d'un script existant proche (ex. [[Controle des Avaries]]) comme modèle.
2. Respecter le gabarit de note : frontmatter → résumé → Prérequis → Installation → Configuration → Commandes → Fonctionnement → Optimisations → Limites → Dépannage → Code complet → Voir aussi.
3. Le bloc de code de la note doit être **identique** au fichier `.cs`.
4. Si le script affiche : utiliser `[Ecran]` + un nouveau `Role`. S'il alarme : copier la classe `BusAlarmes` telle quelle, appeler `Publier(source, besoin, niveau, détail)` pour chaque alarme puis `Terminer()` (voir [[Bus d'Alarmes]]), niveau = critique, majeur, normal ou mineur, et laisser la [[Centrale d'Alarmes]] piloter les gyrophares.
5. Ajouter le script au [[00 - Sommaire]].

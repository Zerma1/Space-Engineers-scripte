---
tags: [space-engineers, revue, documentation]
---
# Revue : cohérence entre les notes et le code

> [!summary] Objet
> Pour chaque note de `optimisation_Claude`, on vérifie que le **texte explicatif** (pas le bloc de code, déjà vérifié identique au `.cs`) décrit fidèlement ce que fait le code : noms de blocs / groupes / balises, sections et clés Custom Data, constantes, commandes, fréquence d'exécution, déroulement, limites et dépannage. Revue en lecture seule : aucun fichier existant n'a été modifié.
> Légende : **Fait** = lu dans le code (fichier:ligne). **Supposition** = comportement du jeu non vérifiable à la lecture du code.

Retour au [[00 - Sommaire]].

---

## 1. Tableau récapitulatif

| Note | Fichier(s) confronté(s) | Verdict | Écarts |
|---|---|---|---|
| [[00 - Sommaire]] | tous | **OK** | 0 |
| [[00 - Conventions et Playbook IA]] | tous | Écarts mineurs | 2 (E-C1, E-C2) |
| [[Serveur Dictionnaire]] | `SERVEUR_DICTIONNAIRE.ini`, `TriInventaire.cs`, `AffichageDataCenter.cs` | Écarts | 3 (E-01a moyen, E-01b, E-01c mineurs) |
| [[Tri Automatique des Inventaires]] | `TriInventaire.cs` | Quasi OK | 1 mineur (E-02a) |
| [[Affichage Data Center]] | `AffichageDataCenter.cs` | Écarts | 2 (E-03a moyen, E-03b mineur) + 1 précision |
| [[Bus d'Alarmes]] | `SERVEUR_ALARMES.ini`, classe `BusAlarmes` (05 et 06) | Quasi OK | 1 mineur (E-04a) |
| [[Controle des Avaries]] | `ControleAvaries.cs` | Quasi OK | 1 mineur (E-05a) |
| [[Surveillance Carburant Uranium]] | `GestionnaireCarburant.cs` | **Écart important** | 1 majeur (E-06a) |
| [[Diagnostic Structurel par Projecteur]] | `DiagnosticStructurel.cs` | **OK** | 0 (1 remarque) |
| [[Sas Automatique (Airlock)]] | `GestionnaireSas.cs` | Quasi OK | 2 mineurs (E-08a, E-08b) |
| [[Fermeture Automatique des Portes]] | `FermeturePortes.cs` | Quasi OK | 1 mineur (E-09a) + 1 précision |
| Transversal | tous | Cosmétique | 1 (E-T1 : « ~1,6 s » / « ~1,7 s ») |

**Liens Obsidian** : 21 cibles distinctes `[[...]]` + 2 ancres internes (`[[#9. Code complet]]`, `[[#4. Commandes]]` dans la note 07). **Aucun lien cassé** : toutes les cibles existent (notes de `optimisation_Claude` ou de `../../.old`), et les deux ancres correspondent à des titres existants.

### Contrôles transversaux (faits)

- La classe `BusAlarmes` est **strictement identique** dans `ControleAvaries.cs` (l. 517-792), `GestionnaireCarburant.cs` (l. 406-681) et le bloc de la note [[Bus d'Alarmes]] (diff vide). Idem pour `EcransRole` (05 l. 794-914 = 06 l. 683-803) : les notes 04, 05 et 06 disent vrai.
- `SERVEUR_ALARMES.ini` = bloc `ini` de la note 04 ; `SERVEUR_DICTIONNAIRE.ini` = section 6 de la note 01.
- Aucun alias en double dans `[Alias]` ni `[Categories]` du dictionnaire (sinon le dernier lu écraserait silencieusement l'autre).
- Taille des scripts : 13 à 46,5 Ko (octets UTF-8), donc sous la limite de 100 000 caractères citée par les Conventions.
- Fréquences réelles : `Update100` pour Tri (`TriInventaire.cs:64`), Affichage (`AffichageDataCenter.cs:252`), Avaries (`ControleAvaries.cs:106`), Carburant (`GestionnaireCarburant.cs:118`), Diagnostic en mode auto (`DiagnosticStructurel.cs:220`, `None` en manuel) ; `Update10` pour Sas (`GestionnaireSas.cs:698`) et Fermeture (`FermeturePortes.cs:113`, `None` en pause). Conforme aux notes.

---

## 2. Schémas d'appel (faits, pour relire les notes)

```text
TRI (TriInventaire.cs)
Program() -> Rescanner() -> AfficherResume()
Main -> TraiterArgument -> DictionnaireAChange? -> [Rescanner: RechercherServeur, RechargerDictionnaire,
        ClasserBloc/AnalyserNomDestination, Sort] -> TraiterLotDeSources -> TrierSource -> DeplacerObjet
        -> ObtenirDestinations (cache) -> TransferItemTo -> AfficherResume

AFFICHAGE (AffichageDataCenter.cs)
Program() -> RescannerBlocs
Main -> (rescan?) RescannerBlocs [si demandé, ou 10 s ET indexCollecte==0] -> VerifierDictionnaire
     -> CollecterInventairesParLot -> (dico OU stock changé) ConstruireLignesStock -> MettreAJourGaz
     -> MettreAJourEnergie -> pour chaque écran MettreAJourEcran (LireConfigEcran si Custom Data changé,
        ConstruireLignesEcran si version changée, ComposerPage, WriteText si différent) -> AfficherResume

AVARIES (ControleAvaries.cs)
Main -> TraiterCommande (ACK / rescan) -> Rescanner(true|false) [manuel, 1er passage, ou 10 s ET début de passage]
     -> TraiterLot -> ControlerStructure ... -> TerminerPassage (tri, besoinAlarme, ConstruireRapport, Ecrire)
     -> BusAlarmes.Publier -> BusAlarmes.AppliquerAuxBlocs -> AfficherEcho

CARBURANT (GestionnaireCarburant.cs)
Main -> TraiterCommande -> (rescan|10 s) Rescanner -> MesurerCarburant -> MettreAJourEtatAlerte
     -> BusAlarmes.Publier -> AppliquerAuxBlocs -> ConstruireRapport -> EcransRole.Ecrire -> AfficherEcho

DIAGNOSTIC (DiagnosticStructurel.cs)
Program() -> lit Storage -> AppliquerFrequence -> RescannerBlocs -> ExecuterDiagnostic(forcé)
Main -> chronos -> TraiterCommande -> (10 s) RescannerBlocs -> ExecuterDiagnostic(forcé si pas Update100)
Save() -> Storage = "auto" | "manuel"

SAS (GestionnaireSas.cs)
Main -> (10 s ou bloc disparu) ScannerLesSas -> TraiterCommande -> pour chaque sas complet : MettreAJour
     (statuts portes, DiagnostiquerPanne, GetOxygenLevel, TraiterRepos | TraiterCycle | TraiterModeManuel,
      AfficherStatut) -> AfficherConsole

FERMETURE (FermeturePortes.cs)
Program() -> lit Storage ("pause") -> AppliquerFrequence -> ScannerLesPortes
Main -> TraiterCommande -> (si pas en pause) [10 s] ScannerLesPortes -> SurveillerLesPortes -> AfficherConsole
```

---

## 3. Détail des écarts

### E-01a — Serveur Dictionnaire : recherche du serveur (gravité : moyenne)

- **Note** : [[Serveur Dictionnaire]], §3, encadré « Un seul serveur par construction » (l. 97-98).
- **Texte** : « Si deux blocs s'appellent `SERVEUR_DICTIONNAIRE`, les scripts prennent le premier trouvé sur **leur** construction. Le serveur d'un vaisseau amarré n'est utilisé que si la base n'en a pas. »
- **Code (fait)** :
  - Tri : vrai. `RechercherServeur()` privilégie `IsSameConstructAs(Me)` puis se rabat sur le premier trouvé (`TriInventaire.cs:297-316`).
  - Affichage : **faux**. `serveurDictionnaire = GridTerminalSystem.GetBlockWithName(NOM_SERVEUR_DICTIONNAIRE);` (`AffichageDataCenter.cs:350`), sans aucun filtre de construction. Le bloc renvoyé peut être celui d'un vaisseau amarré, même si la base possède le sien.
- **Correction proposée** : « Le **Tri** prend d'abord le serveur de sa propre construction, puis à défaut celui d'une grille connectée. L'**Affichage** prend le premier bloc portant ce nom sur tout le réseau (vaisseaux amarrés compris). Évitez donc d'avoir un second `SERVEUR_DICTIONNAIRE` sur un vaisseau qui s'amarre à la base. » (Variante : aligner le code de l'Affichage sur celui du Tri, mais cette revue ne modifie pas le code.)

### E-01b — Serveur Dictionnaire : message de console (gravité : mineure)

- **Note** : [[Serveur Dictionnaire]], §3 Installation, étape 4 (l. 95).
- **Texte** : « leur console affiche `Dictionnaire : OK (...)` ».
- **Code (fait)** : seul le Tri affiche `OK (N alias objets, N alias catégories)` (`TriInventaire.cs:365`). L'Affichage affiche `Dictionnaire : N noms, N alias, N catégories` (`AffichageDataCenter.cs:1097-1099`), ou `Dictionnaire : absent (noms du jeu)` (l. 1093).
- **Correction proposée** : « Console du Tri : `Dictionnaire : OK (...)` ; console de l'Affichage : `Dictionnaire : N noms, N alias, N catégories`. »

### E-01c — Serveur Dictionnaire : sections lues par l'Affichage (gravité : mineure)

- **Note** : [[Serveur Dictionnaire]], §1 tableau (l. 20), §2.4 (l. 84) et « Voir aussi » (l. 270).
- **Texte** : « `[Affichage]` (+ `[Categories]`, `[Alias]` selon le script) » ; « [l'ancienne section] est toujours lue par le script de tri » ; « Affichage Data Center — utilise `[Affichage]` ».
- **Code (fait)** : l'Affichage lit **toujours** les quatre sections : `[Categories]` (l. 442-449, pour la clé `Categories` des écrans), `[Dictionnaire]` puis `[Alias]` (l. 451-469, pour la clé `Exclusions`) et `[Affichage]` (l. 471-478). Ici aussi, `[Alias]` est lu après `[Dictionnaire]` et l'emporte.
- **Correction proposée** : tableau §1 : « `[Affichage]` pour les noms ; `[Categories]` pour la clé `Categories=` des écrans ; `[Alias]` (+ ancienne `[Dictionnaire]`) pour la clé `Exclusions=` ». §2.4 : « elle est toujours lue par le Tri **et** par l'Affichage ».

### E-02a — Tri : détection d'une destination (gravité : mineure)

- **Note** : [[Tri Automatique des Inventaires]], §5 Algorithme, étape 2.3 (l. 120).
- **Texte** : « Chaque conteneur / connecteur dont le nom contient `{` est analysé ».
- **Code (fait)** : le test porte sur `{` **ou** `}` (`TriInventaire.cs:488`). Un nom ne contenant que `}` est donc analysé et produit l'erreur « accolades { } incomplètes ou inversées » (l. 552).
- **Correction proposée** : « Chaque conteneur / connecteur dont le nom contient `{` ou `}` est analysé (un nom avec une seule accolade produit une erreur de nommage dans la console). »

### E-03a — Affichage : le serveur n'est pas filtré par construction (gravité : moyenne)

- **Note** : [[Affichage Data Center]], §1 (l. 19) et §5 étape 1 (l. 174).
- **Texte** : « Tous les blocs doivent être sur la **même construction** que le PB » ; « Seuls les blocs de la même construction que le PB sont gardés. »
- **Code (fait)** : vrai pour les inventaires, réservoirs, batteries, producteurs et écrans (`AffichageDataCenter.cs:354-372`), mais **pas** pour `SERVEUR_DICTIONNAIRE`, cherché par `GetBlockWithName` sans filtre (l. 350). Même cause que E-01a.
- **Correction proposée** : ajouter « Exception : `SERVEUR_DICTIONNAIRE` est cherché par son nom sur tout le réseau, vaisseaux amarrés compris (le premier trouvé est utilisé). »

### E-03b — Affichage : schéma Mermaid incomplet (gravité : mineure)

- **Note** : [[Affichage Data Center]], §5, diagramme (l. 196-198).
- **Texte** : nœud `G{Cycle terminé et stock changé ?}` → « Construire les lignes triées / version des données +1 ».
- **Code (fait)** : les lignes sont reconstruites si `dictionnaireChange || stockChange` (`AffichageDataCenter.cs:302-306`) ; un changement de gaz ou d'énergie incrémente aussi `versionDonnees` (l. 311). Le texte de l'étape 6 (« quand le stock ou le dictionnaire change ») est juste ; seul le schéma est incomplet.
- **Correction proposée** : renommer le nœud en `G{Dictionnaire rechargé, ou cycle terminé avec stock changé ?}` et ajouter après `I` : « version +1 si gaz / énergie changés ».

> [!note] Précision recommandée (pas un écart)
> §5 étape 5 : la collecte additionne **tous** les inventaires de chaque bloc (`for (int i = 0; i < bloc.InventoryCount; i++)`, l. 537-547) : uranium des réacteurs, minerai en entrée des raffineries, munitions des tourelles, contenu des cockpits... Le stock affiché est donc « tout ce que contient la construction », pas seulement les coffres. Une phrase dans la note éviterait une surprise au joueur.

### E-04a — Bus d'Alarmes : délai de détection du serveur (gravité : mineure)

- **Note** : [[Bus d'Alarmes]], §4 Installation, étape 4 (l. 81).
- **Texte** : « Ils retrouvent le serveur au plus tard 10 s après ».
- **Code (fait)** : Carburant rescanne toutes les 10 s (`GestionnaireCarburant.cs:135`). Avaries ne rescanne automatiquement **qu'entre deux passages** (`indexProchainBloc == 0 && secondesDepuisRescan >= 10`, `ControleAvaries.cs:129-130`) : sur un grand vaisseau dont le passage dure plus d'une exécution, le délai peut dépasser 10 s.
- **Correction proposée** : « Ils retrouvent le serveur au rescan suivant (environ 10 s ; un peu plus pour le Contrôle des Avaries sur un très grand vaisseau, qui attend la fin de son passage), ou immédiatement avec l'argument `rescan`. »

> [!note] Suppositions de la note 04 (non vérifiables à la lecture du code)
> - « Les blocs programmables s'exécutent les uns après les autres (jamais en même temps) » (l. 32-33) : c'est le fonctionnement connu du jeu, mais le code ne le garantit pas.
> - « Les commentaires `;` [...] sont conservés par `MyIni` » (l. 105) : dépend de l'implémentation de `MyIni.ToString()`, non vérifié ici.

### E-05a — Contrôle des Avaries : « Analyse en cours » jamais affiché (gravité : mineure)

- **Note** : [[Controle des Avaries]], §8 Dépannage (l. 162).
- **Texte** : « L'écran reste vide / « Analyse en cours » | ... premier passage pas terminé ».
- **Code (fait)** : le texte initial `"... Analyse en cours..."` (`ControleAvaries.cs:95`) n'est jamais envoyé aux écrans. `ecransAvaries.Ecrire(...)` n'est appelé que dans `TerminerPassage()` (l. 373), après `ConstruireRapport()` qui remplace ce texte (l. 410-467). Avant la fin du premier passage, l'écran garde son ancien contenu (ou reste vide).
- **Correction proposée** : « L'écran reste vide ou garde un ancien texte | pas de `[Ecran] Role=Avaries`, écran ajouté depuis moins de 10 s, ou premier passage pas encore terminé | vérifier le Custom Data ; attendre quelques exécutions ; `rescan` ».

### E-06a — Surveillance Carburant : l'alarme s'éteint quand les réacteurs tombent à sec (gravité : majeure)

- **Note** : [[Surveillance Carburant Uranium]], §5 « Calcul de l'autonomie » (l. 101), §7 Limites (l. 154-160), §8 Dépannage (l. 168).
- **Texte** : « Sans consommation (0 MW), l'autonomie est « INFINIE » » ; dépannage : « Autonomie « INFINIE » | réacteurs éteints ou consommation nulle | normal à l'arrêt ».
- **Code (fait)** :
  - `consommationMW` = somme des `CurrentOutput` des **réacteurs** (`GestionnaireCarburant.cs:243`) ;
  - si `consommationMW <= 0`, l'autonomie vaut `double.PositiveInfinity` (l. 284) ;
  - si `enAlerte && autonomieTotaleHeures > SEUIL_REARMEMENT_HEURES`, l'alerte est **levée** et l'ACK oublié (l. 306-310).
- **Conséquence** (fait pour le code, supposition sur le jeu : un réacteur sans uranium ne fournit plus de puissance) : quand **tous** les réacteurs ont épuisé leur uranium, leur production tombe à 0 MW, l'autonomie devient « INFINIE » et l'alarme **s'éteint** au moment précis de la panne sèche. Seule la ligne « SANS URANIUM » (l. 266) reste visible dans le rapport. Même effet si tous les réacteurs sont éteints ou détruits. La note ne signale pas ce cas.
- **Correction proposée** (ajout en §7 et dans le dépannage) :
  > - **Panne sèche** : l'autonomie se calcule avec la puissance **fournie** par les réacteurs. Un réacteur vide ne fournit plus rien : si tous les réacteurs sont à sec (ou éteints), la consommation vaut 0, l'autonomie devient « INFINIE » et **l'alarme s'éteint**. Surveillez la section « Réacteurs à vérifier » (`SANS URANIUM`, `ÉTEINT`, `ENDOMMAGÉ`).
  >
  > Dépannage : « Autonomie « INFINIE » et alarme éteinte alors que la base est dans le noir | réacteurs vides, éteints ou détruits | recharger en uranium ; lire « Réacteurs à vérifier » ».

### E-08a — Sas : casse du nom du sas et de la balise (gravité : mineure)

- **Note** : [[Sas Automatique (Airlock)]], §1 encadré « Nom du sas » (l. 33).
- **Texte** : « Dans les commandes, la casse n'a pas d'importance. »
- **Code (fait)** : c'est exact pour les commandes (`TrouverSas`, `OrdinalIgnoreCase`, `GestionnaireSas.cs:780`), mais **pas pour les noms de blocs** :
  - la balise `[SAS-` est cherchée avec `Contains` sensible à la casse (l. 829) ;
  - le nom du sas sert de clé de `Dictionary<string, GestionnaireSas>` avec le comparateur par défaut (l. 672, 680, 864) : `[SAS-hangar-int]` et `[SAS-Hangar-ext]` forment **deux sas différents**, tous deux incomplets ;
  - seul le rôle (`int`, `ext`, `vent`, `lcd`) est mis en minuscules (l. 859).
- **Correction proposée** : « Dans les commandes, la casse n'a pas d'importance. Dans les **noms de blocs**, en revanche, `[SAS-` et le nom du sas doivent être écrits à l'identique (majuscules comprises) sur tous les blocs d'un même sas ; seul le rôle (`int`/`INT`...) est insensible à la casse. »

### E-08b — Sas : nom omis dans une commande (gravité : mineure)

- **Note** : [[Sas Automatique (Airlock)]], §4 (l. 81).
- **Texte** : « Le nom du sas peut être omis (`cycle`) **s'il n'existe qu'un seul sas**. »
- **Code (fait)** : `TrouverSas("")` ne regarde que `ListeDesSas` (sas **complets**) : `ListeDesSas.Count == 1` (`GestionnaireSas.cs:777`, liste remplie l. 912). Un sas incomplet ne compte pas.
- **Correction proposée** : « ... s'il n'existe qu'un seul sas **complet** (les sas incomplets listés dans la console ne comptent pas). »

### E-09a — Fermeture des portes : `[SAS-` est sensible à la casse (gravité : mineure)

- **Note** : [[Fermeture Automatique des Portes]], §1 (l. 16 et tableau l. 18-23).
- **Texte** : « Balises optionnelles, à placer dans le nom de la porte (**majuscules indifférentes**) », tableau incluant `[SAS-...]`.
- **Code (fait)** : `[NoAuto]` et `[Auto:` sont bien insensibles à la casse (`FermeturePortes.cs:225, 275`), mais `[SAS-` est testé par `nom.Contains(BALISE_SAS)` (l. 224), donc **sensible** à la casse. La note se contredit d'ailleurs elle-même : §7 (l. 105) dit « sensible à la casse ». Le commentaire d'en-tête du code (l. 8) porte la même imprécision.
- **Correction proposée** : « Balises optionnelles (majuscules indifférentes **pour `[NoAuto]` et `[Auto:x]`** ; `[SAS-` doit être écrit exactement ainsi). »

> [!note] Précision recommandée (pas un écart)
> Une porte de hangar dont la balise `[Auto:x]` est illisible ou hors bornes reste **ignorée** : `baliseTrouvee` n'est vrai que pour une balise valide (`FermeturePortes.cs:302`, test l. 238). L'avertissement apparaît dans la console, mais la note (§1, encadré « Portes de hangar ») pourrait le dire.

### E-C1 — Conventions : tableau des noms réservés incomplet (gravité : mineure)

- **Note** : [[00 - Conventions et Playbook IA]], §4.1 (l. 50-53).
- **Texte** : le tableau cite `PROJECTEUR_REPARATION`, `LCD_SCANNER`, `[NoTri]`, `[NoAuto]`.
- **Code (fait)** : deux autres balises réservées existent : `[DIAG]` dans le nom d'un projecteur (`DiagnosticStructurel.cs:40, 249`) et `[Auto:x]` dans le nom d'une porte (`FermeturePortes.cs:51, 275`).
- **Correction proposée** : ligne projecteur : « `PROJECTEUR_REPARATION` ou balise `[DIAG]`, `LCD_SCANNER` » ; ajouter une ligne « `[Auto:x]` | portes | délai personnalisé de [[Fermeture Automatique des Portes]] ».

### E-C2 — Conventions : règle « Custom Data reparsé seulement s'il a changé » (gravité : mineure)

- **Note** : [[00 - Conventions et Playbook IA]], §2, règle 4 (l. 28).
- **Texte** : « **Custom Data** reparsé seulement s'il a changé. »
- **Code (fait)** : appliqué par Tri (`TriInventaire.cs:334`), Affichage (`AffichageDataCenter.cs:411, 795`) et `BusAlarmes` (05 l. 257). En revanche, `EcransRole.Rechercher` reparse **à chaque rescan** (toutes les 10 s) le Custom Data de tous les blocs contenant `Role` (`ControleAvaries.cs:847-852`, idem 06), et le Diagnostic reparse les blocs contenant `[Ecran]` à chaque rescan (`DiagnosticStructurel.cs:329-336`). Le coût reste faible (10 s), mais la règle n'est pas universelle.
- **Correction proposée** : « Custom Data reparsé seulement s'il a changé, **ou au plus au rescan périodique** (recherche des écrans `Role=`). »

> [!note] Affirmations des Conventions non vérifiables par le code
> « Compilateur en C# 6 » et « limite ~50 000 instructions » (§1) sont des affirmations sur le jeu. Le code les respecte : aucun `out var`, motif `is T x`, tuple ni fonction locale trouvé dans les 7 scripts. Mais la version exacte du compilateur ne peut pas être confirmée ici.

### E-T1 — Transversal : « ~1,6 s » ou « ~1,7 s » (gravité : cosmétique)

- **Notes** : Tri (l. 72, 91), Avaries (l. 45), Carburant (l. 126), Bus (l. 16, 108) disent « ~1,6 s » ; Affichage (l. 114, 165, 174) et Diagnostic (l. 86) disent « ~1,7 s ».
- **Code (fait)** : même valeur partout, `UpdateFrequency.Update100`, soit 100 ticks. À 60 ticks/s (supposition : simulation à vitesse normale), cela fait 1,67 s.
- **Correction proposée** : harmoniser sur « ~1,7 s (100 ticks) ».

---

## 4. Notes sans écart : points vérifiés

### [[00 - Sommaire]]
Fichiers, rôles (`Role=Stocks/Avaries/Carburant/Structure`), groupes `ALARMES_CRITIQUES` / `ALARMES_URANIUM`, balises `[SAS-...]`, ordre d'installation et un PB par script : conformes au code.

### [[Diagnostic Structurel par Projecteur]]
Vérifiés et conformes : constantes (8 lignes, valeurs exactes, l. 37-58), commandes `(vide)`/`scan`/`auto`/`manuel`/`stop`/`rescan` (l. 187-211), écriture forcée hors `Update100` (l. 168-169), `Storage` et `Save()` (l. 122, 134-137), quatre cas du rapport et conseils d'alignement (l. 457-517), exemple d'affichage (491/503 → 19 `#`, 97,6 %), ancres `[[#9. Code complet]]` et `[[#4. Commandes]]`.

> [!note] Remarque (comportement non décrit)
> Un LCD nommé exactement `LCD_SCANNER` reçoit le rapport **quel que soit** son Custom Data (`DiagnosticStructurel.cs:322-325`). S'il porte aussi une section `[Ecran]` sans `Role` (ou `Role=Stocks`), l'Affichage Data Center écrira dessus lui aussi. C'est un cas rare ; on peut le signaler dans les limites.

### [[Tri Automatique des Inventaires]] (hors E-02a)
Les points suivants sont conformes : constantes (9 lignes), syntaxe `{categorie}[liste]priorite`, priorité 99 par défaut et correction du `TryParse`, `[NoTri]` (une destination balisée reste destination), sources vidées / jamais vidées (l. 480-513), anti ping-pong strict (l. 727-744), lots de 20 sources et garde à 50 % (l. 661-681), commandes `rescan`/`stats`, messages de dépannage (l. 552-629).

> [!note] Incertitude
> Un **éjecteur** est, à ma connaissance, un bloc de type connecteur (`IMyShipConnector`) : il serait alors traité comme source et, s'il est nommé `{...}[...]`, comme destination. C'est une supposition sur le jeu, non vérifiable dans le code.

### [[Bus d'Alarmes]] (hors E-04a)
Règles d'état global et de couleur (Priorite, puis première source active de `[Etat]`), auto-enregistrement de `[Couleurs]`, absence d'ajout de `[Config]`, mode autonome et messages Echo (`Bus : autonome (aucun bloc SERVEUR_ALARMES)`, `Custom Data de SERVEUR_ALARMES illisible (format INI)`), application `Enabled` / `Color` / `Play()`-`Stop()` : tout est conforme à la classe `BusAlarmes`.

### [[Controle des Avaries]] (hors E-05a)
Constantes (9 lignes), commandes ACK / rescan, règle de réarmement (`IntersectWith`, l. 360), tri détruits → H.S. → intégrité croissante, formule d'intégrité, absence de sauvegarde de l'ACK (aucun `Save()` / `Storage`) : conformes.

### [[Surveillance Carburant Uranium]] (hors E-06a)
Constantes (11 lignes), hystérésis 1 h / 1,5 h, ACK sans alerte sans effet, stock limité aux `IMyCargoContainer`, réacteurs à vérifier (ENDOMMAGÉ / ÉTEINT / SANS URANIUM) : conformes.

### [[Sas Automatique (Airlock)]] (hors E-08a/b)
Constantes (9 lignes), machine à 5 états, front montant, verrouillage croisé, causes d'ÉCHEC, mode manuel de 30 s, exemple d'écran (48,7 % → 5 `#`), rescan qui conserve l'état des sas : conformes.

### [[Fermeture Automatique des Portes]] (hors E-09a)
Constantes (9 lignes), bornes 0,5-3600 s, parsing virgule/point, portes de hangar exclues sauf `[Auto:x]` valide, pause mémorisée dans `Storage`, rescan immédiat si une porte disparue est détectée (uniquement au moment de la refermer, l. 171-190) : conformes.

---

## Voir aussi

- [[00 - Sommaire]]
- [[00 - Conventions et Playbook IA]]

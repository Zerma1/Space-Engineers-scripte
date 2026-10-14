---
tags: [space-engineers, revue]
---
# Revue de code - reviewer B (scripts 04 à 09)

> [!summary] Périmètre et méthode
> Revue en lecture seule (aucun fichier modifié) de `ControleAvaries.cs` (05), `GestionnaireCarburant.cs` (06), du bus `SERVEUR_ALARMES.ini` / classe `BusAlarmes` (04), de `DiagnosticStructurel.cs` (07), `GestionnaireSas.cs` (08) et `FermeturePortes.cs` (09), au regard de [[00 - Conventions et Playbook IA]] (C# 6, ~50 000 instructions, 100 000 caractères, conventions partagées) et des originaux du dossier `Scripte\` et de `../../.olde/Script_Airlock_Optimise.cs`.
> Chaque constat est classé **Fait** (démontré par le code cité) ou **Supposition** (dépend d'un comportement du jeu non vérifiable ici).

Retour au [[00 - Sommaire]].

## Tableau récapitulatif

| ID | Sévérité | Fichier | Ligne(s) | Constat | Nature |
|---|---|---|---|---|---|
| SAS-1 | **Majeur** | 08 `GestionnaireSas.cs` | 905-914, 494-502, 730-733 | Un sas qui devient incomplet en plein cycle n'est plus géré : sa porte cible reste verrouillée (`Enabled=false`) indéfiniment | Fait |
| CAR-1 | **Majeur** | 06 `GestionnaireCarburant.cs` | 282-286, 300-313 | Réacteurs à sec -> puissance 0 -> autonomie « INFINIE » -> l'alarme se coupe au pire moment (défaut déjà présent dans l'original) | Fait (+ supposition légère) |
| AVA-1 | **Majeur** | 05 `ControleAvaries.cs` | 194-229, 324-379 | Le budget d'instructions ne borne que la boucle du lot : rescan (3 parcours complets de grille) et fin de passage (tri par délégué de toutes les avaries) ne sont pas bornés -> risque « Script Too Complex » sur grosse base / après combat | Supposition (ordre de grandeur) |
| SAS-2 | Mineur | 08 `GestionnaireSas.cs` | 343-345, 444-451 | Fenêtre de course : la porte d'origine est déverrouillée au moment où la porte cible s'ouvre -> deux portes ouvertes possibles quelques dixièmes de seconde | Fait |
| BUS-1 | Mineur | 05 et 06 (`BusAlarmes.LireCouleur`) | 05:771-783 / 06:660-672 | Couleur `[Couleurs]` mal formée : chaque script retombe sur **sa** couleur propre -> gyrophare partagé qui alterne rouge / vert | Fait |
| AVA-2 | Mineur | 05 `ControleAvaries.cs` | 202-205, 264-269 | Démontage volontaire, séparation de sous-grille (rotor / bloc de fusion) -> bloc « DÉTRUIT » + alarme critique | Fait |
| CAR-2 | Mineur | 06 `GestionnaireCarburant.cs` | 63, 254, 302 | L'alarme se base sur l'autonomie **totale** (stock des conteneurs inclus) : réacteurs pouvant s'arrêter sans alarme si le stock n'est pas acheminé (limite documentée) | Fait |
| SAS-3 | Mineur | 08 `GestionnaireSas.cs` | 52, 57, 361-383 | 15 s sans gagner 2 points d'O2 = ÉCHEC : un très grand sas avec une seule ventilation peut être abandonné à tort | Supposition |
| DIA-1 | Mineur | 07 `DiagnosticStructurel.cs` | 321-325 | `LCD_SCANNER` est piloté par son nom seul, sans tenir compte de `Role` -> double pilotage avec [[Affichage Data Center]] si son Custom Data contient `[Ecran]` sans `Role` | Fait (conditionnel) |
| POR-1 | Info | 09 `FermeturePortes.cs` | 224-229 | Toute porte contenant `[SAS-` est ignorée, même si le sas est incomplet (et donc ignoré aussi par 08) | Fait |
| POR-2 | Info | 09 `FermeturePortes.cs` | 290-291 | `System.Globalization.CultureInfo` / `NumberStyles` : présence dans la liste blanche du PB à confirmer en jeu | Supposition |
| BUS-2 | Info | 05 / 06 (`BusAlarmes`) | 05:737 / 06:626 | Toute porte placée dans `ALARMES_CRITIQUES` / `ALARMES_URANIUM` est désactivée (verrouillée) hors alarme ; interaction avec 09 | Fait (préexistant) |
| BUS-3 | Info | 05 / 06 (`BusAlarmes`) | 05:560, 734 | `blocsDejaSynchronises` n'est jamais purgé | Fait |
| AVA-3 | Info | 05 `ControleAvaries.cs` | 50, 106, 252-276 | Latence d'alarme proportionnelle à la taille de la grille (N/250 exécutions de ~1,6 s) | Fait |
| GEN-1 | Info | 03, 05/06, 07 | — | Trois implémentations différentes de la convention `[Ecran] Role=` (pré-filtres et casse divergents) | Fait |
| CAR-3 | Info | 06 `GestionnaireCarburant.cs` | 348 | `StringBuilder.Append(StringBuilder)` | Supposition |
| SAS-4 | Info | 08 `GestionnaireSas.cs` | 424-436 | Ventilations laissées dans leur dernier mode après ÉCHEC (pressurisation d'un sas qui fuit) | Supposition |
| OK-1 | Info (positif) | 04/05/06 + notes | — | `BusAlarmes` et `EcransRole` strictement identiques entre 05, 06 et `Bus d'Alarmes.md` ; blocs de code des notes identiques aux `.cs` ; INI cohérent | Fait (vérifié par `diff`) |

**Totaux : 0 Bloquant, 3 Majeurs, 6 Mineurs, 9 Info** (dont 1 constat positif).

> [!info] Compilation
> Aucune syntaxe postérieure à C# 6 trouvée (pas de `out var`, de `is T x`, de tuples, de fonctions locales ni de `default` littéral ; les `out` sont déclarés avant l'appel). Les API utilisées existent dans l'API Ingame à ma connaissance : `MyIni` (`TryParse`, `Get`, `Set`, `ContainsKey`, `GetKeys`), `IMySlimBlock.CurrentDamage/BuildIntegrity/MaxIntegrity`, `IMyProjector.TotalBlocks/RemainingBlocks/BuildableBlocks/IsProjecting`, `IMyAirVent.GetOxygenLevel()/CanPressurize/Depressurize`, `IMyDoor.Status/OpenDoor()/CloseDoor()`, `IMySoundBlock.Play()/Stop()`, `IMyTextSurfaceProvider`, `Runtime.CurrentInstructionCount`. Seule réserve : POR-2 (classée en supposition).
> Tailles : 05 = 37 625 car., 06 = 33 663, 07 = 24 056, 08 = 38 323, 09 = 13 236, tous sous 100 000.

---

## 04 - Bus d'Alarmes (`SERVEUR_ALARMES.ini` + classe `BusAlarmes`)

### OK-1 - Cohérence des copies (Info, positif)
- **Fait** : vérifié par `diff` : les lignes 517-792 de `ControleAvaries.cs` et 406-681 de `GestionnaireCarburant.cs` sont identiques entre elles et identiques au bloc de code de `Bus d'Alarmes.md`. La classe `EcransRole` est aussi identique (05:794-914 / 06:683-803). `SERVEUR_ALARMES.ini` correspond au bloc INI de la note (sources `Avaries` / `Carburant`, couleurs 255,0,0 / 0,255,0, `Priorite=Avaries,Carburant`), et ces noms correspondent à `SOURCE_ALARME` (05:44, 06:45) et à `COULEUR_ALARME` (05:47, 06:48).
- Les blocs `csharp` des notes 05 à 09 sont identiques aux fichiers `.cs` (règle 3 du playbook respectée).

### BUS-1 - Couleur mal formée : alternance de couleurs sur un gyrophare partagé (Mineur)
- **Fichier** : `ControleAvaries.cs` 771-783 (même code dans `GestionnaireCarburant.cs` 660-672)
- **Problème** : quand la valeur `[Couleurs]` de la source dominante est illisible, `LireCouleur` renvoie la couleur propre **du script appelant**. Les deux scripts calculent donc le même état global, mais pas la même couleur.
- **Preuve** :
  ```csharp
  string[] morceaux = texte.Split(',');
  if (morceaux.Length != 3) return couleurPropre;
  ...
  if (!int.TryParse(morceaux[0].Trim(), out rouge)) return couleurPropre;
  ```
  L'auto-enregistrement (05:669-673) ne répare que l'absence de clé (`!ContainsKey`), pas une valeur mal formée (`255;0;0`). Même effet si la source dominante est une 3e source sans couleur.
- **Risque** : un gyrophare présent dans les deux groupes reçoit rouge puis vert toutes les ~1,6 s : c'est exactement le conflit que le bus devait supprimer. Ce n'est pas plus grave que ça, mais la note (« Couleur ignorée ») ne mentionne pas ce clignotement.
- **Recommandation** : en cas de couleur illisible, utiliser une couleur de repli **commune** (constante de la classe, ex. blanc ou rouge) au lieu de `couleurPropre`, et l'indiquer dans `Diagnostic`.

### BUS-2 - Portes dans un groupe d'alarme (Info, préexistant)
- **Fichier** : 05:737 / 06:626 ; commentaire 05:16 « (gyrophares, lumières, blocs sonores, portes...) »
- **Preuve** : `if (bloc.Enabled != AlarmeGlobale) bloc.Enabled = AlarmeGlobale;` appliqué à tout `IMyFunctionalBlock`.
- **Risque** : une porte du groupe est **verrouillée en temps normal** et déverrouillée pendant l'alarme. Le script 09 lui envoie alors `CloseDoor()` sans effet (porte éteinte) et relance son chrono à chaque délai. L'original faisait la même chose (`blocAlarme.Enabled = alarmeRequise`), donc ce n'est pas une régression.
- **Recommandation** : documenter que les portes sont à éviter dans ces groupes, ou ne piloter que `IMyLightingBlock` / `IMySoundBlock`.

### BUS-3 - Ensemble jamais purgé (Info)
- **Preuve** : `readonly HashSet<long> blocsDejaSynchronises` (05:560) ne reçoit que des `Add` (05:734).
- **Risque** : croissance lente si l'on reconstruit souvent des blocs d'alarme ; négligeable en pratique.
- **Recommandation** : `IntersectWith` sur les identifiants du groupe courant au moment du rescan (facultatif).

---

## 05 - Contrôle des Avaries (`ControleAvaries.cs`)

### AVA-1 - Travail non borné dans une même exécution (Majeur)
- **Emplacement** : `Rescanner` 194-229, `TerminerPassage` 324-379 (tri ligne 339), budget ligne 54 / 257
- **Problème** : seul le `while` de `TraiterLot` vérifie `Runtime.CurrentInstructionCount < BUDGET_INSTRUCTIONS`. Ce qui s'exécute avant (rescan) et après (fin de passage) ne s'arrête jamais en cours de route :
  - rescan automatique = 3 parcours complets de la grille avec du code utilisateur par bloc : boucle `GetBlockWithId` sur l'ancien cache (202-205), `GetBlocksOfType<IMyTerminalBlock>(…, EstDeCetteConstruction)` (210), puis `EcransRole.Rechercher` avec un lambda qui lit `CustomData` sur chaque bloc (847-848) ;
  - fin de passage = `avariesPassage.Sort(ComparerAvaries)` sur **toutes** les avaries (le délégué `ComparerAvaries` appelle 2 fois `RangGravite`, soit environ n·log2(n) appels instrumentés), plus deux boucles complètes (345-356, 362-369).
- **Preuve** :
  ```csharp
  while (indexProchainBloc < blocsSurveilles.Count
         && nombreTraites < BLOCS_PAR_EXECUTION
         && Runtime.CurrentInstructionCount < BUDGET_INSTRUCTIONS)
  ...
  if (indexProchainBloc >= blocsSurveilles.Count) TerminerPassage();   // non borné
  ```
  `TerminerPassage` peut s'exécuter dans le même run qu'un lot qui a déjà consommé jusqu'à 30 000 instructions.
- **Risque** (**supposition** sur le coût exact par appel) : après un combat (plusieurs centaines de blocs terminaux endommagés) ou sur une base de plusieurs milliers de blocs terminaux, on peut dépasser la limite et le PB s'arrête (« Script Too Complex »). C'est justement le moment où le contrôle des avaries est utile.
- **Recommandation** : (1) faire le rescan dans une exécution dédiée, sans lot derrière ; (2) découper `TerminerPassage` (ou trier par seaux de gravité sans délégué : trois listes `Detruit` / `Critique` / `Degats`, et ne trier que les `MAX_LIGNES_AFFICHEES` premières) ; (3) exprimer le budget en fraction de `Runtime.MaxInstructionCount` plutôt qu'en constante fixe de 30 000.

### AVA-2 - Faux « DÉTRUIT » sur démontage ou séparation (Mineur)
- **Emplacement** : 202-205 et 264-269
- **Preuve** :
  ```csharp
  if (GridTerminalSystem.GetBlockWithId(bloc.EntityId) == null)
  {
      MemoriserDetruit(bloc);
  ```
- **Problème** : tout bloc qui quitte le système terminal est classé DÉTRUIT, donc critique, et déclenche l'alarme : démontage volontaire à la meuleuse, tête de rotor ou piston arrachée, désamarrage par bloc de fusion. L'en-tête (ligne 22) prévoit le cas du démontage (« rescan … à faire après un démontage volontaire »), mais pas celui de la séparation d'une sous-grille. Ajout par rapport à l'original, qui n'avait pas de notion de DÉTRUIT.
- **Risque** : fausses alarmes rouges ; le joueur prend l'habitude de faire `ACK`, ce qui efface aussi les vrais DÉTRUIT (`Acquitter` vide `blocsDetruits`, 178).
- **Recommandation** : documenter le cas des sous-grilles. Envisager un `ACK` qui garde l'affichage des DÉTRUIT sans relancer l'alarme (séparer « acquitté » et « oublié »).

### AVA-3 - Latence de l'alarme (Info)
- **Preuve** : `BLOCS_PAR_EXECUTION = 250` (50), `Update100` (106) ; l'alarme n'est recalculée qu'en fin de passage (361-369).
- **Risque** : avec 5 000 blocs terminaux : 20 exécutions × ~1,6 s ≈ 32 s entre une panne et l'alarme. C'est voulu (scan par lots), mais ce n'est pas chiffré dans la note.
- **Recommandation** : l'indiquer dans « Limites » ; éventuellement lever l'alarme dès qu'un bloc **nouvellement** critique est vu dans `ControlerStructure`, sans attendre la fin du passage.

### Régressions vs `Controle_Avaries_SE.md`
- **Fait** : aucune fonctionnalité perdue. Écrans `Role=Avaries`, `[CRITIQUE]` / `[DÉGÂTS] x%`, rapport « SYSTÈMES NOMINAUX » et groupe `ALARMES_CRITIQUES` sont conservés. Ajouts : ACK, DÉTRUIT, limite de lignes, surfaces de cockpit. Changement voulu : seulement la construction courante (`SEULEMENT_CETTE_CONSTRUCTION`).

---

## 06 - Gestionnaire de Carburant (`GestionnaireCarburant.cs`)

### CAR-1 - Réacteurs vides : l'alarme s'éteint (Majeur, préexistant)
- **Emplacement** : 282-286, 300-313
- **Preuve** :
  ```csharp
  if (consommationMW <= 0.0) return double.PositiveInfinity;
  ...
  else if (enAlerte && autonomieTotaleHeures > SEUIL_REARMEMENT_HEURES)
  {
      enAlerte = false;
      alarmeAcquittee = false;
  ```
- **Problème** : quand les réacteurs n'ont plus d'uranium, ils ne produisent plus rien, donc `CurrentOutput` vaut 0 (comportement du jeu : quasi certain, mais **supposition** non vérifiée ici). `consommationMW` vaut alors 0, l'autonomie devient infinie, `enAlerte` repasse à `false` et l'alarme se coupe **au moment de la panne sèche**. L'écran n'affiche plus que « SANS URANIUM » dans la liste des réacteurs. Le même trou existait dans l'original (`float.MaxValue`), et la note le présente comme normal (« Autonomie INFINIE … normal à l'arrêt »).
- **Risque** : black-out silencieux, alors que c'est le scénario exact que le script doit signaler.
- **Recommandation** : traiter « réacteurs activés et fonctionnels mais uranium total = 0 » comme une autonomie nulle (alerte), ou forcer `besoinAlarme` quand tous les réacteurs actifs sont « SANS URANIUM ».

### CAR-2 - Alarme sur l'autonomie totale (Mineur, documenté)
- **Emplacement** : 63, 254, 302
- **Preuve** : `autonomieTotaleHeures = CalculerAutonomieHeures(uraniumReacteursKg + uraniumStockKg);` puis `if (!enAlerte && autonomieTotaleHeures <= SEUIL_ALERTE_HEURES)`.
- **Problème** : changement par rapport à l'original, qui alarmait sur l'uranium **des réacteurs** seulement. Si les conteneurs ne sont pas reliés par convoyeur, les réacteurs s'arrêtent sans alarme. C'est documenté (note, § Limites) avec un contournement (`COMPTER_STOCK_CONTENEURS=false`).
- **Recommandation** : deuxième seuil d'alerte sur `autonomieReacteursHeures`, par exemple 10 min, pour couvrir le cas sans dépendre du réglage.

### CAR-3 - `Append(StringBuilder)` (Info, supposition)
- **Ligne 348** : `texte.Append(lignesReacteursAProbleme);` Sur .NET Framework (moteur du jeu, **supposition**), il n'y a pas de surcharge `Append(StringBuilder)` : l'appel passe par `Append(object)`, ce qui fonctionne mais alloue une chaîne intermédiaire. Sans conséquence fonctionnelle.

### Régressions vs `Gestionnaire de Carburant.md`
- **Fait** : ACK, couleur verte et seuil d'1 h sont conservés. La balise `[giro-alarme]` est remplacée par le groupe `ALARMES_URANIUM` : c'est le nom donné par la note d'origine, et le changement est annoncé en en-tête (ligne 12). En revanche, un joueur qui utilisait la balise doit recréer le groupe (Info).

---

## 07 - Diagnostic Structurel (`DiagnosticStructurel.cs`)

### DIA-1 - `LCD_SCANNER` hors convention `Role` (Mineur, conditionnel)
- **Emplacement** : 321-325
- **Preuve** :
  ```csharp
  if (panneau != null && bloc.CustomName == NOM_ECRAN)
  {
      return panneau;
  }
  ```
  `AffichageDataCenter.cs` (850) capture tout écran ayant `[Ecran]` **sans** `Role` (« Role absent = Stocks »).
- **Risque** : un `LCD_SCANNER` dont le Custom Data contient `[Ecran]` sans `Role` est piloté par les deux scripts, qui écrivent chacun leur tour (le bug que la convention 4.2 voulait éliminer).
- **Recommandation** : pour la règle 1, ignorer `LCD_SCANNER` si `[Ecran]` a un `Role` différent de `Structure` **ou absent**, avec un avertissement dans Echo ; ou documenter « ne pas mettre `[Ecran]` dans `LCD_SCANNER` ».

### Autres observations
- `ContentType` n'est forcé qu'au rescan (`PreparerSurface`, 267/377-383) : si un joueur repasse l'écran en mode image, le texte reste invisible jusqu'au rescan suivant (≤ 10 s en mode auto). Info.
- **Régressions vs originaux** (`Le Scanner de Brèches.md`, `Script Space Engineers Diagnostic Structurel.md`) : aucune. Projecteur `PROJECTEUR_REPARATION`, `LCD_SCANNER`, états INTÈGRE / BRÈCHES, blocs soudables et conseil d'alignement sont conservés ; mode manuel par défaut (`UpdateFrequency.None` sans `Storage`). Ajouts : multi-projecteurs `[DIAG]`, mode auto mémorisé, barre d'intégrité.

---

## 08 - Gestionnaire de Sas (`GestionnaireSas.cs`)

### SAS-1 - Sas devenu incomplet en plein cycle : porte verrouillée à vie (Majeur)
- **Emplacement** : `ScannerLesSas` 905-914 ; `Verrouiller` 494-502 ; `TraiterCycle` 343 ; `Main` 715-733
- **Preuve** :
  ```csharp
  foreach (KeyValuePair<string, GestionnaireSas> paire in sasTrouves)
  {
      sasParNom[paire.Key] = paire.Value;
      if (paire.Value.EstComplet()) ListeDesSas.Add(paire.Value);
      else NomsDesSasIncomplets.Add(paire.Key);
  }
  ...
  foreach (GestionnaireSas sas in ListeDesSas) sas.MettreAJour(secondesEcoulees);
  ```
  et, pendant un cycle : `Verrouiller(porteCible);` qui fait `porte.Enabled = false`.
- **Problème** : pendant une (dé)pressurisation, la porte cible est éteinte. Si la dernière ventilation ou l'autre porte est détruite ou démontée (ou renommée), `TousLesBlocsExistent` déclenche un rescan, le sas passe dans `NomsDesSasIncomplets`, et plus aucun code ne touche à ses portes : rien ne déverrouille la porte cible. L'état PANNE (porte endommagée, ventilation hors service) n'est jamais atteint dans ce cas, car il suppose le sas complet.
- **Risque** : joueur bloqué dans le sas, par exemple côté base inaccessible après la destruction de la ventilation pendant une pressurisation, jusqu'à une réactivation manuelle de la porte dans le terminal. Cela contredit la promesse de la note (« plus jamais de joueur bloqué »).
- **Recommandation** : lors du scan, pour tout sas connu qui devient incomplet, réactiver (`Enabled = true`) les portes encore présentes, remettre `Etat = Repos` et afficher « sas incomplet » sur ses écrans. Plus simple : ne jamais retirer un sas de la mise à jour, mais le gérer en mode PANNE dégradé (verrouillage croisé sur les portes restantes).

### SAS-2 - Fenêtre où les deux portes peuvent être ouvertes (Mineur)
- **Emplacement** : 343-345 et 444-451
- **Preuve** :
  ```csharp
  if (porteOrigine.Status == DoorStatus.Closed) Deverrouiller(porteOrigine);
  ...
  porte.OpenDoor();               // porte cible, porte d'origine toujours activée
  ```
- **Problème** : au moment où le script ouvre la porte cible, la porte d'origine est fermée mais **activée** (déverrouillée pour le « changement d'avis »). Le verrouillage croisé n'agira qu'à l'exécution suivante (~0,17 s), puis la porte doit encore se refermer. Un 2e joueur qui tape la porte d'origine dans cet intervalle ouvre les deux portes en même temps. Le code détecte le cas et relance un cycle, mais l'air de la base fuit pendant la fermeture.
- **Risque** : faible (surtout en multijoueur), mais contraire à l'en-tête (« jamais ouvertes en même temps »).
- **Recommandation** : dans `OuvrirPorteCible`, verrouiller la porte d'origine (elle est fermée, puisque `deuxPortesFermees`) **avant** `OpenDoor()`.

### SAS-3 - Délai sans progrès pour les grands sas (Mineur, supposition)
- **Emplacement** : 52, 57, 361-383
- **Preuve** : `DELAI_MAX_SANS_PROGRES_S = 15.0`, `PROGRES_MINIMAL = 0.02f` ; ÉCHEC si le niveau n'a pas bougé de 2 points en 15 s.
- **Risque** (**supposition** sur le débit des ventilations) : un sas-hangar volumineux avec une seule ventilation peut progresser de moins de 2 % en 15 s : cycle abandonné et portes déverrouillées alors que tout fonctionne.
- **Recommandation** : rendre le délai configurable par sas (balise ou Custom Data), ou mesurer une pente (progrès / temps) plutôt qu'un seuil fixe.

### SAS-4 - Ventilations laissées en l'état après ÉCHEC (Info, supposition)
- **Emplacement** : 424-436 (« Les ventilations sont laissées telles quelles »)
- **Risque** (**supposition**) : après un échec « sas non étanche », les ventilations restent en pressurisation sur une pièce qui fuit. La consommation d'O2 qui en résulte dépend du comportement du jeu, non vérifié.
- **Recommandation** : en ÉCHEC de pressurisation, repasser les ventilations en `Depressurize = true` (ou au moins le documenter).

### Points de sécurité vérifiés (Faits, sans anomalie)
- Dépressurisation : la porte extérieure n'est ouverte que si `deuxPortesFermees && niveauO2 <= SEUIL_VIDE` (352-358).
- Démarrage avec les deux portes ouvertes : la porte intérieure est fermée en priorité (461-464).
- Verrouillage seulement une fois la porte fermée (`Verrouiller`, 494-502) : corrige la porte « éteinte en mouvement » de l'original.
- ÉCHEC et PANNE déverrouillent les portes fermées : pas de blocage en cas de réservoirs pleins ou vides, ni de coupure de courant des ventilations.
- Ouverture par le script non prise pour un « door bump » (`statutPrecedent… = Opening`, 449-450).
- Recompilation en plein cycle : l'état repart à `Repos`, puis le verrouillage croisé déverrouille les portes fermées (auto-réparation).

### Régressions vs originaux (`Gestionnaire de Sas (Airlock) Intelligent.md`, `../../.olde/Script_Airlock_Optimise.cs`)
- **Fait** : nomenclature, multi-ventilations, écrans, rescan, auto-rescan sur bloc disparu et liste des sas incomplets sont conservés.
- Changement volontaire et documenté : une porte intérieure tapée dans un sas non étanche lance une pressurisation (puis un ÉCHEC après 15 s) au lieu de s'ouvrir directement. C'est plus sûr, mais le joueur attend 15 s de plus. Info.

---

## 09 - Fermeture Automatique des Portes (`FermeturePortes.cs`)

### POR-1 - Portes `[SAS-` orphelines (Info)
- **Emplacement** : 224-229
- **Preuve** : `if (nom.Contains(BALISE_SAS) || …) { nombreExclues++; continue; }`
- **Problème** : l'exclusion se fait sur la seule balise. Les portes d'un sas **incomplet** (ou mal balisé, ex. `[SAS-X-intt]`) ne sont gérées ni par 08 ni par 09, et ne se referment jamais automatiquement. C'est cohérent avec la convention (`[SAS-` est sensible à la casse dans les deux scripts), mais cela mérite une ligne dans la note.
- **Recommandation** : documenter ; éventuellement afficher dans Echo le nombre de portes `[SAS-` ignorées.

### POR-2 - `CultureInfo` dans le PB (Info, supposition)
- **Emplacement** : 290-291
- **Preuve** : `double.TryParse(texteDelai, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out delai)`
- **Supposition** : je pense que ces types sont autorisés dans la liste blanche du PB, sans pouvoir le garantir. Si ce n'est pas le cas, la compilation échouera.
- **Recommandation** : vérifier en jeu (« Vérifier le code »). Repli possible : découper sur `'.'` et utiliser deux `int.TryParse`, ou `double.TryParse(string, out double)` après normalisation (dépend alors de la culture du serveur).

### Régressions vs `fermeture des porte.md`
- **Fait** : délai de 2 s et exclusion `[SAS-` conservés ; passage de `DateTime.Now` au temps de jeu. Changement voulu : les portes de hangar ne sont plus refermées par défaut (`INCLURE_PORTES_HANGAR=false`, sauf `[Auto:x]`). Cela corrige un défaut de l'original, mais modifie son comportement : à signaler aux utilisateurs.

---

## Transversal

### GEN-1 - Trois implémentations de la convention `[Ecran] Role=` (Info, maintenabilité)
- `EcransRole` (05/06) : pré-filtre `CustomData.Contains("Role")` sensible à la casse (05:848).
- `DiagnosticStructurel` (07) : pré-filtre `Contains("[Ecran]")` sensible à la casse (07:330), plus la règle par nom `LCD_SCANNER`.
- `AffichageDataCenter` (03) : pré-filtre `IndexOf("[Ecran]", OrdinalIgnoreCase)` (03:838), et `Role` absent = Stocks.
- **Risque** : un Custom Data écrit `[ecran]` ou `role=` est accepté ou refusé selon le script. Divergences futures probables.
- **Recommandation** : faire de `EcransRole` la classe « copie conforme » de référence (comme `BusAlarmes`), avec une note dédiée, et l'utiliser aussi dans 07.

### Conflits entre scripts : bilan
| Ressource | Verdict |
|---|---|
| Écrans `Role` (Avaries / Carburant / Structure / Stocks) | Cohérent, sauf DIA-1 (`LCD_SCANNER`) |
| Portes `[SAS-` (08 vs 09) | Cohérent (même balise, même casse) ; voir POR-1 |
| Gyrophares partagés (05 vs 06) | Cohérent **avec** `SERVEUR_ALARMES` ; alternance possible si couleur mal formée (BUS-1) ; conflit inchangé sans serveur (documenté) |
| Portes dans les groupes d'alarme vs 09 | Voir BUS-2 |

## Voir aussi
- [[00 - Conventions et Playbook IA]]
- [[Bus d'Alarmes]] · [[Controle des Avaries]] · [[Surveillance Carburant Uranium]]
- [[Diagnostic Structurel par Projecteur]] · [[Sas Automatique (Airlock)]] · [[Fermeture Automatique des Portes]]

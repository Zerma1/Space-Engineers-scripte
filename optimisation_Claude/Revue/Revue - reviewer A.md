---
tags: [space-engineers, revue]
---
# Revue - reviewer A

> [!summary] Périmètre
> - `02 - Tri Automatique/TriInventaire.cs` (843 lignes, 34 004 caractères)
> - `03 - Affichage Data Center/AffichageDataCenter.cs` (1 122 lignes, 46 507 caractères)
> - `01 - Serveur Dictionnaire/SERVEUR_DICTIONNAIRE.ini` (cohérence avec le parsing des deux scripts)
>
> Références : [[00 - Conventions et Playbook IA]], originaux `../../_old/Scripte`, `../../_old/Scripte/Gestion de l'Affichage et Traductions.md`, `../../_old/gemini-code-1791364758252.cs` (tri), `../../_old/gemini-code-1791364773398.cs` (affichage).
>
> Aucun fichier existant n'a été modifié.

## Tableau récapitulatif

| # | Sévérité | Fichier | Ligne(s) | Constat |
|---|---|---|---|---|
| M1 | **Majeur** | AffichageDataCenter.cs | 308-311, 805-809, 519-528 | Le numéro de version unique (`versionDonnees`) est incrémenté presque à chaque exécution par l'énergie, ce qui reconstruit TOUS les écrans à chaque fois, hors budget d'instructions |
| M2 | **Majeur** | TriInventaire.cs | 345-356, 289, 577, 728-731 | Si le dictionnaire est en erreur ou disparaît, le tri continue avec des règles cassées et peut déplacer en masse le contenu des coffres nommés par alias |
| m1 | Mineur | TriInventaire.cs | 243-248, 436-462 | Le rescan (toutes les 10 s) n'est pas soumis au budget d'instructions, et le lot de sources s'exécute dans le même tick |
| m2 | Mineur | TriInventaire.cs | 724-737, 768 | `GetItemAmount` et la concaténation de la clé du cache sont faits pour chaque pile, même quand aucune destination n'est éligible |
| m3 | Mineur | TriInventaire.cs | 485-494 | Les connecteurs (dont les éjecteurs, *supposition*) sont vidés : le script peut reprendre ce que le joueur veut éjecter |
| m4 | Mineur | TriInventaire.cs / AffichageDataCenter.cs | Tri 297-316 / Aff. 350 | Les deux scripts ne recherchent pas `SERVEUR_DICTIONNAIRE` de la même façon |
| m5 | Mineur | AffichageDataCenter.cs | 297-311 | Collecte, gaz et énergie calculés même sans aucun écran actif (l'original s'arrêtait) |
| m6 | Mineur | AffichageDataCenter.cs + .ini | Aff. 968-973 ; ini 31 | Une exclusion par alias perd la catégorie : `Exclusions=gravier` masque aussi la pierre (minerai) |
| m7 | Mineur | TriInventaire.cs | 485-494, 550-562 | Un coffre `{...}` mal nommé reste une SOURCE et il est vidé |
| m8 | Mineur | TriInventaire.cs | 444-454, 519, 574 | Le rescan toutes les 10 s réalloue toutes les destinations, les sources et les HashSet |
| m9 | Mineur | AffichageDataCenter.cs | 229-231 | La taille `5` des tableaux de production est codée en dur, sans lien avec `NOMS_PRODUCTEURS` |
| i1 | Info | TriInventaire.cs | 480-513 | Périmètre des sources réduit volontairement par rapport à l'original (régression assumée) |
| i2 | Info | AffichageDataCenter.cs | 862-867, 1052-1068, 1022 | Changements de comportement visibles par rapport à l'original |
| i3 | Info | AffichageDataCenter.cs | 377-388, 937-948 | Un seul écran par bloc ; conflit possible avec la règle par nom de Diagnostic Structurel |
| i4 | Info | AffichageDataCenter.cs | 133, 592, 159-162 | Code mort et valeurs par défaut en double |
| i5 | Info | TriInventaire.cs | 697 | En désassemblage, l'assembleur n'est plus vidé du tout |
| i6 | Info | SERVEUR_DICTIONNAIRE.ini | 9-139 | Fichier cohérent avec les deux parseurs ; quelques objets récents ne sont pas couverts |
| i7 | Info | les deux .cs | - | Contrôle de compilation C# 6 et des API : rien de bloquant trouvé |

**Total : 0 Bloquant, 2 Majeurs, 9 Mineurs, 7 Info.**

---

## Détail des constats

### M1 - Majeur - Reconstruction de tous les écrans à chaque exécution, hors budget

- **Fichier** : `AffichageDataCenter.cs`
- **Emplacement** : lignes 308-311 (Main), 805-809 (MettreAJourEcran), 519-528 (seule partie du code soumise au budget)
- **Problème** : un seul compteur `versionDonnees` regroupe le stock, le gaz et l'énergie. Le texte de l'énergie contient des valeurs instantanées (`CurrentInput`, `CurrentOutput`, production solaire…) au centième de MW : il change presque à chaque exécution, donc `versionDonnees++` aussi. Chaque écran actif repasse alors dans `ConstruireLignesEcran` (ResoudreFiltres, puis filtrage et en-têtes de **toutes** les lignes de stock), même un écran qui n'affiche ni gaz ni énergie. Seule la collecte des inventaires vérifie `Runtime.CurrentInstructionCount` ; la construction des lignes de stock, le tri et la reconstruction des écrans ne le font pas.
- **Preuve** :
  ```csharp
  bool gazChange = MettreAJourGaz();
  bool energieChange = MettreAJourEnergie();
  if (gazChange || energieChange) versionDonnees++;
  ...
  if (ecran.VersionDonneesAppliquee != versionDonnees)
  {
      ConstruireLignesEcran(ecran);
  ```
  ```csharp
  tamponLignes.Add($"Charge : {entree:0.00} MW | Décharge : {sortie:0.00} MW");
  ...
  tamponLignes.Add($"{NOMS_PRODUCTEURS[i]} : {productionActuelle[i]:0.00} / {productionMax[i]:0.00} MW");
  ```
- **Risque** :
  - *Fait* : avec du solaire ou une charge variable, tous les écrans sont reconstruits à chaque exécution. L'optimisation « reconstruire seulement si les données ont changé » ne sert donc à rien.
  - *Supposition (ordre de grandeur)* : la collecte peut consommer jusqu'à 50 % du budget (25 000 instructions). Ensuite, environ 10 écrans × 100 lignes de stock × ~40 instructions par ligne donnent environ 40 000 instructions. Le total peut dépasser la limite d'environ 50 000, ce qui provoque l'exception « Script too complex » et arrête le PB.
- **Recommandation** : utiliser deux numéros de version, `versionStock` (stock et dictionnaire) et `versionGazEnergie`. Chaque écran mémorise les deux et ne refiltre le stock que si `versionStock` a changé (les lignes gaz et énergie se recopient à part). En plus, traiter les écrans par lots, ou les arrêter quand `Runtime.CurrentInstructionCount` dépasse un seuil (par ex. 80 %), et reprendre au tick suivant.

### M2 - Majeur - Tri poursuivi avec un dictionnaire en erreur ou absent

- **Fichier** : `TriInventaire.cs`
- **Emplacement** : 345-356 (RechargerDictionnaire), 289 (DictionnaireAChange), 577 (ResoudreCategorie), 728-731 (anti ping-pong)
- **Problème** : si `TryParse` échoue (par exemple à cause d'une **clé en double** ajoutée par le joueur, que MyIni refuse) ou si le serveur est détruit, les tables d'alias sont vidées mais le tri continue. Les coffres nommés avec des alias (`{minerai}[fer]`) reçoivent alors la catégorie brute `"minerai"`, qui ne correspond à aucun objet. Leur `DestinationAssociee.Accepte(...)` devient faux, `prioriteSource` vaut `int.MaxValue`, et tout leur contenu part vers n'importe quel coffre écrit avec les noms du jeu (`{ore}[*]`).
- **Preuve** :
  ```csharp
  aliasVersSubtype.Clear();
  aliasVersCategorie.Clear();
  ...
  if (!lecteurIni.TryParse(texte, out resultat))
  {
      etatDictionnaire = "ERREUR INI : " + resultat.ToString();
      return;   // le tri continue avec des tables vides
  }
  ```
  ```csharp
  int prioriteSource = int.MaxValue;
  if (source.DestinationAssociee != null && source.DestinationAssociee.Accepte(categorie, subtype))
  ```
- **Risque** : une simple faute de frappe dans le Custom Data du serveur, ou sa destruction, fait déplacer en masse le contenu de la base. Une fois la faute corrigée, il faut un second déplacement massif dans l'autre sens.
- **Recommandation** : quand le dictionnaire est en erreur (ou quand un serveur déjà vu a disparu), **conserver les tables précédentes**. On peut aussi suspendre `TraiterLotDeSources` et l'afficher dans la console (« Tri suspendu : dictionnaire invalide »). Concrètement, ne vider `aliasVersSubtype` et `aliasVersCategorie` qu'après un `TryParse` réussi.

### m1 - Mineur - Rescan non budgété, suivi d'un lot complet dans le même tick

- **Fichier** : `TriInventaire.cs`, lignes 243-248 et 436-462
- **Problème** : `Rescanner()` appelle deux fois `GetBlocksOfType` sur **tous** les blocs du réseau (`EstNommeServeur`, puis `EstBlocAutorise`, chaque callback compte des instructions), puis `ClasserBloc` et `AnalyserNomDestination` pour chaque bloc à inventaire. Juste après, `TraiterLotDeSources()` traite au moins une source avant de vérifier le budget.
- **Preuve** :
  ```csharp
  if (rescanDemande || tempsDepuisRescan >= INTERVALLE_RESCAN_SECONDES)
  {
      Rescanner();
  }
  TraiterLotDeSources();
  ```
- **Risque** (*supposition sur l'ampleur*) : sur une très grosse base (plusieurs milliers de blocs terminaux), le tick du rescan est le plus lourd et peut approcher la limite.
- **Recommandation** : sauter le lot de sources dans le tick où un rescan a eu lieu (`else TraiterLotDeSources();`), ou vérifier le budget **avant** la première source.

### m2 - Mineur - Travail inutile pour chaque pile d'objets

- **Fichier** : `TriInventaire.cs`, lignes 724-737 et 768
- **Problème** : pour chaque pile de chaque source, à chaque passage, le script alloue la clé `categorie + "/" + subtype` et appelle `GetItemAmount` (qui parcourt l'inventaire côté jeu). Il le fait avant même de savoir si une destination est éligible, alors que pour un objet déjà bien rangé, `candidates[0].Priorite >= prioriteSource` arrête tout de suite la boucle.
- **Preuve** :
  ```csharp
  string cle = categorie + "/" + subtype;           // allocation par pile
  ...
  MyFixedPoint quantiteRestanteVisee = inventaireSource.GetItemAmount(objet.Type) - objet.Amount;
  for (int index = 0; index < candidates.Count; index++)
  {
      if (destination.Priorite >= prioriteSource) break;
  ```
- **Risque** : du temps CPU et du travail pour le ramasse-miettes gaspillés en régime établi (la base est déjà triée). La plupart des piles sont dans ce cas.
- **Recommandation** : tester `candidates[0].Priorite >= prioriteSource` avant de calculer `quantiteRestanteVisee` (calcul paresseux au premier transfert). Utiliser comme clé de cache `MyItemType` directement (`Dictionary<MyItemType, List<DestinationTri>>`), ce qui supprime la concaténation.

### m3 - Mineur - Les éjecteurs sont des sources

- **Fichier** : `TriInventaire.cs`, lignes 485-494
- **Problème** : tout `IMyShipConnector` sans `[NoTri]` est ajouté aux sources.
- **Preuve** :
  ```csharp
  if (bloc is IMyCargoContainer || bloc is IMyShipConnector)
  {
      ...
      if (!exclu) AjouterSource(bloc, bloc.GetInventory(0), destination, null);
  ```
- **Risque** (*supposition* : l'éjecteur du jeu est un bloc de type `ShipConnector`, donc un `IMyShipConnector`. C'est le cas à ma connaissance, mais je ne l'ai pas vérifié dans l'API ici) : la pierre envoyée dans un éjecteur, ou dans un connecteur en « Throw Out », est reprise vers un coffre `{ore}[*]` et n'est jamais éjectée. *Fait* : l'original avait le même comportement (il parcourait tous les blocs), ce n'est donc pas une régression.
- **Recommandation** : ignorer comme source un connecteur dont `ThrowOut` est actif (`IMyShipConnector.ThrowOut`), ou le documenter clairement (« mettre `[NoTri]` sur les éjecteurs »).

### m4 - Mineur - Recherche du serveur différente entre les deux scripts

- **Fichiers** : `TriInventaire.cs` 297-316, `AffichageDataCenter.cs` 350
- **Problème** : Tri choisit en priorité le `SERVEUR_DICTIONNAIRE` de sa propre construction. Affichage prend le premier trouvé par `GetBlockWithName`, quelle que soit la grille.
- **Preuve** :
  ```csharp
  // Tri
  if (bloc.IsSameConstructAs(Me)) { serveurDictionnaire = bloc; break; }
  // Affichage
  serveurDictionnaire = GridTerminalSystem.GetBlockWithName(NOM_SERVEUR_DICTIONNAIRE);
  ```
- **Risque** : si un vaisseau amarré a aussi son `SERVEUR_DICTIONNAIRE`, les deux scripts peuvent lire deux dictionnaires différents (alias et noms incohérents), alors que la convention parle d'un serveur commun.
- **Recommandation** : reprendre dans Affichage la logique de Tri (construction d'abord, sinon le premier trouvé), idéalement sous forme d'une méthode commune copiée à l'identique.

### m5 - Mineur - Calcul sans écran actif

- **Fichier** : `AffichageDataCenter.cs`, lignes 297-311
- **Problème** : la collecte, la construction des lignes, le gaz et l'énergie tournent même si `ecrans` est vide ou si aucun écran n'est actif. L'original sortait tout de suite (`../../_old/gemini-code-1791364773398.cs` l. 91 : `if (ecransConfigures.Count == 0) return;`).
- **Preuve** : aucune condition avant `CollecterInventairesParLot()`, `MettreAJourGaz()` et `MettreAJourEnergie()`.
- **Risque** : un PB installé sans écran configuré consomme quand même du CPU. C'est une petite régression de performance par rapport à l'original.
- **Recommandation** : si aucun écran n'est actif (et sans rescan demandé), ne faire que le résumé `Echo`. Ne calculer le gaz et l'énergie que si au moins un écran a `AfficherGaz` ou `AfficherEnergie`.

### m6 - Mineur - Une exclusion par alias ignore la catégorie

- **Fichiers** : `AffichageDataCenter.cs` 968-973 ; `SERVEUR_DICTIONNAIRE.ini` 31
- **Problème** : `Exclusions=gravier` se traduit par l'alias `Stone`, et `SubtypesExclus` contient alors `Stone`. Cela masque `Ingot_Stone` (gravier) **et** `Ore_Stone` (pierre). La clé `Ingot_Stone` existe pourtant dans `[Affichage]`.
- **Preuve** :
  ```csharp
  if (dicoAlias.TryGetValue(terme, out subtypeId)) ecran.SubtypesExclus.Add(subtypeId);
  ```
  ```ini
  Stone=pierre, gravier, gravel, roche
  ```
- **Risque** : le joueur exclut le gravier et perd aussi l'affichage de la pierre (ou l'inverse). *Fait* : l'original (`Exclusions=Stone`) masquait déjà les deux, l'écart vient de la nouvelle couche d'alias.
- **Recommandation** : permettre l'exclusion par clé spécifique `Ingot_Stone` (déjà gérée via `TermesExclus`, l. 982) et le documenter, ou retirer « gravier » des alias de `Stone` dans `[Alias]` (il reste utile pour Tri avec `{ingot}[gravier]`, à arbitrer).

### m7 - Mineur - Un coffre de destination mal nommé est vidé

- **Fichier** : `TriInventaire.cs`, lignes 485-494 et 550-562
- **Problème** : quand `AnalyserNomDestination` renvoie `null` (accolades ou crochets incomplets), le coffre est quand même ajouté aux sources sans `DestinationAssociee`, donc tout ce qu'une autre destination accepte en est retiré.
- **Preuve** :
  ```csharp
  destination = AnalyserNomDestination(bloc);
  if (destination != null) destinations.Add(destination);
  ...
  if (!exclu) AjouterSource(bloc, bloc.GetInventory(0), destination, null);
  ```
- **Risque** : une faute de frappe dans le nom d'un coffre trié (par ex. `{ore}[*-stone` sans `]`) vide ce coffre. L'erreur est bien signalée dans la console, mais seulement les 5 premières.
- **Recommandation** : quand le nom contient `{` et que l'analyse échoue, ne pas considérer ce coffre comme source tant que l'erreur n'est pas corrigée (principe de prudence).

### m8 - Mineur - Réallocations à chaque rescan

- **Fichier** : `TriInventaire.cs`, lignes 444-454, 519, 574 (et 772 pour le cache)
- **Problème** : toutes les 10 s, toutes les `DestinationTri` (avec 2 `HashSet` chacune), toutes les `SourceTri` et toutes les listes du cache sont recréées, même si aucun nom n'a changé. C'est contraire à la règle de performance n° 3 du playbook (« Pas d'allocation inutile »).
- **Preuve** : `destinations.Clear(); sources.Clear(); cacheDestinationsParObjet.Clear();` puis `new DestinationTri()` / `new SourceTri()` pour chaque bloc.
- **Risque** : pression sur le ramasse-miettes côté serveur, surtout en multijoueur avec plusieurs PB.
- **Recommandation** : réutiliser les objets par `EntityId` (comme `ecransConnus` dans Affichage) et ne réanalyser un nom que si `CustomName` a changé.

### m9 - Mineur - Taille de tableau codée en dur

- **Fichier** : `AffichageDataCenter.cs`, lignes 229-231
- **Preuve** :
  ```csharp
  double[] productionActuelle = new double[5];
  double[] productionMax = new double[5];
  int[] nombreProducteurs = new int[5];
  ```
- **Risque** : si l'on ajoute un type dans `TYPES_PRODUCTEURS` et `NOMS_PRODUCTEURS` sans changer le `5`, on obtient une `IndexOutOfRangeException` dans `MettreAJourEnergie` (l. 720).
- **Recommandation** : `new double[NOMS_PRODUCTEURS.Length]` (possible, car les initialiseurs de champs readonly sont évalués dans l'ordre de déclaration et `NOMS_PRODUCTEURS` est déclaré avant, l. 108).

### i1 - Info - Périmètre des sources réduit (régression volontaire)

- **Fichier** : `TriInventaire.cs`, lignes 480-513
- **Fait** : l'original (`../../_old/gemini-code-1791364758252.cs` l. 61, 176-203) parcourait **tous** les blocs du réseau (`GetBlocks`), y compris les grilles amarrées, et **tous** leurs inventaires (`i < InventoryCount`). La nouvelle version ne vide plus les réacteurs, les générateurs O2/H2, les tourelles, les cockpits, les soudeuses, l'entrée des raffineries et assembleurs, ni les grilles amarrées. Elle ajoute en revanche les connecteurs comme destinations.
- **Commentaire** : c'est une amélioration (l'original affamait les raffineries et les réacteurs), documentée en tête de fichier. Mais c'est une perte de fonctionnalité pour qui comptait sur le vidage d'un vaisseau amarré : il faut le signaler dans la note.

### i2 - Info - Changements de comportement de l'affichage

- **Fichier** : `AffichageDataCenter.cs`
- **Faits** :
  - l. 862-867 : un `Mode` inconnu donne maintenant **Fixe** avec un avertissement. Dans l'original, toute valeur autre que `fixe` donnait Rotatif (`gemini…773398.cs` l. 134-144).
  - l. 1058-1059 : en Rotatif, le titre est `=== DATA CENTER (x/y) ===` au lieu de `=== ROTATION (x/y) ===`.
  - l. 1022-1029 : des en-têtes `--- LINGOTS ---` sont ajoutés quand l'écran affiche plusieurs catégories. Ils comptent dans la pagination Fixe, et un en-tête peut se retrouver en bas de page, séparé de ses lignes.
  - l. 354-372 : seulement cette construction (le contenu des vaisseaux amarrés n'est plus compté).
  - l. 1052-1060 : la rotation se base sur le temps de jeu cumulé, et non plus sur `DateTime.Now.Second`. C'est conforme au playbook.

### i3 - Info - Un seul écran par bloc, et conflit avec la règle par nom

- **Fichier** : `AffichageDataCenter.cs`, lignes 377-388 et 937-948
- **Fait** : il y a une seule `ConfigEcran` par `EntityId` (une seule section `[Ecran]` par Custom Data). Un cockpit ne peut donc pas afficher les stocks sur deux surfaces, ni partager ses surfaces entre deux rôles (convention commune).
- **Fait (hors périmètre, conflit possible)** : `DiagnosticStructurel.cs` l. 322 prend tout LCD nommé exactement `LCD_SCANNER`, quel que soit son Custom Data. Un LCD `LCD_SCANNER` dont le Custom Data contient `[Ecran]` sans `Role` serait donc piloté par les deux scripts. Le cas est peu probable, mais il contredit la règle « un écran = un seul script » (§4.2 du playbook).
- **Recommandation** : documenter la limite, ou passer plus tard à une section par surface (`[Ecran.0]`, `[Ecran.1]`…).

### i4 - Info - Code mort et doublons

- **Fichier** : `AffichageDataCenter.cs`
- l. 133 et 592 : `LigneStock.Quantite` est écrit mais jamais lu.
- l. 161-162 : `LignesParPage = 10` et `SecondesParPage = 8.0` répètent les constantes `LIGNES_PAR_PAGE_PAR_DEFAUT` et `SECONDES_PAR_PAGE_PAR_DEFAUT` (sans effet, car toujours écrasés dans `LireConfigEcran`, mais source de divergence).

### i5 - Info - Assembleur en désassemblage

- **Fichier** : `TriInventaire.cs`, ligne 697
- **Preuve** : `if (source.Assembleur != null && source.Assembleur.Mode == MyAssemblerMode.Disassembly) return;`
- **Fait** : en désassemblage, les composants obtenus arrivent dans l'inventaire d'**entrée** de l'assembleur, qui n'est jamais une source. Ils restent donc dans l'assembleur. L'original vidait les deux inventaires. C'est un choix défendable (pour ne pas voler ce qui est à désassembler), mais à documenter.

### i6 - Info - Cohérence de `SERVEUR_DICTIONNAIRE.ini`

**Faits (vérifiés) :**
- `[Categories]` : les clés sont en minuscules (`ore`, `ammomagazine`…). Tri les passe en minuscules (`LireSectionListeAlias`, l. 382) et les compare au TypeId en minuscules (l. 787). Affichage les compare sans tenir compte de la casse à `"Ore"` / `"AmmoMagazine"` (`HashSet` OrdinalIgnoreCase, l. 172). Les deux sont cohérents.
- `[Alias]` : clé = SubtypeId, valeur = alias séparés par `,`. Tri (l. 385-391) et Affichage (l. 464-469, via `AjouterAlias`) font le même découpage et le même `Trim`.
- `[Affichage]` : les clés spécifiques `Ore_Stone`, `Ingot_Stone`… correspondent exactement à `CleSpecifique = Categorie + "_" + SubtypeId` (l. 590, `Categorie` sans le préfixe et avec sa casse d'origine) et sont recherchées en priorité (l. 500).
- Aucune clé n'est en double dans une même section (MyIni refuserait le fichier entier, voir M2).
- Les SubtypeId utilisés existent en vanilla à ma connaissance (`Stone` minerai et lingot, `Scrap` minerai et lingot, `OxygenBottle` dans OxygenContainerObject, `HydrogenBottle` dans GasContainerObject, `NATO_25x184mm`, `Missile200mm`…).
- Le fichier est identique au bloc `ini` de la note [[Serveur Dictionnaire]], et les blocs C# des notes [[Tri Automatique des Inventaires]] et [[Affichage Data Center]] sont identiques aux `.cs` (vérifié par `diff`).

**Suppositions / manques (non bloquants) :**
- Les munitions et objets récents (`NATO_5p56x45mm`, `LargeCalibreAmmo`, `MediumCalibreAmmo`, `AutocannonClip`, munitions de railgun, composants Prototech…) n'ont ni alias ni nom affiché : ils s'afficheront avec leur SubtypeId.
- Les catégories `Datapad`, `Package` et `PhysicalObject` (crédits spatiaux) ne sont pas dans `[Categories]`. Elles restent utilisables par leur nom de jeu (`{physicalobject}[*]`).
- `Ore_Scrap=Ferraille` (l. 91) fait doublon avec la clé générique `Scrap=Ferraille` (l. 106).

### i7 - Info - Compilation C# 6 et API

**Faits :**
- Une recherche (grep) sur `out var`, le pattern matching `is T x`, `default` littéral, les tuples, `var`, `using`/`namespace` et LINQ ne trouve rien dans les deux fichiers. Pas de fonction locale. Les chaînes interpolées (`$"..."`), y compris les accolades échappées `{{{destination.Categorie}}}` (Tri l. 838), sont valides en C# 6.
- `const UpdateFrequency FREQUENCE_EXECUTION` (Tri l. 64) : une constante d'énumération est légale.
- `GetBlocksOfType<IMyTerminalBlock>(liste, méthode)` : pas d'ambiguïté entre les deux surcharges (la surcharge non générique en `List<IMyTerminalBlock>` est plus spécifique).
- API utilisées et existantes dans l'API Ingame (à ma connaissance) : `IsSameConstructAs`, `GetBlockWithId`, `IMyInventory.IsFull / CanTransferItemTo(IMyInventory, MyItemType) / TransferItemTo(IMyInventory, MyInventoryItem, MyFixedPoint?) / GetItemAmount(MyItemType)`, `IMyProductionBlock.OutputInventory`, `IMyAssembler.Mode`, `MyAssemblerMode.Disassembly`, `IMyCollector`, `IMyShipDrill`, `IMyShipGrinder`, `IMyTextSurfaceProvider.SurfaceCount / GetSurface`, `ContentType.TEXT_AND_IMAGE` (espace de noms corrigé), `IMyEntity.Closed`, `BlockDefinition.SubtypeId / TypeIdString`, `MyIni.TryParse / GetKeys / ContainsSection / Get().ToInt32 / ToDouble / ToBoolean`.
- Tailles : 34 004 et 46 507 caractères, sous la limite de 100 000.

**Suppositions :**
- `Array.Clear` (Affichage l. 703-705) et `StringComparer.OrdinalIgnoreCase` sont supposés autorisés par la liste blanche du PB. Ils le sont dans les scripts publics courants, mais je ne l'ai pas vérifié ici.
- La conversion `(double)objet.Amount` (Affichage l. 545) suppose un opérateur explicite `MyFixedPoint` vers `double`. L'original l'utilisait déjà.

---

## Points positifs notables (pour mémoire)

- La priorité par défaut est corrigée (`LirePriorite`, Tri l. 622-631) : l'original donnait 0 au lieu de 99 quand le texte était illisible.
- Le grand réservoir d'O2 vanilla (SubtypeId vide) est correctement classé, contrairement à l'original (Affichage l. 661).
- Les batteries ne sont plus comptées deux fois dans les producteurs (Affichage l. 363).
- Le filtre `Role` respecte la convention §4.2. L'écriture n'est faite que si le texte change, et le Custom Data n'est reparsé que s'il a changé.
- Le tri a un repli vers la destination suivante quand un coffre est plein ou non relié (Tri l. 739-759). L'original abandonnait.

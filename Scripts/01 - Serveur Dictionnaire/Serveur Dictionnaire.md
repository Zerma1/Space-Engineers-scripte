---
tags:
  - space-engineers
  - data-center
  - configuration
  - optimise
fichier: SERVEUR_DICTIONNAIRE.ini
original: "[[_old/Scripte/guide_du_tri_automatique_et_data_center_se|guide_du_tri_automatique_et_data_center_se]]"
---
# Serveur Dictionnaire

> [!summary] Résumé
> Un seul bloc, nommé `SERVEUR_DICTIONNAIRE`, contient dans son **Custom Data** toutes les traductions du Data Center : alias de catégories (`minerai` → `ore`), alias d'objets (`fer` → `Iron`) et noms affichés sur les écrans (`Iron` → `Fer`). Les scripts le relisent automatiquement dès que vous le modifiez : plus besoin de toucher au code.

Notes d'origine : [[_old/Scripte/guide_du_tri_automatique_et_data_center_se|guide_du_tri_automatique_et_data_center_se]] (ancienne section `[Dictionnaire]`) et [[_old/Scripte/Gestion de l'Affichage et Traductions|Gestion de l'Affichage et Traductions]] (ancienne section `[Affichage]`).

---

## 1. Quels scripts l'utilisent ?

| Script | Sections lues | Usage |
|---|---|---|
| [[Tri Automatique des Inventaires]] | `[Categories]`, `[Alias]` (+ ancienne `[Dictionnaire]`) | Comprendre les noms de coffres : `{minerai}[fer;pierre]0` |
| [[Affichage Data Center]] | `[Affichage]` (+ `[Categories]`, `[Alias]` selon le script) | Afficher `Fer`, `Gravier`… au lieu de `Iron`, `Stone` |

> [!info] Sans serveur
> Les scripts fonctionnent quand même, avec les noms du jeu (`{ore}[Iron]`, affichage `Iron`). Ils le signalent dans leur console (Echo).

## 2. Le format en 3 sections

### 2.1 `[Categories]` — les grandes familles d'objets

Clé = catégorie du jeu (le `TypeId` sans `MyObjectBuilder_`, en minuscules). Valeur = vos alias, séparés par des virgules.

```ini
[Categories]
ore=minerai, minerais
```

| Catégorie du jeu | Contenu | Alias fournis |
|---|---|---|
| `ore` | Minerais (pierre, fer, glace…) | minerai, minerais |
| `ingot` | Lingots (et le gravier = `Ingot/Stone`) | lingot, lingots |
| `component` | Composants de construction | composant, composants |
| `ammomagazine` | Munitions | munition, munitions, ammo |
| `physicalgunobject` | Outils portatifs et armes | outil, outils, arme, armes |
| `oxygencontainerobject` | Bouteilles d'oxygène | bouteille o2, bouteille oxygene |
| `gascontainerobject` | Bouteilles d'hydrogène | bouteille h2, bouteille hydrogene |
| `consumableitem` | Consommables (kits, boissons…) | consommable, consommables |
| `gasproperties` | Gaz des réservoirs (`Oxygen`, `Hydrogen`) | gaz, gas |

> [!warning] Correction de l'ancien dictionnaire
> L'ancienne ligne `ammo=Ammo` était **invalide** : `Ammo` n'est pas un objet du jeu. Les munitions forment une **catégorie** (`ammomagazine`). `ammo`, `munition` et `munitions` sont donc désormais des alias de catégorie : `{munitions}[*]`.

### 2.2 `[Alias]` — les objets

Clé = `SubtypeId` exact du jeu. Valeur = vos alias (français, anglais, symboles chimiques, avec ou sans accents).

```ini
[Alias]
Iron=fer, fe
```

- Le `SubtypeId` est **toujours** son propre alias : `Iron` marche même sans ligne.
- Les alias sont comparés **sans tenir compte des majuscules** (`FER`, `Fer`, `fer`).
- Un même `SubtypeId` sert au minerai **et** au lingot : c'est la catégorie entre `{ }` qui fait la différence (`{ore}[fer]` = minerai de fer, `{ingot}[fer]` = lingot de fer). C'est pour cela que `pierre` et `gravier` pointent tous les deux vers `Stone`.

> [!tip] Pourquoi les alias sont dans la VALEUR et plus dans la clé ?
> L'ancien format (`plaque d'acier=SteelPlate`) mettait des espaces, apostrophes et accents dans les **clés** INI, ce qui est fragile et oblige à répéter le `SubtypeId` sur chaque ligne. Le nouveau format regroupe tout par objet : une ligne = un objet, facile à relire et à compléter.

### 2.3 `[Affichage]` — les noms sur les écrans

```ini
[Affichage]
Iron=Fer
Ingot_Stone=Gravier
Ore_Stone=Pierre
```

| Forme de clé | Exemple | Priorité |
|---|---|---|
| `Categorie_SubtypeId` (spécifique) | `Ingot_Stone=Gravier` | **Prioritaire** |
| `SubtypeId` (générique) | `Stone=Pierre` | Utilisée si aucune clé spécifique |

La clé spécifique sert quand un même `SubtypeId` change de sens selon la catégorie : `Stone` est de la **pierre** en minerai mais du **gravier** en lingot ; `Scrap` est de la **ferraille** en minerai et de la **vieille ferraille** en lingot.

### 2.4 Compatibilité avec l'ancienne section `[Dictionnaire]`

Si votre serveur contient encore l'ancienne section (`alias=SubtypeId`), elle est **toujours lue** par le script de tri. En cas de conflit, le nouveau format (`[Alias]`) l'emporte. Vous pouvez donc migrer à votre rythme, puis supprimer `[Dictionnaire]`.

### 2.5 Rechargement automatique

Les scripts mémorisent le dernier texte lu et le comparent à chaque exécution. Dès que vous **modifiez et validez** le Custom Data du serveur, le dictionnaire est relu et les noms des coffres sont réinterprétés — sans recompiler. Un serveur construit **après** le script est détecté au rescan périodique suivant (environ 10 s) ou avec l'argument `rescan`.

## 3. Installation

1. Construisez un bloc quelconque sur la base : bloc programmable, écran, petit conteneur… (un bloc qui ne sert à rien d'autre est idéal).
2. Renommez-le exactement **`SERVEUR_DICTIONNAIRE`** (majuscules comprises).
3. Ouvrez ses **Données Personnalisées** (Custom Data), effacez tout, collez le bloc complet de la section 6.
4. Validez. Les scripts clients le prennent en compte tout seuls (leur console affiche `Dictionnaire : OK (...)`).

> [!warning] Un seul serveur par construction
> Si deux blocs s'appellent `SERVEUR_DICTIONNAIRE`, les scripts prennent le premier trouvé sur **leur** construction. Le serveur d'un vaisseau amarré n'est utilisé que si la base n'en a pas.

## 4. Ajouter un objet (mod ou objet oublié)

1. Trouvez le **`SubtypeId`** exact : nommez un coffre de test `{ore}[*]` et regardez ce qu'il récupère, ou consultez la page du mod / les fichiers `.sbc` du mod (`<SubtypeId>...</SubtypeId>`).
2. Ajoutez une ligne dans `[Alias]` : `MonMinerai=mon alias, autre alias`.
3. Ajoutez sa traduction dans `[Affichage]` : `MonMinerai=Mon minerai` (ou `Ore_MonMinerai=...` / `Ingot_MonMinerai=...` si le nom diffère entre minerai et lingot).
4. Pour une **catégorie** de mod, ajoutez-la dans `[Categories]` : `nomdutypeid=alias` (TypeId sans `MyObjectBuilder_`, en minuscules).

> [!tip] Règle d'or
> N'écrivez jamais un `SubtypeId` deviné : une faute de frappe ne provoque aucune erreur, l'objet n'est simplement jamais reconnu.

## 5. Objets ajoutés par rapport à l'ancien dictionnaire

| Ajout | Section | Raison |
|---|---|---|
| `BulletproofGlass`, `Explosives`, `Canvas`, `ZoneChip` | `[Alias]`, `[Affichage]` | Composants vanilla absents de l'ancien dictionnaire |
| `OxygenBottle`, `HydrogenBottle`, `NATO_25x184mm`, `Missile200mm` | `[Alias]`, `[Affichage]` | Bouteilles et munitions vanilla courantes |
| `gasproperties`, `Oxygen`, `Hydrogen` | `[Categories]`, `[Alias]`, `[Affichage]` | Les gaz eux-mêmes (contenu des réservoirs), distincts des bouteilles |
| Traductions de **tous** les minerais, lingots et composants | `[Affichage]` | L'ancienne section n'en traduisait que 6 |
| `Ore_Stone`, `Ingot_Stone`, `Ore_Ice`, `Ore_Scrap`, `Ingot_Scrap` | `[Affichage]` | Distinguer pierre / gravier, ferraille / vieille ferraille |
| `ammo=Ammo` → `ammomagazine=…, ammo` | `[Categories]` | Correction : `Ammo` n'est pas un `SubtypeId` |
| Quelques alias (`poutrelle`, `supraconducteur`, `roche`, `déchets`…) | `[Alias]` | Variantes françaises courantes |

## 6. Custom Data complet

À coller tel quel dans le bloc `SERVEUR_DICTIONNAIRE` (identique au fichier `SERVEUR_DICTIONNAIRE.ini`) :

```ini
; ==============================================================================
; SERVEUR_DICTIONNAIRE - Custom Data du Data Center (nouveau format)
; A coller dans les Donnees Personnalisees d'un bloc nomme SERVEUR_DICTIONNAIRE.
; Les CLES sont toujours des identifiants du jeu (sans espace).
; Les ALIAS du joueur vont dans la VALEUR, separes par des virgules.
; Les alias sont compares sans tenir compte des majuscules.
; ==============================================================================

[Categories]
; CategorieDuJeu=alias1, alias2
; (CategorieDuJeu = TypeId sans "MyObjectBuilder_", en minuscules)
; Utilise dans le nom des coffres : {minerai}[*] equivaut a {ore}[*]
ore=minerai, minerais
ingot=lingot, lingots
component=composant, composants
; Les munitions sont une CATEGORIE (et non un objet) : "ammo=Ammo" de l'ancien
; dictionnaire etait invalide, il est corrige ici.
ammomagazine=munition, munitions, ammo
physicalgunobject=outil, outils, arme, armes
oxygencontainerobject=bouteille o2, bouteille oxygene
gascontainerobject=bouteille h2, bouteille hydrogene
consumableitem=consommable, consommables
; Gaz stockes dans les reservoirs (MyObjectBuilder_GasProperties/Oxygen, /Hydrogen)
gasproperties=gaz, gas

[Alias]
; SubtypeId=alias1, alias2, ...
; Le SubtypeId est toujours son propre alias (taper "Iron" marche toujours).
; Un meme SubtypeId sert pour le minerai ET le lingot (Iron = minerai de fer
; dans {ore}, lingot de fer dans {ingot}).

; --- Pierre / gravier / glace ---
Stone=pierre, gravier, gravel, roche
Ice=glace

; --- Minerais et lingots metalliques ---
Iron=fer, fe
Nickel=ni
Silicon=si, silicium
Cobalt=co
Magnesium=mg, magnésium
Silver=ag, argent
Gold=au, or
Platinum=pt, platine
Uranium=u

; --- Dechets / ferraille ---
Scrap=ferraille, scrap metal, dechet, dechets, déchet, déchets

; --- Composants de base ---
SteelPlate=plaque d'acier, plaque d acier, plaque, steel plate
Construction=composant de construction
InteriorPlate=plaque interieure, plaque intérieure, interior plate
Motor=moteur
Computer=ordinateur, pc
SmallTube=petit tube, small tube
LargeTube=grand tube, large tube
Display=affichage, ecran, écran
Girder=poutre, poutrelle
MetalGrid=grille metallique, grille métallique, metal grid, grille
BulletproofGlass=verre blinde, verre blindé, bulletproof glass, verre
Explosives=explosif, explosifs
Canvas=toile
ZoneChip=puce de zone, zone chip, puce

; --- Composants avances ---
Thrust=composant de propulseur, propulseur
Reactor=composant de reacteur, composant de réacteur, reacteur, réacteur
SolarCell=cellule solaire, solar cell
PowerCell=cellule d'energie, cellule d'énergie, power cell, batterie
Superconductor=superconducteur, supraconducteur
Medical=composant medical, composant médical, médical
RadioCommunication=composant de communication, radio, communication
Detector=composant de detecteur, composant de détecteur, detecteur, détecteur
GravityGenerator=composant de gravite, composant de gravité, gravity, gravité, gravite

; --- Bouteilles ---
OxygenBottle=bouteille d'oxygene, bouteille d'oxygène
HydrogenBottle=bouteille d'hydrogene, bouteille d'hydrogène

; --- Gaz (categorie gasproperties, contenu des reservoirs) ---
Oxygen=oxygene, oxygène, o2, oxygen
Hydrogen=hydrogene, hydrogène, h2, hydrogen

; --- Munitions (categorie ammomagazine) ---
NATO_25x184mm=gatling, munition gatling
Missile200mm=missile, missiles, roquette, roquettes

[Affichage]
; SubtypeId=Nom affiche              (cle generique, minerai ET lingot)
; Categorie_SubtypeId=Nom affiche    (cle specifique, PRIORITAIRE sur la generique)

; --- Cles specifiques (meme SubtypeId, sens different selon la categorie) ---
Ore_Stone=Pierre
Ingot_Stone=Gravier
Ore_Ice=Glace
Ore_Scrap=Ferraille
Ingot_Scrap=Vieille ferraille

; --- Minerais / lingots (cles generiques) ---
Stone=Pierre
Ice=Glace
Iron=Fer
Nickel=Nickel
Silicon=Silicium
Cobalt=Cobalt
Magnesium=Magnésium
Silver=Argent
Gold=Or
Platinum=Platine
Uranium=Uranium
Scrap=Ferraille

; --- Composants ---
SteelPlate=Plaque d'acier
Construction=Composant de construction
InteriorPlate=Plaque intérieure
Motor=Moteur
Computer=Ordinateur
SmallTube=Petit tube d'acier
LargeTube=Grand tube d'acier
Display=Écran
Girder=Poutrelle
MetalGrid=Grille métallique
BulletproofGlass=Verre blindé
Explosives=Explosifs
Canvas=Toile
ZoneChip=Puce de zone
Thrust=Composant de propulseur
Reactor=Composant de réacteur
SolarCell=Cellule solaire
PowerCell=Cellule d'énergie
Superconductor=Supraconducteur
Medical=Composant médical
RadioCommunication=Composant radio
Detector=Composant de détecteur
GravityGenerator=Composant de gravité

; --- Bouteilles ---
OxygenBottle=Bouteille d'oxygène
HydrogenBottle=Bouteille d'hydrogène

; --- Gaz ---
Oxygen=O2
Hydrogen=H2

; --- Munitions ---
NATO_25x184mm=Munitions Gatling
Missile200mm=Missiles 200 mm
```

## Voir aussi

- [[Tri Automatique des Inventaires]] — utilise `[Categories]` et `[Alias]`
- [[Affichage Data Center]] — utilise `[Affichage]`
- [[Conventions du projet]]
- Notes d'origine : [[_old/Scripte/guide_du_tri_automatique_et_data_center_se|guide_du_tri_automatique_et_data_center_se]], [[_old/Scripte/Gestion de l'Affichage et Traductions|Gestion de l'Affichage et Traductions]]

---
tags: [space-engineers, script, optimise, production]
script: ProductionAutomatique.cs
---
# Production Automatique (quotas Minimum / Maximum)

> [!summary] Résumé
> Garde en permanence chaque objet configuré entre un **Minimum** et un **Maximum** : sous le Minimum, le script commande la production jusqu'au Maximum ; au Maximum, il l'arrête. Les coffres ne débordent pas et les lingots ne sont pas vidés pour rien. Les objets fabriqués (munitions, composants…) passent par la **file d'attente des assembleurs** ; les lingots par la **mise en marche / arrêt de raffineries dédiées**. Assembleurs et raffineries peuvent être dédiés à un quota par un groupe.

## 1. Prérequis en jeu

| Bloc | Nom / configuration | Obligatoire ? |
|---|---|---|
| Bloc programmable | n'importe quel nom ; contient ce script et sa configuration (Custom Data) | oui |
| Assembleur(s) | aucun nom imposé ; groupe facultatif pour les dédier à un quota | oui, pour les objets fabriqués |
| Raffinerie(s) | **groupe obligatoire** pour un quota de lingot | oui, pour les lingots |
| Écran(s) | Custom Data `[Ecran] Role=Production` (+ `Surface=N` pour un cockpit) | non (sans écran : tout est dans Echo) |

```ini
[Ecran]
Role=Production
```

> [!info] Écrans séparés
> `Role=Production` est réservé à ce script (voir [[Conventions du projet]] §4.2).

## 2. Installation

1. Coller le code de la section 9 dans un bloc programmable dédié, **Vérifier**, puis **OK**.
2. Au premier lancement, le script écrit un **exemple de configuration** dans le Custom Data du bloc programmable s'il est vide.
3. Adapter le Custom Data (section 3). Le script relit la configuration tout seul dès qu'elle change.
4. Facultatif : créer un groupe d'assembleurs (ex. `Assembleurs munitions`) et/ou de raffineries (ex. `Raffineries fer`), puis l'indiquer dans `Groupe=` du quota.
5. Vérifier le résumé dans la fenêtre Echo : chaque quota y a une ligne d'état.

## 3. Configuration

### 3.1 Custom Data du bloc programmable

```ini
[Production]
; Groupe d'assembleurs des quotas sans Groupe= (vide = tous les assembleurs
; de la construction qui ne sont pas dédiés à un autre quota).
Assembleurs=
; Groupe de raffineries des quotas de lingots sans Groupe=.
Raffineries=

[Quota Munitions autocanon]
Objet=AmmoMagazine/AutocannonClip
Minimum=10000
Maximum=12000
Groupe=Assembleurs munitions
Plan=

[Quota Plaques]
Objet=Component/SteelPlate
Minimum=2000
Maximum=5000

[Quota Fer]
Objet=Ingot/Iron
Minimum=50000
Maximum=100000
Groupe=Raffineries fer
```

| Clé | Rôle |
|---|---|
| `[Quota <nom>]` | une section par objet ; le nom est libre et sert à l'affichage |
| `Objet` | `Type/Sous-type` : `AmmoMagazine`, `Component`, `Ingot`, `PhysicalGunObject` (outils), `OxygenContainerObject`, `GasContainerObject`, `ConsumableItem`, `PhysicalObject`. La casse du type est corrigée ; celle du **sous-type doit être exacte** |
| `Minimum` (X) | la production est relancée quand le stock descend **à** X ou en dessous |
| `Maximum` (Y) | la production s'arrête quand le stock atteint Y. Il faut `0 <= X <= Y` et `Y > 0` |
| `Groupe` | facultatif : assembleurs dédiés (objets fabriqués) ou raffineries dédiées (**obligatoire** pour un lingot, sauf si `[Production] Raffineries=` est rempli) |
| `Plan` | facultatif : nom du plan (blueprint) quand la détection automatique échoue (Echo affiche « plan introuvable ») |

Quelques sous-types courants : `SteelPlate`, `Construction`, `InteriorPlate`, `MetalGrid`, `SmallTube`, `LargeTube`, `Motor`, `Computer`, `Display`, `BulletproofGlass` (composants) ; `NATO_25x184mm`, `Missile200mm`, `AutocannonClip` (munitions) ; `Iron`, `Nickel`, `Silicon`, `Cobalt`, `Uranium` (lingots).

### 3.2 Constantes en tête du script

| Constante | Défaut | Rôle |
|---|---|---|
| `SEULEMENT_CETTE_CONSTRUCTION` | `true` | ignore les vaisseaux amarrés (ni comptés, ni pilotés) |
| `ROLE_ECRAN` | `Production` | valeur de `Role` des écrans de ce script |
| `BLOCS_PAR_EXECUTION` | 200 | blocs à inventaire lus par exécution |
| `BUDGET_INSTRUCTIONS` | 30 000 | arrêt du lot avant la limite du jeu |
| `INTERVALLE_RESCAN_SECONDES` | 30 | relecture automatique des blocs, groupes et écrans |

## 4. Commandes

| Argument | Effet |
|---|---|
| `rescan` | relit tout de suite la configuration, les blocs, les groupes et les écrans (après une construction ou un renommage) |

## 5. Fonctionnement

1. **Comptage** : le stock d'un objet est la somme de **tous** les inventaires de la construction (coffres, connecteurs, sorties d'assembleurs, cockpits…), lue par lots de 200 blocs.
2. **Hystérésis**, à la fin de chaque passage complet :
   - stock ≥ Maximum → arrêt ;
   - stock ≤ Minimum → production ;
   - entre les deux → on garde la décision précédente : une remontée commencée va jusqu'au Maximum, et un stock qui baisse n'est relancé qu'au Minimum.
3. **Objets fabriqués** : en production, le script commande `Maximum − stock − déjà en file` à l'assembleur le moins chargé (allumé, en mode Assemblage, capable de fabriquer le plan). Les assembleurs en mode coopératif se partagent le travail eux-mêmes. Au Maximum, le plan est retiré de la file de tous les assembleurs du quota.
4. **Lingots** : une raffinerie d'un groupe piloté est allumée si **au moins un** de ses quotas est en production, éteinte sinon. Les raffineries qui ne sont dans aucun groupe de quota ne sont jamais touchées.
5. **Plan** : le jeu ne donne pas aux scripts le plan qui fabrique un objet. Sans `Plan=`, le script essaie le sous-type, puis sous-type + `Component`, puis sous-type + `Magazine` (ex. `Computer` → `ComputerComponent`, `NATO_25x184mm` → `NATO_25x184mmMagazine`), et garde le premier qu'un assembleur du quota accepte (`CanUseBlueprint`).
6. **Sauvegarde** : les quotas en cours de production sont gardés dans le `Storage` du bloc, pour que la remontée continue après un rechargement de la partie.

Exemple avec `Minimum=10000`, `Maximum=12000` : à 9 990 munitions, le script commande 2 010 munitions ; à 12 000, il vide la file ; les tirs font redescendre le stock à 11 000, 10 500… sans rien relancer ; à 10 000, il recommande 2 000.

## 6. Optimisations

- **Cache** : blocs, groupes, plans et écrans relus toutes les 30 s, après `rescan` ou quand le Custom Data change, jamais à chaque exécution.
- **Comptage par lots** avec budget d'instructions ; `GetItemAmount` fait l'addition côté jeu au lieu de parcourir chaque pile d'objets.
- **Aucune allocation** dans la boucle : listes, `HashSet`, `MyIni` et `StringBuilder` sont des champs réutilisés.
- **Écritures conditionnelles** : `Enabled` des raffineries et texte des écrans seulement s'ils changent ; la file n'est modifiée que s'il manque au moins 1 objet ou au Maximum.
- **Pas de surcommande** : ce qui est déjà en file est déduit avant chaque commande.

## 7. Limites connues

- **Assembleurs partagés** : sur un assembleur géré par un quota, le script compte aussi les commandes passées à la main pour le même plan, et les retire quand le stock atteint le Maximum.
- **Plans** : un plan qui fabrique plusieurs objets par unité ferait dépasser le Maximum (cas non rencontré pour les munitions et les composants de base, non vérifié pour les objets de mods). Les objets de mods demandent souvent `Plan=`.
- **Sous-type `AutocannonClip`** (munitions d'autocanon) : à vérifier en jeu ; s'il est faux, Echo affiche « plan introuvable » et rien n'est produit.
- **Ressources** : le script ne connaît pas le coût en lingots d'un plan (l'API ne le donne pas). Pour ne pas vider un lingot, lui donner son propre quota avec ses raffineries ; un assembleur sans lingots reste simplement en attente.
- **Raffineries** : elles raffinent tout le minerai qu'elles reçoivent. Pour qu'un groupe ne produise que du fer, il faut qu'il ne reçoive que du minerai de fer (tri, connecteurs dédiés).
- **Retard** : la décision tombe à la fin d'un passage complet ; sur une très grande station (plus de 200 blocs à inventaire), compter quelques exécutions de 1,6 s. Le stock peut dépasser le Maximum de ce qui est sorti de l'assembleur pendant ce temps.
- Non testé en jeu.

## 8. Dépannage

| Symptôme (Echo / écran) | Cause | Solution |
|---|---|---|
| `Aucune section [Quota ...]` | Custom Data sans quota | ajouter une section `[Quota Nom]` |
| `Objet= doit être Type/Sous-type` | clé `Objet` absente ou sans `/` | ex. `Objet=Component/SteelPlate` |
| `il faut 0 <= Minimum <= Maximum` | seuils absents ou inversés | corriger `Minimum` / `Maximum` |
| `plan introuvable : préciser Plan=` | sous-type mal écrit (casse !) ou plan au nom inhabituel | vérifier le sous-type, sinon remplir `Plan=` |
| `groupe '...' introuvable` | nom de groupe différent, ou groupe sur un vaisseau amarré | vérifier le nom exact, puis `rescan` |
| `aucune raffinerie : mettre Groupe=` | quota de lingot sans groupe | créer un groupe de raffineries dédiées |
| `BLOQUÉ : aucun assembleur allumé en mode Assemblage` | assembleurs éteints, en Désassemblage, ou incapables de fabriquer le plan | allumer un assembleur, le passer en Assemblage |
| `PRODUCTION (file N)` mais rien n'avance | pas assez de lingots dans l'assembleur | vérifier les lingots, ou ajouter un quota de lingot |
| Le stock reste à 0 alors que les coffres sont pleins | sous-type mal écrit (casse exacte) | corriger `Objet=` |

## 9. Code complet

> [!info] Classe partagée
> La classe `EcransRole` est une copie conforme de celle de [[Controle des Avaries]] et de [[Surveillance Carburant Uranium]].

```csharp
// Copyright (c) 2026 Zerma1 - https://github.com/Zerma1/Space-Engineers-scripte
// Licence : PolyForm Noncommercial 1.0.0 - https://polyformproject.org/licenses/noncommercial/1.0.0
// Usage et partage gratuits autorisés, vente interdite. / Free use and sharing allowed, selling prohibited.
// ==============================================================================
// SCRIPT : PRODUCTION AUTOMATIQUE PAR QUOTAS (Minimum / Maximum)
// ------------------------------------------------------------------------------
// Rôle :
//   Maintient le stock de chaque objet configuré entre un Minimum et un Maximum
//   (stock = total de tous les inventaires de la construction) :
//   - stock <= Minimum  -> la production est relancée, jusqu'au Maximum ;
//   - stock >= Maximum  -> la production s'arrête, pour ne pas remplir les
//                          coffres ni épuiser les lingots ;
//   - entre les deux    -> on termine ce qui a été commencé (hystérésis) : on
//                          ne relance pas l'assembleur pour 1 objet à chaque tir.
//   Objets fabriqués (munitions, composants, outils...) : le script ajoute ou
//   retire le plan dans la file d'attente des assembleurs.
//   Lingots : le script allume ou éteint un groupe de raffineries dédiées.
//
// Nomenclature des blocs :
//   - Configuration : Custom Data de CE bloc programmable. Un exemple y est
//     écrit au premier lancement si le Custom Data est vide :
//       [Quota Munitions autocanon]
//       Objet=AmmoMagazine/AutocannonClip
//       Minimum=10000
//       Maximum=12000
//       Groupe=          (optionnel : assembleurs / raffineries dédiés)
//       Plan=            (optionnel : nom du plan si la détection échoue)
//   - Écrans (optionnel) : Custom Data   [Ecran]
//                                        Role=Production
//                                        Surface=0   (optionnel)
//
// Commandes (argument du bloc programmable, majuscules/minuscules indifférentes) :
//   - rescan : relit immédiatement configuration, blocs, groupes et écrans.
//
// Dépendances : aucune.
// ==============================================================================

// ------------------------------------------------------------------------------
// CONSTANTES DE CONFIGURATION (toutes les valeurs réglables sont ici)
// ------------------------------------------------------------------------------

// true = ignore les vaisseaux amarrés par connecteur (on ne compte et ne pilote que le nôtre).
const bool SEULEMENT_CETTE_CONSTRUCTION = true;

// Valeur de la clé Role des écrans de ce script ([Ecran] Role=Production).
const string ROLE_ECRAN = "Production";

// Section des réglages généraux dans le Custom Data.
const string SECTION_GENERALE = "Production";

// Toute section dont le nom commence par ce mot est un quota : [Quota <nom libre>].
const string PREFIXE_SECTION_QUOTA = "Quota";

// Nombre maximal de blocs à inventaire lus par exécution (comptage par lots).
const int BLOCS_PAR_EXECUTION = 200;

// Sécurité : on arrête le lot si le script a déjà consommé autant d'instructions
// (la limite du jeu est donnée par Runtime.MaxInstructionCount).
const int BUDGET_INSTRUCTIONS = 30000;

// Intervalle minimal entre deux relectures automatiques de la grille (secondes de jeu).
const double INTERVALLE_RESCAN_SECONDES = 30.0;

// Préfixes des identifiants du jeu : on laisse le joueur écrire "Ingot/Iron"
// au lieu de "MyObjectBuilder_Ingot/Iron".
const string PREFIXE_TYPE = "MyObjectBuilder_";
const string PREFIXE_PLAN = "MyObjectBuilder_BlueprintDefinition/";

// Types d'objets acceptés dans Objet= (la casse saisie par le joueur est corrigée).
readonly string[] TYPES_CONNUS = { "AmmoMagazine", "Component", "Ingot", "Ore",
    "PhysicalGunObject", "OxygenContainerObject", "GasContainerObject",
    "ConsumableItem", "PhysicalObject" };

// Détection automatique du plan quand Plan= est vide : on essaie le sous-type
// tel quel, puis avec ces suffixes (ex. Computer -> ComputerComponent,
// NATO_25x184mm -> NATO_25x184mmMagazine). Le premier plan accepté par un
// assembleur du quota (CanUseBlueprint) est retenu.
readonly string[] SUFFIXES_PLAN = { "", "Component", "Magazine" };

// Configuration écrite dans le Custom Data s'il est vide au premier lancement.
const string CONFIG_EXEMPLE =
@"[Production]
; Groupe d'assembleurs utilisés par les quotas sans Groupe=.
; Vide = tous les assembleurs de la construction qui ne sont pas
; déjà dédiés à un quota par son Groupe=.
Assembleurs=
; Groupe de raffineries utilisé par les quotas de lingots sans Groupe=.
Raffineries=

; Un quota par section [Quota <nom libre>].
; Production relancée quand le stock descend à Minimum,
; arrêtée quand il atteint Maximum (Minimum <= Maximum).
[Quota Munitions autocanon]
; Objet : Type/Sous-type (AmmoMagazine, Component, Ingot...)
Objet=AmmoMagazine/AutocannonClip
Minimum=10000
Maximum=12000
; Optionnel : nom du groupe d'assembleurs dédiés à ce quota
; (pour un lingot : groupe de raffineries dédiées, obligatoire).
Groupe=
; Optionnel : nom du plan si la détection automatique échoue.
Plan=

; Exemple de lingot (enlever les ; pour l'activer) :
;[Quota Fer]
;Objet=Ingot/Iron
;Minimum=50000
;Maximum=100000
;Groupe=Raffineries fer
";

// ------------------------------------------------------------------------------
// ÉTAT DU SCRIPT (champs réutilisés d'une exécution à l'autre)
// ------------------------------------------------------------------------------

// Quotas lus dans le Custom Data (reconstruits seulement s'il change).
readonly List<Quota> quotas = new List<Quota>();
// Erreurs de lecture du Custom Data, affichées dans Echo et sur les écrans.
readonly List<string> erreursConfiguration = new List<string>();
// Cache des blocs qui ont un inventaire (parcouru par lots pour compter les stocks).
readonly List<IMyTerminalBlock> blocsAvecInventaire = new List<IMyTerminalBlock>();
// Toutes les raffineries pilotées par au moins un quota de lingot (sans doublon).
readonly List<IMyRefinery> raffineriesPilotees = new List<IMyRefinery>();
// EntityId des raffineries pilotées (pour éviter les doublons dans la liste ci-dessus).
readonly HashSet<long> idsRaffineriesPilotees = new HashSet<long>();
// EntityId des raffineries qu'au moins un quota veut allumées (recalculé à chaque passage).
readonly HashSet<long> idsRaffineriesDemandees = new HashSet<long>();
// EntityId des assembleurs dédiés à un quota par Groupe= (exclus du lot par défaut).
readonly HashSet<long> idsAssembleursDedies = new HashSet<long>();
// Noms des quotas en cours de production : restaurés depuis Storage au chargement,
// pour garder l'hystérésis après un rechargement de la partie.
readonly HashSet<string> nomsEnProduction = new HashSet<string>();
// Tampons réutilisés (pas d'allocation à chaque exécution).
readonly List<IMyAssembler> tamponAssembleurs = new List<IMyAssembler>();
readonly List<IMyRefinery> tamponRaffineries = new List<IMyRefinery>();
readonly List<MyProductionItem> tamponFile = new List<MyProductionItem>();
readonly List<string> tamponSections = new List<string>();
readonly MyIni lecteurIni = new MyIni();
readonly StringBuilder constructeurTexte = new StringBuilder();

// Écrans [Ecran] Role=Production (voir la classe en fin de script).
readonly EcransRole ecransProduction;

string dernierCustomDataLu = null;  // null = configuration jamais lue
string groupeAssembleursDefaut = ""; // [Production] Assembleurs=
string groupeRaffineriesDefaut = ""; // [Production] Raffineries=
int indexProchainBloc = 0;          // position du prochain bloc à lire dans le cache
double secondesDepuisRescan = 0;    // temps de jeu écoulé depuis le dernier rescan
bool rescanDemande = true;          // commande "rescan" reçue (ou premier lancement)
int nombrePassagesTermines = 0;     // compteur pour Echo (montre que le script tourne)
string messageCommande = "";        // retour de la dernière commande, pour Echo

/// <summary>
/// Un quota : un objet dont le stock doit rester entre Minimum et Maximum,
/// avec les blocs qui le produisent et le résultat du dernier comptage.
/// </summary>
class Quota
{
    public string Nom;                  // nom libre de la section [Quota Nom]
    public MyItemType TypeObjet;        // objet compté dans les inventaires
    public string SousType;             // ex. "AutocannonClip" (sert à deviner le plan)
    public bool EstLingot;              // true = raffineries allumées / éteintes
    public double Minimum;
    public double Maximum;
    public string NomGroupe;            // Groupe= ("" = blocs par défaut)
    public string PlanDemande;          // Plan= ("" = détection automatique)

    // Résolus au rescan.
    public readonly List<IMyAssembler> Assembleurs = new List<IMyAssembler>();
    public readonly List<IMyRefinery> Raffineries = new List<IMyRefinery>();
    public MyDefinitionId Plan;         // plan (blueprint) mis en file d'attente
    public bool PlanTrouve;
    public string Erreur = "";          // "" = quota utilisable

    // Comptage.
    public double StockEnCours;         // somme du passage en cours
    public double Stock;                // résultat du dernier passage complet
    public bool StockConnu;             // au moins un passage complet depuis la création
    public double EnFile;               // quantité déjà en file d'attente (assembleurs)

    // Décision.
    public bool EnProduction;           // hystérésis : on produit jusqu'au Maximum
    public bool AucunAssembleurDisponible; // en production mais aucun assembleur allumé en mode Assemblage
}

/// <summary>
/// Constructeur : appelé une seule fois à la compilation / au chargement.
/// Update100 = une exécution tous les 100 ticks (~1,6 s) : largement assez
/// réactif pour des stocks, sans charger le serveur.
/// </summary>
public Program()
{
    ecransProduction = new EcransRole(ROLE_ECRAN);

    // Restaure les quotas qui étaient en production à la dernière sauvegarde.
    foreach (string nom in (Storage ?? "").Split('\n'))
    {
        if (nom.Length > 0) nomsEnProduction.Add(nom);
    }

    // Premier lancement : on écrit un exemple commenté que le joueur n'a plus qu'à adapter.
    if (string.IsNullOrWhiteSpace(Me.CustomData))
    {
        Me.CustomData = CONFIG_EXEMPLE;
    }

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

/// <summary>
/// Sauvegarde du jeu : on garde seulement les noms des quotas en production,
/// pour ne pas perdre l'hystérésis (un stock entre Min et Max qui était en
/// train de remonter doit continuer à remonter après le rechargement).
/// </summary>
public void Save()
{
    // Configuration jamais lue (sauvegarde avant la 1re exécution) : on ne touche à rien.
    if (dernierCustomDataLu == null) return;

    constructeurTexte.Clear();
    foreach (Quota quota in quotas)
    {
        if (quota.EnProduction) constructeurTexte.Append(quota.Nom).Append('\n');
    }
    Storage = constructeurTexte.ToString();
}

/// <summary>
/// Boucle principale. À chaque exécution :
/// 1. traiter la commande éventuelle (rescan) ;
/// 2. rescanner si demandé, si le Custom Data a changé, ou périodiquement
///    (seulement entre deux passages pour ne pas fausser un comptage) ;
/// 3. compter un lot d'inventaires ; si le passage est complet, appliquer les quotas ;
/// 4. afficher l'état dans Echo et sur les écrans.
/// </summary>
public void Main(string argument, UpdateType updateSource)
{
    TraiterCommande(argument);

    // Temps de JEU écoulé (et non l'horloge du PC) : cohérent même si le serveur ralentit.
    secondesDepuisRescan += Runtime.TimeSinceLastRun.TotalSeconds;

    if (rescanDemande
        || Me.CustomData != dernierCustomDataLu
        || (indexProchainBloc == 0 && secondesDepuisRescan >= INTERVALLE_RESCAN_SECONDES))
    {
        Rescanner();
    }

    if (CompterLotInventaires())
    {
        AppliquerQuotas();
    }

    AfficherEtat();
}

// ------------------------------------------------------------------------------
// COMMANDES
// ------------------------------------------------------------------------------

/// <summary>Traite l'argument du bloc programmable (seule commande : rescan).</summary>
void TraiterCommande(string argument)
{
    string commande = (argument ?? "").Trim().ToLowerInvariant();
    if (commande.Length == 0) return;

    if (commande == "rescan")
    {
        rescanDemande = true;
        messageCommande = "Rescan effectué.";
    }
    else
    {
        messageCommande = "Commande inconnue : '" + argument.Trim() + "' (rescan)";
    }
}

// ------------------------------------------------------------------------------
// RESCAN : configuration, blocs, groupes, plans, écrans
// ------------------------------------------------------------------------------

/// <summary>
/// Relit tout ce qui ne change qu'à la construction ou à la configuration,
/// puis recommence un passage de comptage depuis le début.
/// </summary>
void Rescanner()
{
    rescanDemande = false;
    secondesDepuisRescan = 0;

    LireConfiguration();

    // Tous les blocs à inventaire : coffres, connecteurs, assembleurs (leur
    // sortie contient les objets finis), raffineries (lingots), cockpits...
    blocsAvecInventaire.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocsAvecInventaire,
        b => b.HasInventory && EstDeCetteConstruction(b));

    TrouverBlocsDeProduction();

    ecransProduction.Rechercher(GridTerminalSystem, Me, SEULEMENT_CETTE_CONSTRUCTION);

    // Nouveau passage : un comptage commencé avec l'ancienne liste de blocs serait faux.
    indexProchainBloc = 0;
    foreach (Quota quota in quotas) quota.StockEnCours = 0;
}

/// <summary>Filtre de construction : écarte les vaisseaux amarrés par connecteur.</summary>
bool EstDeCetteConstruction(IMyTerminalBlock bloc)
{
    return !SEULEMENT_CETTE_CONSTRUCTION || bloc.IsSameConstructAs(Me);
}

/// <summary>
/// Lit le Custom Data du bloc programmable s'il a changé depuis la dernière
/// lecture, et reconstruit la liste des quotas. L'état "en production" d'un
/// quota est conservé si son nom ne change pas (modifier un seuil ne relance
/// ni n'interrompt rien).
/// </summary>
void LireConfiguration()
{
    string texte = Me.CustomData;
    if (texte == dernierCustomDataLu) return;
    dernierCustomDataLu = texte;

    // Mémorise l'état des anciens quotas (au premier passage, la liste vient de Storage).
    if (quotas.Count > 0)
    {
        nomsEnProduction.Clear();
        foreach (Quota ancien in quotas)
        {
            if (ancien.EnProduction) nomsEnProduction.Add(ancien.Nom);
        }
    }

    quotas.Clear();
    erreursConfiguration.Clear();
    groupeAssembleursDefaut = "";
    groupeRaffineriesDefaut = "";

    MyIniParseResult resultat;
    if (!lecteurIni.TryParse(texte, out resultat))
    {
        erreursConfiguration.Add("Custom Data illisible, ligne " + resultat.LineNo + " : " + resultat.Error);
        return;
    }

    groupeAssembleursDefaut = lecteurIni.Get(SECTION_GENERALE, "Assembleurs").ToString("").Trim();
    groupeRaffineriesDefaut = lecteurIni.Get(SECTION_GENERALE, "Raffineries").ToString("").Trim();

    tamponSections.Clear();
    lecteurIni.GetSections(tamponSections);
    foreach (string section in tamponSections)
    {
        if (!section.StartsWith(PREFIXE_SECTION_QUOTA, StringComparison.OrdinalIgnoreCase)) continue;

        Quota quota = LireQuota(section);
        if (quota != null) quotas.Add(quota);
    }

    if (quotas.Count == 0 && erreursConfiguration.Count == 0)
    {
        erreursConfiguration.Add("Aucune section [Quota ...] dans le Custom Data.");
    }
}

/// <summary>
/// Lit une section [Quota Nom]. Renvoie null (et note l'erreur) si l'objet
/// ou les seuils sont invalides : un quota mal écrit ne doit rien piloter.
/// </summary>
Quota LireQuota(string section)
{
    string nom = section.Substring(PREFIXE_SECTION_QUOTA.Length).Trim();
    if (nom.Length == 0) nom = section;

    // Objet=Type/Sous-type
    string objet = lecteurIni.Get(section, "Objet").ToString("").Trim();
    int barre = objet.IndexOf('/');
    if (barre <= 0 || barre == objet.Length - 1)
    {
        erreursConfiguration.Add("[" + section + "] Objet= doit être Type/Sous-type (ex. Component/SteelPlate).");
        return null;
    }
    string typeSaisi = objet.Substring(0, barre).Trim();
    string sousType = objet.Substring(barre + 1).Trim();
    if (typeSaisi.StartsWith(PREFIXE_TYPE, StringComparison.OrdinalIgnoreCase))
    {
        typeSaisi = typeSaisi.Substring(PREFIXE_TYPE.Length);
    }

    // On remet la casse officielle du type (le jeu compare les identifiants à la casse près).
    string type = null;
    foreach (string typeConnu in TYPES_CONNUS)
    {
        if (string.Equals(typeConnu, typeSaisi, StringComparison.OrdinalIgnoreCase)) type = typeConnu;
    }
    if (type == null)
    {
        erreursConfiguration.Add("[" + section + "] type '" + typeSaisi + "' inconnu (AmmoMagazine, Component, Ingot...).");
        return null;
    }
    if (type == "Ore")
    {
        erreursConfiguration.Add("[" + section + "] un minerai ne se fabrique pas.");
        return null;
    }

    // Seuils : -1 = clé absente ou illisible.
    double minimum = lecteurIni.Get(section, "Minimum").ToDouble(-1);
    double maximum = lecteurIni.Get(section, "Maximum").ToDouble(-1);
    if (minimum < 0 || maximum <= 0 || minimum > maximum)
    {
        erreursConfiguration.Add("[" + section + "] il faut 0 <= Minimum <= Maximum et Maximum > 0.");
        return null;
    }

    Quota quota = new Quota();
    quota.Nom = nom;
    quota.TypeObjet = new MyItemType(PREFIXE_TYPE + type, sousType);
    quota.SousType = sousType;
    quota.EstLingot = type == "Ingot";
    quota.Minimum = minimum;
    quota.Maximum = maximum;
    quota.NomGroupe = lecteurIni.Get(section, "Groupe").ToString("").Trim();
    quota.PlanDemande = lecteurIni.Get(section, "Plan").ToString("").Trim();
    quota.EnProduction = nomsEnProduction.Contains(nom);
    return quota;
}

/// <summary>
/// Associe à chaque quota ses assembleurs ou ses raffineries, puis résout
/// le plan (blueprint) des quotas d'assembleurs.
/// 1. Groupes dédiés (Groupe=) d'abord : ces assembleurs sont retirés du lot par défaut.
/// 2. Quotas sans Groupe= : lot par défaut ([Production] Assembleurs= / Raffineries=).
/// 3. Plans et liste des raffineries pilotées.
/// </summary>
void TrouverBlocsDeProduction()
{
    idsAssembleursDedies.Clear();
    raffineriesPilotees.Clear();
    idsRaffineriesPilotees.Clear();

    // 1. Groupes dédiés.
    foreach (Quota quota in quotas)
    {
        quota.Assembleurs.Clear();
        quota.Raffineries.Clear();
        quota.PlanTrouve = false;
        quota.Erreur = "";
        if (quota.NomGroupe.Length == 0) continue;

        string erreur = ChargerGroupe(quota.NomGroupe, quota.EstLingot, quota.Assembleurs, quota.Raffineries);
        if (erreur != null) quota.Erreur = erreur;
        foreach (IMyAssembler assembleur in quota.Assembleurs) idsAssembleursDedies.Add(assembleur.EntityId);
    }

    // 2. Lots par défaut.
    tamponAssembleurs.Clear();
    tamponRaffineries.Clear();
    string erreurAssembleurs = null;
    string erreurRaffineries = "aucune raffinerie : mettre Groupe= (ou [Production] Raffineries=)";
    if (groupeAssembleursDefaut.Length > 0)
    {
        erreurAssembleurs = ChargerGroupe(groupeAssembleursDefaut, false, tamponAssembleurs, tamponRaffineries);
    }
    else
    {
        GridTerminalSystem.GetBlocksOfType<IMyAssembler>(tamponAssembleurs,
            a => EstDeCetteConstruction(a) && !idsAssembleursDedies.Contains(a.EntityId));
    }
    if (groupeRaffineriesDefaut.Length > 0)
    {
        erreurRaffineries = ChargerGroupe(groupeRaffineriesDefaut, true, tamponAssembleurs, tamponRaffineries);
    }

    foreach (Quota quota in quotas)
    {
        if (quota.NomGroupe.Length > 0) continue;
        if (quota.EstLingot)
        {
            // Pas d'allumage / extinction de TOUTES les raffineries pour un seul
            // lingot : elles raffinent n'importe quel minerai. Groupe obligatoire.
            if (erreurRaffineries != null) quota.Erreur = erreurRaffineries;
            else quota.Raffineries.AddRange(tamponRaffineries);
        }
        else
        {
            if (erreurAssembleurs != null) quota.Erreur = erreurAssembleurs;
            else quota.Assembleurs.AddRange(tamponAssembleurs);
        }
    }
    tamponAssembleurs.Clear();
    tamponRaffineries.Clear();

    // 3. Plans et raffineries pilotées.
    foreach (Quota quota in quotas)
    {
        if (quota.Erreur.Length > 0) continue;

        if (quota.EstLingot)
        {
            if (quota.Raffineries.Count == 0)
            {
                quota.Erreur = "aucune raffinerie dans le groupe";
                continue;
            }
            foreach (IMyRefinery raffinerie in quota.Raffineries)
            {
                if (idsRaffineriesPilotees.Add(raffinerie.EntityId)) raffineriesPilotees.Add(raffinerie);
            }
        }
        else
        {
            if (quota.Assembleurs.Count == 0)
            {
                quota.Erreur = "aucun assembleur";
                continue;
            }
            ResoudrePlan(quota);
            if (!quota.PlanTrouve)
            {
                quota.Erreur = quota.PlanDemande.Length > 0
                    ? "plan '" + quota.PlanDemande + "' refusé par les assembleurs"
                    : "plan introuvable : préciser Plan=";
            }
        }
    }
}

/// <summary>
/// Remplit la liste des assembleurs du groupe nommé, ou celle des
/// raffineries si <paramref name="chercherRaffineries"/> vaut true.
/// Renvoie un message d'erreur, ou null si le groupe est utilisable.
/// </summary>
string ChargerGroupe(string nomGroupe, bool chercherRaffineries,
    List<IMyAssembler> assembleurs, List<IMyRefinery> raffineries)
{
    IMyBlockGroup groupe = GridTerminalSystem.GetBlockGroupWithName(nomGroupe);
    if (groupe == null) return "groupe '" + nomGroupe + "' introuvable";

    if (chercherRaffineries)
    {
        groupe.GetBlocksOfType<IMyRefinery>(raffineries, r => EstDeCetteConstruction(r));
        if (raffineries.Count == 0) return "aucune raffinerie dans '" + nomGroupe + "'";
    }
    else
    {
        groupe.GetBlocksOfType<IMyAssembler>(assembleurs, a => EstDeCetteConstruction(a));
        if (assembleurs.Count == 0) return "aucun assembleur dans '" + nomGroupe + "'";
    }
    return null;
}

/// <summary>
/// Trouve le plan (blueprint) qui fabrique l'objet du quota. Le jeu ne donne
/// pas la correspondance objet -> plan aux scripts : on essaie Plan= s'il est
/// rempli, sinon le sous-type avec les suffixes courants, et on garde le premier
/// qu'un des assembleurs du quota sait fabriquer (CanUseBlueprint).
/// </summary>
void ResoudrePlan(Quota quota)
{
    if (quota.PlanDemande.Length > 0)
    {
        EssayerPlan(quota, quota.PlanDemande);
        return;
    }
    foreach (string suffixe in SUFFIXES_PLAN)
    {
        if (EssayerPlan(quota, quota.SousType + suffixe)) return;
    }
}

/// <summary>Retient ce plan si un assembleur du quota peut le fabriquer.</summary>
bool EssayerPlan(Quota quota, string nomPlan)
{
    MyDefinitionId plan;
    if (!MyDefinitionId.TryParse(PREFIXE_PLAN + nomPlan, out plan)) return false;

    foreach (IMyAssembler assembleur in quota.Assembleurs)
    {
        if (assembleur.CanUseBlueprint(plan))
        {
            quota.Plan = plan;
            quota.PlanTrouve = true;
            return true;
        }
    }
    return false;
}

// ------------------------------------------------------------------------------
// COMPTAGE DES STOCKS (par lots, sur plusieurs exécutions)
// ------------------------------------------------------------------------------

/// <summary>
/// Additionne, pour chaque quota, la quantité présente dans un lot de blocs.
/// Renvoie true quand le passage est complet : les stocks sont alors à jour
/// et on peut décider. Le lot s'arrête aussi si le budget d'instructions est
/// atteint (grandes stations).
/// </summary>
bool CompterLotInventaires()
{
    if (quotas.Count == 0) return false;

    int blocsLus = 0;
    while (indexProchainBloc < blocsAvecInventaire.Count)
    {
        if (blocsLus >= BLOCS_PAR_EXECUTION || Runtime.CurrentInstructionCount >= BUDGET_INSTRUCTIONS)
        {
            return false;   // suite à la prochaine exécution
        }

        IMyTerminalBlock bloc = blocsAvecInventaire[indexProchainBloc];
        indexProchainBloc++;
        blocsLus++;
        if (bloc.Closed) continue;  // détruit ou démonté depuis le rescan

        // GetItemAmount fait l'addition côté jeu : bien moins d'instructions
        // que de parcourir chaque pile d'objets avec GetItems.
        for (int i = 0; i < bloc.InventoryCount; i++)
        {
            IMyInventory inventaire = bloc.GetInventory(i);
            foreach (Quota quota in quotas)
            {
                quota.StockEnCours += (double)inventaire.GetItemAmount(quota.TypeObjet);
            }
        }
    }

    // Passage complet : on publie les totaux et on repart de zéro.
    foreach (Quota quota in quotas)
    {
        quota.Stock = quota.StockEnCours;
        quota.StockEnCours = 0;
        quota.StockConnu = true;
    }
    indexProchainBloc = 0;
    nombrePassagesTermines++;
    return true;
}

// ------------------------------------------------------------------------------
// DÉCISIONS : file d'attente des assembleurs, marche / arrêt des raffineries
// ------------------------------------------------------------------------------

/// <summary>
/// Applique l'hystérésis de chaque quota puis pilote les blocs :
/// - stock >= Maximum -> arrêt ; stock <= Minimum -> production ;
///   entre les deux -> on garde la décision précédente ;
/// - quotas de lingots : une raffinerie est allumée si AU MOINS un de ses
///   quotas la demande (plusieurs lingots peuvent partager un groupe).
/// </summary>
void AppliquerQuotas()
{
    idsRaffineriesDemandees.Clear();

    foreach (Quota quota in quotas)
    {
        if (quota.Erreur.Length > 0) continue;

        if (quota.Stock >= quota.Maximum) quota.EnProduction = false;
        else if (quota.Stock <= quota.Minimum) quota.EnProduction = true;

        if (quota.EstLingot)
        {
            if (!quota.EnProduction) continue;
            foreach (IMyRefinery raffinerie in quota.Raffineries) idsRaffineriesDemandees.Add(raffinerie.EntityId);
        }
        else
        {
            GererFileAssembleurs(quota);
        }
    }

    // Écriture conditionnelle : Enabled seulement s'il change.
    foreach (IMyRefinery raffinerie in raffineriesPilotees)
    {
        if (raffinerie.Closed) continue;
        bool allumer = idsRaffineriesDemandees.Contains(raffinerie.EntityId);
        if (raffinerie.Enabled != allumer) raffinerie.Enabled = allumer;
    }
}

/// <summary>
/// Quota d'assembleurs : compte ce qui est déjà en file d'attente pour ce plan,
/// puis ajoute juste ce qui manque pour atteindre le Maximum (en production),
/// ou vide la file de ce plan (stock au Maximum).
/// La commande est confiée à l'assembleur le moins chargé ; les assembleurs en
/// mode coopératif se partagent ensuite le travail eux-mêmes.
/// </summary>
void GererFileAssembleurs(Quota quota)
{
    quota.EnFile = 0;
    IMyAssembler moinsCharge = null;
    int plusPetiteFile = int.MaxValue;

    foreach (IMyAssembler assembleur in quota.Assembleurs)
    {
        if (assembleur.Closed || !assembleur.IsFunctional) continue;

        tamponFile.Clear();
        assembleur.GetQueue(tamponFile);
        foreach (MyProductionItem element in tamponFile)
        {
            if (element.BlueprintId == quota.Plan) quota.EnFile += (double)element.Amount;
        }

        // Candidat pour une nouvelle commande : allumé, en mode Assemblage
        // (pas Désassemblage) et capable de fabriquer ce plan.
        if (assembleur.Enabled
            && assembleur.Mode == MyAssemblerMode.Assembly
            && tamponFile.Count < plusPetiteFile
            && assembleur.CanUseBlueprint(quota.Plan))
        {
            moinsCharge = assembleur;
            plusPetiteFile = tamponFile.Count;
        }
    }

    quota.AucunAssembleurDisponible = false;
    if (quota.EnProduction)
    {
        // Ce qui manque = Maximum - stock - déjà commandé : on ne recommande pas
        // à chaque passage ce qui est déjà dans la file.
        double manque = Math.Ceiling(quota.Maximum - quota.Stock - quota.EnFile);
        if (manque < 1) return;

        if (moinsCharge == null)
        {
            quota.AucunAssembleurDisponible = true;
            return;
        }
        int quantite = manque > int.MaxValue ? int.MaxValue : (int)manque;
        moinsCharge.AddQueueItem(quota.Plan, (MyFixedPoint)quantite);
        quota.EnFile += quantite;
    }
    else if (quota.Stock >= quota.Maximum && quota.EnFile > 0)
    {
        // Seulement au Maximum : entre Min et Max (en attente), on ne touche pas
        // aux commandes que le joueur aurait passées à la main.
        RetirerDeLaFile(quota);
    }
}

/// <summary>Retire le plan du quota de la file d'attente de tous ses assembleurs.</summary>
void RetirerDeLaFile(Quota quota)
{
    foreach (IMyAssembler assembleur in quota.Assembleurs)
    {
        if (assembleur.Closed) continue;

        tamponFile.Clear();
        assembleur.GetQueue(tamponFile);
        // De la fin vers le début : retirer un élément ne décale pas ceux qui restent à voir.
        for (int i = tamponFile.Count - 1; i >= 0; i--)
        {
            if (tamponFile[i].BlueprintId == quota.Plan) assembleur.RemoveQueueItem(i, tamponFile[i].Amount);
        }
    }
    quota.EnFile = 0;
}

// ------------------------------------------------------------------------------
// AFFICHAGE (écrans Role=Production et Echo)
// ------------------------------------------------------------------------------

/// <summary>
/// Construit le rapport (une ligne d'état par quota, puis les erreurs),
/// l'écrit sur les écrans (seulement s'il a changé) et dans Echo avec
/// les compteurs techniques.
/// </summary>
void AfficherEtat()
{
    constructeurTexte.Clear();
    constructeurTexte.Append("=== PRODUCTION AUTOMATIQUE ===\n");

    foreach (Quota quota in quotas)
    {
        constructeurTexte.Append(quota.Nom).Append('\n');
        constructeurTexte.Append("  ");
        if (quota.StockConnu) constructeurTexte.Append(FormaterQuantite(quota.Stock));
        else constructeurTexte.Append('?');
        constructeurTexte.Append(" / ").Append(FormaterQuantite(quota.Minimum))
                         .Append('-').Append(FormaterQuantite(quota.Maximum)).Append(" : ");
        AjouterEtatQuota(quota);
        constructeurTexte.Append('\n');
    }

    foreach (string erreur in erreursConfiguration)
    {
        constructeurTexte.Append("ERREUR ").Append(erreur).Append('\n');
    }

    string rapport = constructeurTexte.ToString();
    ecransProduction.Ecrire(rapport);

    constructeurTexte.Append("---\n");
    constructeurTexte.Append("Blocs à inventaire : ").Append(blocsAvecInventaire.Count).Append('\n');
    constructeurTexte.Append("Écrans Role=").Append(ROLE_ECRAN).Append(" : ").Append(ecransProduction.Nombre);
    if (ecransProduction.ErreursConfiguration > 0)
    {
        constructeurTexte.Append(" (").Append(ecransProduction.ErreursConfiguration).Append(" Surface= invalide)");
    }
    constructeurTexte.Append('\n');
    constructeurTexte.Append("Passages : ").Append(nombrePassagesTermines)
                     .Append(" | ").Append(Runtime.LastRunTimeMs.ToString("0.000")).Append(" ms\n");
    if (messageCommande.Length > 0) constructeurTexte.Append(messageCommande).Append('\n');
    Echo(constructeurTexte.ToString());
}

/// <summary>Ajoute au rapport l'état lisible d'un quota.</summary>
void AjouterEtatQuota(Quota quota)
{
    if (quota.Erreur.Length > 0)
    {
        constructeurTexte.Append("ERREUR ").Append(quota.Erreur);
    }
    else if (!quota.StockConnu)
    {
        constructeurTexte.Append("comptage...");
    }
    else if (quota.EnProduction)
    {
        if (quota.EstLingot)
        {
            constructeurTexte.Append("PRODUCTION (").Append(quota.Raffineries.Count).Append(" raffinerie(s))");
        }
        else if (quota.AucunAssembleurDisponible)
        {
            constructeurTexte.Append("BLOQUÉ : aucun assembleur allumé en mode Assemblage");
        }
        else
        {
            constructeurTexte.Append("PRODUCTION (file ").Append(FormaterQuantite(quota.EnFile)).Append(')');
        }
    }
    else if (quota.Stock >= quota.Maximum)
    {
        constructeurTexte.Append("arrêt (plein)");
    }
    else
    {
        constructeurTexte.Append("arrêt (reprise sous le Minimum)");
    }
}

/// <summary>Quantité lisible : entier au-delà de 100, deux décimales en dessous (lingots).</summary>
string FormaterQuantite(double quantite)
{
    return quantite >= 100 ? Math.Floor(quantite).ToString("0") : quantite.ToString("0.##");
}

// ==============================================================================
// CLASSE UTILITAIRE : ÉCRANS CONFIGURÉS PAR CUSTOM DATA ([Ecran] Role=...)
// (même code dans "Contrôle des Avaries", "Gestionnaire de Carburant" et
// "Production Automatique")
// ==============================================================================

/// <summary>
/// Trouve et alimente les écrans dont le Custom Data contient
/// [Ecran] Role=&lt;role&gt; (et optionnellement Surface=N).
/// Gère les écrans LCD (IMyTextPanel) ET les blocs à plusieurs surfaces
/// (cockpits, bloc programmable... via IMyTextSurfaceProvider).
/// N'écrit sur un écran que si le texte a changé depuis la dernière écriture.
/// </summary>
public class EcransRole
{
    const string SECTION_ECRAN = "Ecran";
    const string CLE_ROLE = "Role";
    const string CLE_SURFACE = "Surface";

    // Rôle recherché (comparaison insensible à la casse, comme l'original).
    readonly string role;
    readonly MyIni lecteurIni = new MyIni();
    readonly List<IMyTerminalBlock> tamponRecherche = new List<IMyTerminalBlock>();
    readonly List<IMyTextSurface> surfaces = new List<IMyTextSurface>();

    // Dernier texte écrit sur chaque surface. Conservé d'un rescan à l'autre
    // pour ne pas réécrire inutilement tous les écrans après chaque rescan.
    Dictionary<IMyTextSurface, string> textesEcrits = new Dictionary<IMyTextSurface, string>();
    Dictionary<IMyTextSurface, string> tamponTextes = new Dictionary<IMyTextSurface, string>();

    /// <summary>Nombre de surfaces trouvées au dernier scan.</summary>
    public int Nombre { get { return surfaces.Count; } }

    /// <summary>Nombre de blocs Role=... dont la surface demandée n'existe pas.</summary>
    public int ErreursConfiguration { get; private set; }

    /// <summary>Crée le gestionnaire pour un rôle donné (ex. "Avaries").</summary>
    public EcransRole(string role)
    {
        this.role = role;
    }

    /// <summary>
    /// Rescanne la grille pour retrouver les écrans de ce rôle.
    /// À appeler lors du rescan périodique uniquement (lecture de Custom Data).
    /// </summary>
    public void Rechercher(IMyGridTerminalSystem grille, IMyTerminalBlock moi, bool seulementCetteConstruction)
    {
        surfaces.Clear();
        tamponTextes.Clear();
        ErreursConfiguration = 0;

        // Pré-filtre bon marché : on ne parse que les Custom Data contenant "Role".
        tamponRecherche.Clear();
        grille.GetBlocksOfType<IMyTerminalBlock>(tamponRecherche,
            b => (!seulementCetteConstruction || b.IsSameConstructAs(moi)) && b.CustomData.Contains(CLE_ROLE));

        foreach (IMyTerminalBlock bloc in tamponRecherche)
        {
            if (!lecteurIni.TryParse(bloc.CustomData)) continue;

            string roleLu = lecteurIni.Get(SECTION_ECRAN, CLE_ROLE).ToString("").Trim();
            if (!string.Equals(roleLu, role, StringComparison.OrdinalIgnoreCase)) continue;

            IMyTextSurface surface = null;
            IMyTextPanel panneau = bloc as IMyTextPanel;
            if (panneau != null)
            {
                // Un écran LCD classique EST lui-même une surface de texte.
                surface = panneau;
            }
            else
            {
                // Cockpit, bloc programmable... : on prend la surface demandée.
                IMyTextSurfaceProvider fournisseur = bloc as IMyTextSurfaceProvider;
                int index = lecteurIni.Get(SECTION_ECRAN, CLE_SURFACE).ToInt32(0);
                if (fournisseur != null && index >= 0 && index < fournisseur.SurfaceCount)
                {
                    surface = fournisseur.GetSurface(index);
                }
            }

            if (surface == null)
            {
                ErreursConfiguration++;
                continue;
            }

            surfaces.Add(surface);
            // On récupère le dernier texte connu de cette surface (s'il existe).
            string ancienTexte;
            if (textesEcrits.TryGetValue(surface, out ancienTexte)) tamponTextes[surface] = ancienTexte;
        }
        tamponRecherche.Clear();

        // Échange des deux dictionnaires : les surfaces disparues sont oubliées.
        Dictionary<IMyTextSurface, string> echange = textesEcrits;
        textesEcrits = tamponTextes;
        tamponTextes = echange;
    }

    /// <summary>
    /// Écrit le texte sur toutes les surfaces, seulement là où il a changé.
    /// Force aussi le mode texte (ContentType.TEXT_AND_IMAGE) si nécessaire.
    /// </summary>
    public void Ecrire(string texte)
    {
        foreach (IMyTextSurface surface in surfaces)
        {
            if (surface.ContentType != ContentType.TEXT_AND_IMAGE)
            {
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
            }

            string dernier;
            if (textesEcrits.TryGetValue(surface, out dernier) && dernier == texte) continue;

            surface.WriteText(texte);
            textesEcrits[surface] = texte;
        }
    }
}
```

## Voir aussi

- [[00 - Sommaire]] — tous les scripts
- [[Conventions du projet]] — rôles des écrans, règles communes
- [[Tri Automatique des Inventaires]] — range les objets produits dans les coffres
- [[Affichage Data Center]] — affichage des stocks

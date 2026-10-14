// Copyright (c) 2026 Zerma1 - https://github.com/Zerma1/Space-Engineers-scripte
// Licence : PolyForm Noncommercial 1.0.0 - https://polyformproject.org/licenses/noncommercial/1.0.0
// Usage et partage gratuits autorisés, vente interdite. / Free use and sharing allowed, selling prohibited.
// ==============================================================================
// SCRIPT : CONTRÔLE DES AVARIES & ALARMES  (version optimisée)
// ------------------------------------------------------------------------------
// Rôle :
//   Contrôle l'intégrité de tous les blocs terminaux de la construction (par
//   lots, sur plusieurs exécutions), publie un rapport trié sur les écrans
//   "Role=Avaries" et déclenche le groupe ALARMES_CRITIQUES quand un bloc est
//   hors service (H.S.) ou a disparu (DÉTRUIT).
//
// Nomenclature des blocs :
//   - Écrans  : Custom Data   [Ecran]
//                             Role=Avaries
//                             Surface=0      (optionnel : n° de surface d'un
//                                             cockpit / bloc programmable)
//   - Alarmes : groupe nommé exactement ALARMES_CRITIQUES (gyrophares,
//               lumières, blocs sonores, portes...).
//
// Commandes (argument du bloc programmable, majuscules/minuscules indifférentes) :
//   - ACK    : acquitte l'alarme. Elle se rallume dès qu'un NOUVEAU bloc devient
//              critique. Oublie aussi les blocs "DÉTRUIT" mémorisés.
//   - rescan : relit immédiatement blocs / écrans / alarmes / serveur et oublie
//              les blocs "DÉTRUIT" mémorisés (à faire après un démontage volontaire).
//
// Dépendances :
//   - SERVEUR_ALARMES (OPTIONNEL) : bus d'alarmes partagé avec le script
//     "Gestionnaire de Carburant" (source "Avaries"). Sans lui, le script
//     fonctionne seul.
// ==============================================================================

// ------------------------------------------------------------------------------
// CONSTANTES DE CONFIGURATION (toutes les valeurs réglables sont ici)
// ------------------------------------------------------------------------------

// true = ignore les vaisseaux amarrés par connecteur (on ne surveille que le nôtre).
const bool SEULEMENT_CETTE_CONSTRUCTION = true;

// Nom exact du groupe de blocs d'alarme (inchangé par rapport à l'original).
const string NOM_GROUPE_ALARMES = "ALARMES_CRITIQUES";

// Valeur de la clé Role des écrans de ce script ([Ecran] Role=Avaries).
const string ROLE_ECRAN = "Avaries";

// Nom de la source dans le bus d'alarmes SERVEUR_ALARMES.
const string SOURCE_ALARME = "Avaries";

// Couleur propre de l'alarme (utilisée sans serveur ou si [Couleurs] est absent).
readonly Color COULEUR_ALARME = new Color(255, 0, 0);

// Nombre maximal de blocs contrôlés par exécution (scan par lots).
const int BLOCS_PAR_EXECUTION = 250;

// Sécurité : on arrête le lot si le script a déjà consommé autant d'instructions
// (la limite du jeu est donnée par Runtime.MaxInstructionCount).
const int BUDGET_INSTRUCTIONS = 30000;

// Intervalle minimal entre deux relectures automatiques de la grille (secondes de jeu).
const double INTERVALLE_RESCAN_SECONDES = 10.0;

// Nombre maximal de lignes d'avaries affichées sur l'écran ("... et N autres" ensuite).
const int MAX_LIGNES_AFFICHEES = 15;

// ------------------------------------------------------------------------------
// ÉTAT DU SCRIPT (champs réutilisés d'une exécution à l'autre)
// ------------------------------------------------------------------------------

// Cache des blocs surveillés (rempli au rescan, parcouru par lots).
readonly List<IMyTerminalBlock> blocsSurveilles = new List<IMyTerminalBlock>();
// Cache des blocs du groupe d'alarme.
readonly List<IMyFunctionalBlock> blocsAlarme = new List<IMyFunctionalBlock>();
// Avaries trouvées pendant le passage en cours (publiées à la fin du passage).
readonly List<Avarie> avariesPassage = new List<Avarie>();
// Blocs disparus de la grille : EntityId -> dernier nom connu.
readonly Dictionary<long, string> blocsDetruits = new Dictionary<long, string>();
// EntityId des blocs critiques au dernier passage complet.
readonly HashSet<long> idsCritiquesCourants = new HashSet<long>();
// EntityId des blocs critiques déjà acquittés par ACK.
readonly HashSet<long> idsCritiquesAcquittes = new HashSet<long>();
// Constructeurs de texte réutilisés (pas de concaténation de string en boucle).
readonly StringBuilder constructeurRapport = new StringBuilder();
readonly StringBuilder constructeurEcho = new StringBuilder();

// Objets partagés (voir les classes en fin de script).
readonly BusAlarmes busAlarmes;
readonly EcransRole ecransAvaries;

int indexProchainBloc = 0;          // position du prochain bloc à contrôler dans le cache
double secondesDepuisRescan = 0;    // temps de jeu écoulé depuis le dernier rescan
bool premierRescanFait = false;     // le cache a-t-il déjà été rempli ?
bool rescanManuelDemande = false;   // commande "rescan" reçue
bool groupeAlarmeTrouve = false;    // le groupe ALARMES_CRITIQUES existe-t-il ?
bool besoinAlarme = false;          // ce script demande-t-il l'alarme ? (ACK déjà pris en compte)
int nombreCritiques = 0;            // résultat du dernier passage complet
int nombreAvertissements = 0;       // résultat du dernier passage complet
int nombrePassagesTermines = 0;     // compteur pour Echo (montre que le script tourne)
string rapportCourant = "=== ÉTAT DES SYSTÈMES ===\n\nAnalyse en cours...";
string messageCommande = "";        // retour de la dernière commande, pour Echo

/// <summary>
/// Constructeur : appelé une seule fois à la compilation / au chargement.
/// Update100 = une exécution tous les 100 ticks (~1,6 s) : réactif sans charger le serveur.
/// </summary>
public Program()
{
    busAlarmes = new BusAlarmes(SOURCE_ALARME, COULEUR_ALARME);
    ecransAvaries = new EcransRole(ROLE_ECRAN);
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

/// <summary>
/// Boucle principale. À chaque exécution :
/// 1. traiter la commande éventuelle (ACK / rescan) ;
/// 2. rescanner la grille si nécessaire (au début d'un passage seulement) ;
/// 3. contrôler un lot de blocs ; si le passage est complet, publier le rapport ;
/// 4. publier le besoin d'alarme sur le bus et appliquer l'état global au groupe ;
/// 5. afficher un résumé dans Echo.
/// </summary>
public void Main(string argument, UpdateType updateSource)
{
    TraiterCommande(argument);

    secondesDepuisRescan += Runtime.TimeSinceLastRun.TotalSeconds;

    if (rescanManuelDemande)
    {
        // Rescan manuel : on oublie aussi les blocs "DÉTRUIT" mémorisés.
        Rescanner(true);
        rescanManuelDemande = false;
    }
    else if (!premierRescanFait
             || (indexProchainBloc == 0 && secondesDepuisRescan >= INTERVALLE_RESCAN_SECONDES))
    {
        // Rescan automatique : seulement entre deux passages, pour ne pas
        // mélanger deux listes de blocs dans un même rapport.
        Rescanner(false);
    }

    TraiterLot();

    busAlarmes.Publier(besoinAlarme);
    busAlarmes.AppliquerAuxBlocs(blocsAlarme);

    AfficherEcho();
}

// ------------------------------------------------------------------------------
// COMMANDES
// ------------------------------------------------------------------------------

/// <summary>Interprète l'argument du bloc programmable (vide lors des exécutions automatiques).</summary>
void TraiterCommande(string argument)
{
    string commande = (argument ?? "").Trim().ToLower();
    if (commande.Length == 0) return;

    if (commande == "ack")
    {
        Acquitter();
        messageCommande = "ACK : alarme acquittée.";
    }
    else if (commande == "rescan")
    {
        rescanManuelDemande = true;
        messageCommande = "Rescan demandé.";
    }
    else
    {
        messageCommande = "Commande inconnue : '" + argument + "' (attendu : ACK ou rescan)";
    }
}

/// <summary>
/// Acquittement : les blocs critiques actuels sont marqués "connus", l'alarme
/// s'éteint, et les blocs "DÉTRUIT" mémorisés sont oubliés. L'alarme se
/// rallumera si un bloc qui n'est pas dans cette liste devient critique.
/// </summary>
void Acquitter()
{
    blocsDetruits.Clear();
    idsCritiquesAcquittes.Clear();
    foreach (long identifiant in idsCritiquesCourants) idsCritiquesAcquittes.Add(identifiant);
    besoinAlarme = false;
}

// ------------------------------------------------------------------------------
// RESCAN (cache des blocs, écrans, groupe d'alarme, serveur)
// ------------------------------------------------------------------------------

/// <summary>
/// Reconstruit tous les caches. Avant d'oublier l'ancienne liste, on vérifie
/// quels blocs ont disparu de la grille depuis leur dernier contrôle, pour ne
/// pas perdre un bloc détruit entre deux passages.
/// </summary>
/// <param name="oublierDetruits">true = rescan manuel : on vide la liste des blocs DÉTRUIT.</param>
void Rescanner(bool oublierDetruits)
{
    if (oublierDetruits)
    {
        blocsDetruits.Clear();
    }
    else
    {
        foreach (IMyTerminalBlock ancienBloc in blocsSurveilles)
        {
            if (GridTerminalSystem.GetBlockWithId(ancienBloc.EntityId) == null) MemoriserDetruit(ancienBloc);
        }
    }

    // 1. Blocs surveillés (tous les blocs terminaux de notre construction).
    blocsSurveilles.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocsSurveilles, EstDeCetteConstruction);

    // 2. Écrans Role=Avaries.
    ecransAvaries.Rechercher(GridTerminalSystem, Me, SEULEMENT_CETTE_CONSTRUCTION);

    // 3. Groupe d'alarme.
    blocsAlarme.Clear();
    IMyBlockGroup groupe = GridTerminalSystem.GetBlockGroupWithName(NOM_GROUPE_ALARMES);
    groupeAlarmeTrouve = groupe != null;
    if (groupe != null) groupe.GetBlocksOfType<IMyFunctionalBlock>(blocsAlarme, EstDeCetteConstruction);

    // 4. Serveur d'alarmes (optionnel).
    busAlarmes.RechercherServeur(GridTerminalSystem, Me, SEULEMENT_CETTE_CONSTRUCTION);

    // On repart d'un passage propre.
    avariesPassage.Clear();
    indexProchainBloc = 0;
    secondesDepuisRescan = 0;
    premierRescanFait = true;
}

/// <summary>Filtre de construction : écarte les vaisseaux amarrés par connecteur.</summary>
bool EstDeCetteConstruction(IMyTerminalBlock bloc)
{
    return !SEULEMENT_CETTE_CONSTRUCTION || bloc.IsSameConstructAs(Me);
}

/// <summary>Mémorise un bloc disparu (clé = EntityId, valeur = dernier nom connu).</summary>
void MemoriserDetruit(IMyTerminalBlock bloc)
{
    blocsDetruits[bloc.EntityId] = bloc.CustomName;
}

// ------------------------------------------------------------------------------
// SCAN PAR LOTS
// ------------------------------------------------------------------------------

/// <summary>
/// Contrôle au plus BLOCS_PAR_EXECUTION blocs (et s'arrête avant si le budget
/// d'instructions est atteint). Quand tout le cache a été parcouru, le passage
/// est terminé : on publie le rapport et on recommence au début.
/// </summary>
void TraiterLot()
{
    int nombreTraites = 0;
    while (indexProchainBloc < blocsSurveilles.Count
           && nombreTraites < BLOCS_PAR_EXECUTION
           && Runtime.CurrentInstructionCount < BUDGET_INSTRUCTIONS)
    {
        IMyTerminalBlock bloc = blocsSurveilles[indexProchainBloc];
        nombreTraites++;

        // Bloc pulvérisé : il n'existe plus dans la grille. On le mémorise et on
        // le retire du cache (l'index pointe alors déjà sur le bloc suivant).
        if (GridTerminalSystem.GetBlockWithId(bloc.EntityId) == null)
        {
            MemoriserDetruit(bloc);
            blocsSurveilles.RemoveAt(indexProchainBloc);
            continue;
        }

        indexProchainBloc++;
        ControlerStructure(bloc);
    }

    if (indexProchainBloc >= blocsSurveilles.Count) TerminerPassage();
}

/// <summary>
/// Lit la structure physique d'un bloc et l'ajoute aux avaries s'il est endommagé.
/// IMyTerminalBlock ne donne que la partie "logicielle" (nom, on/off) ;
/// IMySlimBlock donne la partie physique (composants, points de vie).
/// </summary>
void ControlerStructure(IMyTerminalBlock bloc)
{
    IMySlimBlock structure = bloc.CubeGrid.GetCubeBlock(bloc.Position);

    // CurrentDamage > 0 : le bloc a perdu des points de vie (même critère que l'original).
    if (structure == null || structure.CurrentDamage <= 0f) return;

    Avarie avarie = new Avarie();
    avarie.Identifiant = bloc.EntityId;
    avarie.Nom = bloc.CustomName;
    avarie.Pourcentage = CalculerIntegrite(structure);
    // IsFunctional = false : le bloc est passé sous sa "ligne rouge", il ne fonctionne plus.
    avarie.Critique = !bloc.IsFunctional;
    avarie.Detruit = false;
    avariesPassage.Add(avarie);
}

/// <summary>
/// Intégrité restante en pourcentage du maximum.
///   BuildIntegrity : points de vie apportés par la soudure (= MaxIntegrity si
///                    le bloc est entièrement construit) ;
///   CurrentDamage  : dégâts accumulés depuis ;
///   MaxIntegrity   : points de vie d'un bloc complet et intact.
/// Intégrité = (BuildIntegrity - CurrentDamage) / MaxIntegrity × 100.
/// La division est protégée (MaxIntegrity &lt;= 0 renvoie 0 %).
/// </summary>
static double CalculerIntegrite(IMySlimBlock structure)
{
    if (structure.MaxIntegrity <= 0f) return 0.0;

    double santeActuelle = structure.BuildIntegrity - structure.CurrentDamage;
    if (santeActuelle < 0.0) santeActuelle = 0.0;

    return santeActuelle / structure.MaxIntegrity * 100.0;
}

/// <summary>
/// Fin d'un passage complet : ajoute les blocs DÉTRUIT, trie, calcule le
/// besoin d'alarme (en tenant compte de l'ACK), construit le rapport et
/// l'envoie aux écrans, puis prépare le passage suivant.
/// </summary>
void TerminerPassage()
{
    // 1. Les blocs disparus sont des avaries critiques.
    foreach (KeyValuePair<long, string> paire in blocsDetruits)
    {
        Avarie avarie = new Avarie();
        avarie.Identifiant = paire.Key;
        avarie.Nom = paire.Value;
        avarie.Pourcentage = 0.0;
        avarie.Critique = true;
        avarie.Detruit = true;
        avariesPassage.Add(avarie);
    }

    // 2. Tri : DÉTRUIT, puis H.S., puis les plus endommagés d'abord.
    avariesPassage.Sort(ComparerAvaries);

    // 3. Comptage et liste des blocs critiques.
    idsCritiquesCourants.Clear();
    nombreCritiques = 0;
    nombreAvertissements = 0;
    foreach (Avarie avarie in avariesPassage)
    {
        if (avarie.Critique)
        {
            nombreCritiques++;
            idsCritiquesCourants.Add(avarie.Identifiant);
        }
        else
        {
            nombreAvertissements++;
        }
    }

    // 4. Acquittement : on oublie les blocs acquittés qui ne sont plus critiques
    //    (réparés). S'ils redeviennent critiques plus tard, l'alarme repartira.
    idsCritiquesAcquittes.IntersectWith(idsCritiquesCourants);
    besoinAlarme = false;
    foreach (long identifiant in idsCritiquesCourants)
    {
        if (!idsCritiquesAcquittes.Contains(identifiant))
        {
            besoinAlarme = true; // au moins un bloc critique NON acquitté
            break;
        }
    }

    // 5. Rapport et écrans (écriture seulement si le texte a changé).
    ConstruireRapport();
    ecransAvaries.Ecrire(rapportCourant);

    // 6. Passage suivant.
    avariesPassage.Clear();
    indexProchainBloc = 0;
    nombrePassagesTermines++;
}

/// <summary>Ordre de tri : rang de gravité, puis pourcentage d'intégrité croissant.</summary>
static int ComparerAvaries(Avarie a, Avarie b)
{
    int rangA = RangGravite(a);
    int rangB = RangGravite(b);
    if (rangA != rangB) return rangA.CompareTo(rangB);
    return a.Pourcentage.CompareTo(b.Pourcentage);
}

/// <summary>0 = détruit, 1 = hors service, 2 = simplement endommagé.</summary>
static int RangGravite(Avarie avarie)
{
    if (avarie.Detruit) return 0;
    if (avarie.Critique) return 1;
    return 2;
}

// ------------------------------------------------------------------------------
// AFFICHAGE
// ------------------------------------------------------------------------------

/// <summary>
/// Construit le texte de l'écran à partir de la liste triée.
/// Le nombre de lignes est limité à MAX_LIGNES_AFFICHEES.
/// </summary>
void ConstruireRapport()
{
    StringBuilder texte = constructeurRapport;
    texte.Clear();
    texte.Append("=== ÉTAT DES SYSTÈMES ===\n\n");

    if (avariesPassage.Count == 0)
    {
        texte.Append("   [ SYSTÈMES NOMINAUX ]\n   Intégrité à 100%\n   Aucune avarie détectée.");
        rapportCourant = texte.ToString();
        return;
    }

    if (nombreCritiques > 0 && !besoinAlarme)
    {
        texte.Append("(Alarme acquittée - ACK)\n\n");
    }

    int lignesEcrites = 0;
    bool enteteCritiquesEcrit = false;
    bool enteteAvertissementsEcrit = false;

    foreach (Avarie avarie in avariesPassage)
    {
        if (lignesEcrites >= MAX_LIGNES_AFFICHEES) break;

        if (avarie.Critique && !enteteCritiquesEcrit)
        {
            texte.Append("!!! PANNES MAJEURES (").Append(nombreCritiques).Append(") !!!\n");
            enteteCritiquesEcrit = true;
        }
        if (!avarie.Critique && !enteteAvertissementsEcrit)
        {
            if (enteteCritiquesEcrit) texte.Append('\n');
            texte.Append("--- RÉPARATIONS REQUISES (").Append(nombreAvertissements).Append(") ---\n");
            enteteAvertissementsEcrit = true;
        }

        if (avarie.Detruit)
        {
            texte.Append("[DÉTRUIT] ").Append(avarie.Nom).Append('\n');
        }
        else if (avarie.Critique)
        {
            texte.Append("[CRITIQUE] ").Append(avarie.Nom).Append(" H.S. (")
                 .Append(avarie.Pourcentage.ToString("0")).Append("%)\n");
        }
        else
        {
            texte.Append("[DÉGÂTS] ").Append(avarie.Nom).Append(" : ")
                 .Append(avarie.Pourcentage.ToString("0")).Append("%\n");
        }
        lignesEcrites++;
    }

    int lignesRestantes = avariesPassage.Count - lignesEcrites;
    if (lignesRestantes > 0)
    {
        texte.Append("... et ").Append(lignesRestantes).Append(" autres\n");
    }

    rapportCourant = texte.ToString();
}

/// <summary>Résumé court dans la fenêtre du bloc programmable (diagnostic).</summary>
void AfficherEcho()
{
    StringBuilder texte = constructeurEcho;
    texte.Clear();
    texte.Append("=== CONTRÔLE DES AVARIES ===\n");
    texte.Append("Blocs surveillés : ").Append(blocsSurveilles.Count)
         .Append(" (lot : ").Append(indexProchainBloc).Append(")\n");
    texte.Append("Passages terminés : ").Append(nombrePassagesTermines).Append('\n');
    texte.Append("Critiques : ").Append(nombreCritiques)
         .Append(" | Dégâts : ").Append(nombreAvertissements).Append('\n');
    texte.Append("Alarme (Avaries) : ")
         .Append(besoinAlarme ? "ACTIVE" : (nombreCritiques > 0 ? "acquittée" : "inactive")).Append('\n');
    texte.Append("Écrans Role=").Append(ROLE_ECRAN).Append(" : ").Append(ecransAvaries.Nombre);
    if (ecransAvaries.ErreursConfiguration > 0)
    {
        texte.Append(" (").Append(ecransAvaries.ErreursConfiguration).Append(" Surface=N invalide)");
    }
    texte.Append('\n');
    if (groupeAlarmeTrouve)
    {
        texte.Append("Groupe ").Append(NOM_GROUPE_ALARMES).Append(" : ").Append(blocsAlarme.Count).Append(" blocs\n");
    }
    else
    {
        texte.Append("ATTENTION : groupe ").Append(NOM_GROUPE_ALARMES).Append(" introuvable\n");
    }
    texte.Append("Bus : ").Append(busAlarmes.Diagnostic).Append('\n');
    texte.Append("Dernière exécution : ").Append(Runtime.LastRunTimeMs.ToString("0.000")).Append(" ms\n");
    if (messageCommande.Length > 0) texte.Append(messageCommande).Append('\n');
    Echo(texte.ToString());
}

// ------------------------------------------------------------------------------
// STRUCTURE DE DONNÉES
// ------------------------------------------------------------------------------

/// <summary>Une ligne du rapport : un bloc endommagé, hors service ou détruit.</summary>
public class Avarie
{
    public long Identifiant;    // EntityId du bloc (sert à l'acquittement)
    public string Nom;          // nom affiché (CustomName)
    public double Pourcentage;  // intégrité restante en %
    public bool Critique;       // true = hors service ou détruit
    public bool Detruit;        // true = le bloc a disparu de la grille
}

// ==============================================================================
// CLASSE PARTAGÉE : BUS D'ALARMES  (COPIE CONFORME DANS CHAQUE SCRIPT QUI ALARME)
// ------------------------------------------------------------------------------
// Ne PAS modifier cette classe dans un seul script : elle doit rester strictement
// identique dans "Contrôle des Avaries" et "Gestionnaire de Carburant", sinon les
// deux scripts ne calculeraient plus le même état global et se battraient de
// nouveau pour les gyrophares.
// ==============================================================================

/// <summary>
/// Bus d'alarmes partagé entre plusieurs blocs programmables.
/// QUOI : chaque script publie son propre besoin d'alarme (Source=true/false)
/// dans la section [Etat] du Custom Data d'un bloc nommé SERVEUR_ALARMES, puis
/// relit toutes les sources pour calculer UN état global (au moins une source
/// active) et UNE couleur (première source active dans [Config] Priorite).
/// POURQUOI : sans ce bus, le script d'avaries éteignait les gyrophares que le
/// script de carburant venait d'allumer (et inversement). Avec le bus, tous les
/// scripts calculent exactement le même état : plus de conflit.
/// Si le bloc SERVEUR_ALARMES n'existe pas, la classe fonctionne en mode
/// autonome : état global = besoin propre du script, couleur = couleur propre.
/// </summary>
public class BusAlarmes
{
    /// <summary>Nom exact du bloc dont le Custom Data sert de bus.</summary>
    public const string NOM_SERVEUR = "SERVEUR_ALARMES";

    // Noms des sections et des clés du Custom Data partagé.
    const string SECTION_CONFIG = "Config";
    const string SECTION_COULEURS = "Couleurs";
    const string SECTION_ETAT = "Etat";
    const string CLE_PRIORITE = "Priorite";

    // Identité du script qui utilise ce bus (ex. "Avaries", "Carburant").
    readonly string source;
    // Couleur utilisée en mode autonome ou si la couleur du serveur est illisible.
    readonly Color couleurPropre;

    // Objets réutilisés d'une exécution à l'autre (évite les allocations).
    readonly MyIni lecteurIni = new MyIni();
    readonly List<MyIniKey> clesEtat = new List<MyIniKey>();
    readonly List<IMyTerminalBlock> tamponRecherche = new List<IMyTerminalBlock>();
    // Blocs d'alarme déjà synchronisés au moins une fois (par EntityId) :
    // permet de traiter un bloc ajouté au groupe sans relancer les sons des autres.
    readonly HashSet<long> blocsDejaSynchronises = new HashSet<long>();

    // Bloc serveur trouvé lors du dernier scan (null = mode autonome).
    IMyTerminalBlock serveur;
    // Dernier Custom Data lu : on ne reparse que si le texte a changé.
    string dernierCustomDataLu;
    // Résultat du dernier parsing (false = Custom Data illisible).
    bool customDataValide;
    // Dernier état global effectivement appliqué aux blocs d'alarme.
    bool etatApplique;

    /// <summary>Vrai si un bloc SERVEUR_ALARMES a été trouvé au dernier scan.</summary>
    public bool ServeurPresent { get { return serveur != null; } }

    /// <summary>État global : vrai si au moins une source demande l'alarme.</summary>
    public bool AlarmeGlobale { get; private set; }

    /// <summary>Couleur à appliquer aux lumières quand l'alarme globale est active.</summary>
    public Color CouleurGlobale { get; private set; }

    /// <summary>Nom de la source qui impose sa couleur (vide si aucune alarme).</summary>
    public string SourceDominante { get; private set; }

    /// <summary>Message court destiné à Echo (état du bus, erreurs de configuration).</summary>
    public string Diagnostic { get; private set; }

    /// <summary>
    /// Crée le bus pour une source donnée.
    /// </summary>
    /// <param name="source">Nom de la source (clé utilisée dans [Etat] et [Couleurs]).</param>
    /// <param name="couleurPropre">Couleur par défaut de cette source.</param>
    public BusAlarmes(string source, Color couleurPropre)
    {
        this.source = source;
        this.couleurPropre = couleurPropre;
        CouleurGlobale = couleurPropre;
        SourceDominante = "";
        Diagnostic = "pas encore initialisé";
    }

    /// <summary>
    /// Cherche le bloc SERVEUR_ALARMES (à appeler lors du rescan périodique,
    /// pas à chaque exécution). Le filtre de construction évite de lire le
    /// serveur d'un vaisseau amarré par connecteur.
    /// </summary>
    public void RechercherServeur(IMyGridTerminalSystem grille, IMyTerminalBlock moi, bool seulementCetteConstruction)
    {
        // Recherche rapide par nom (renvoie le premier bloc portant ce nom).
        IMyTerminalBlock trouve = grille.GetBlockWithName(NOM_SERVEUR);

        // Si ce premier bloc appartient à un vaisseau amarré, on cherche
        // explicitement celui de notre construction (cas rare, scan complet).
        if (trouve != null && seulementCetteConstruction && !trouve.IsSameConstructAs(moi))
        {
            tamponRecherche.Clear();
            grille.GetBlocksOfType<IMyTerminalBlock>(tamponRecherche,
                b => b.CustomName == NOM_SERVEUR && b.IsSameConstructAs(moi));
            trouve = tamponRecherche.Count > 0 ? tamponRecherche[0] : null;
            tamponRecherche.Clear();
        }

        // Si le serveur a changé (apparu, disparu, remplacé), on force une relecture.
        if (trouve != serveur)
        {
            serveur = trouve;
            dernierCustomDataLu = null;
        }
    }

    /// <summary>
    /// Publie le besoin d'alarme de CE script puis calcule l'état global.
    /// Étapes : (1) relire le Custom Data s'il a changé, (2) écrire
    /// [Etat] Source=besoin seulement si la valeur change, (3) état global =
    /// au moins une clé [Etat] vraie, (4) couleur = première source active de
    /// [Config] Priorite, sinon première source active trouvée dans [Etat].
    /// </summary>
    /// <param name="besoinPropre">Vrai si ce script demande l'alarme (déjà corrigé de l'ACK).</param>
    public void Publier(bool besoinPropre)
    {
        // ---- Mode autonome : pas de serveur -----------------------------------
        if (serveur == null)
        {
            PasserEnModeAutonome(besoinPropre, "autonome (aucun bloc " + NOM_SERVEUR + ")");
            return;
        }

        // ---- (1) Relecture seulement si le texte a changé ---------------------
        string texte = serveur.CustomData;
        if (texte != dernierCustomDataLu)
        {
            dernierCustomDataLu = texte;
            customDataValide = lecteurIni.TryParse(texte);
        }
        if (!customDataValide)
        {
            PasserEnModeAutonome(besoinPropre, "Custom Data de " + NOM_SERVEUR + " illisible (format INI)");
            return;
        }

        // ---- (2) Publication de notre état (écriture seulement si changement) -
        bool modifie = false;
        if (!lecteurIni.ContainsKey(SECTION_ETAT, source)
            || lecteurIni.Get(SECTION_ETAT, source).ToBoolean(false) != besoinPropre)
        {
            lecteurIni.Set(SECTION_ETAT, source, besoinPropre);
            modifie = true;
        }
        // Auto-enregistrement de notre couleur si le joueur ne l'a pas définie :
        // ainsi les autres scripts connaissent notre couleur.
        if (!lecteurIni.ContainsKey(SECTION_COULEURS, source))
        {
            lecteurIni.Set(SECTION_COULEURS, source, couleurPropre.R + "," + couleurPropre.G + "," + couleurPropre.B);
            modifie = true;
        }
        if (modifie)
        {
            string nouveauTexte = lecteurIni.ToString();
            serveur.CustomData = nouveauTexte;
            // On mémorise notre propre écriture pour ne pas la reparser au prochain cycle.
            dernierCustomDataLu = nouveauTexte;
        }

        // ---- (3) État global : au moins une source active ---------------------
        AlarmeGlobale = false;
        SourceDominante = "";
        CouleurGlobale = couleurPropre;
        clesEtat.Clear();
        lecteurIni.GetKeys(SECTION_ETAT, clesEtat);
        string premiereSourceActive = "";
        foreach (MyIniKey cle in clesEtat)
        {
            if (lecteurIni.Get(SECTION_ETAT, cle.Name).ToBoolean(false))
            {
                AlarmeGlobale = true;
                premiereSourceActive = cle.Name;
                break;
            }
        }

        // ---- (4) Couleur : première source active selon la priorité -----------
        if (AlarmeGlobale)
        {
            SourceDominante = premiereSourceActive;
            string priorite = lecteurIni.Get(SECTION_CONFIG, CLE_PRIORITE).ToString("");
            string[] sourcesOrdonnees = priorite.Split(',');
            foreach (string nomBrut in sourcesOrdonnees)
            {
                string nom = nomBrut.Trim();
                if (nom.Length > 0 && lecteurIni.Get(SECTION_ETAT, nom).ToBoolean(false))
                {
                    SourceDominante = nom;
                    break;
                }
            }
            CouleurGlobale = LireCouleur(SourceDominante);
        }

        Diagnostic = "connecté, alarme globale " + (AlarmeGlobale ? "ACTIVE (" + SourceDominante + ")" : "inactive");
    }

    /// <summary>
    /// Applique l'état global au groupe de blocs d'alarme de ce script.
    /// Chaque écriture (Enabled, Color, Play/Stop) n'est faite que si elle
    /// change quelque chose : un bloc sonore n'est relancé qu'au changement
    /// d'état global, ou la première fois qu'on le rencontre.
    /// </summary>
    /// <param name="blocsAlarme">Blocs du groupe d'alarme (lumières, sons, portes...).</param>
    public void AppliquerAuxBlocs(List<IMyFunctionalBlock> blocsAlarme)
    {
        bool changementEtat = AlarmeGlobale != etatApplique;

        foreach (IMyFunctionalBlock bloc in blocsAlarme)
        {
            // HashSet.Add renvoie true si l'identifiant n'y était pas encore.
            bool nouveauBloc = blocsDejaSynchronises.Add(bloc.EntityId);

            // Allumage / extinction (le bloc doit être allumé AVANT Play()).
            if (bloc.Enabled != AlarmeGlobale) bloc.Enabled = AlarmeGlobale;

            // Couleur des lumières (gyrophares...) : utile seulement si allumées.
            IMyLightingBlock lumiere = bloc as IMyLightingBlock;
            if (lumiere != null && AlarmeGlobale && lumiere.Color != CouleurGlobale)
            {
                lumiere.Color = CouleurGlobale;
            }

            // Blocs sonores : allumer ne suffit pas, il faut lancer la lecture.
            IMySoundBlock son = bloc as IMySoundBlock;
            if (son != null && (changementEtat || nouveauBloc))
            {
                if (AlarmeGlobale) son.Play();
                else son.Stop();
            }
        }

        etatApplique = AlarmeGlobale;
    }

    /// <summary>Mode sans serveur : l'état global est simplement notre besoin propre.</summary>
    void PasserEnModeAutonome(bool besoinPropre, string message)
    {
        AlarmeGlobale = besoinPropre;
        CouleurGlobale = couleurPropre;
        SourceDominante = besoinPropre ? source : "";
        Diagnostic = message;
    }

    /// <summary>
    /// Lit la couleur "R,G,B" d'une source dans [Couleurs].
    /// Renvoie la couleur propre du script si la clé est absente ou mal écrite.
    /// </summary>
    Color LireCouleur(string nomSource)
    {
        string texte = lecteurIni.Get(SECTION_COULEURS, nomSource).ToString("");
        string[] morceaux = texte.Split(',');
        if (morceaux.Length != 3) return couleurPropre;

        int rouge, vert, bleu;
        if (!int.TryParse(morceaux[0].Trim(), out rouge)) return couleurPropre;
        if (!int.TryParse(morceaux[1].Trim(), out vert)) return couleurPropre;
        if (!int.TryParse(morceaux[2].Trim(), out bleu)) return couleurPropre;

        return new Color(Borner(rouge), Borner(vert), Borner(bleu));
    }

    /// <summary>Ramène une composante de couleur dans l'intervalle 0..255.</summary>
    static int Borner(int valeur)
    {
        if (valeur < 0) return 0;
        if (valeur > 255) return 255;
        return valeur;
    }
}

// ==============================================================================
// CLASSE UTILITAIRE : ÉCRANS CONFIGURÉS PAR CUSTOM DATA ([Ecran] Role=...)
// (même code dans "Contrôle des Avaries" et "Gestionnaire de Carburant")
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

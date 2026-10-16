// Copyright (c) 2026 Zerma1 - https://github.com/Zerma1/Space-Engineers-scripte
// Licence : PolyForm Noncommercial 1.0.0 - https://polyformproject.org/licenses/noncommercial/1.0.0
// Usage et partage gratuits autorisés, vente interdite. / Free use and sharing allowed, selling prohibited.
// ==============================================================================
// SCRIPT : CENTRALE D'ALARMES  (version optimisée)
// ------------------------------------------------------------------------------
// Rôle :
//   SEUL pilote des gyrophares. Lit le bus SERVEUR_ALARMES (où chaque script
//   source publie [Etat] Source=true/false et sa criticité) et donne au groupe
//   ALARMES la couleur du niveau le plus grave parmi les alarmes actives
//   (Critique, Majeur, Normal, Mineur). Affiche la liste des alarmes actives,
//   triées par criticité puis par ordre d'arrivée, et leur nombre par niveau
//   sur des écrans repérés par leur nom.
//   Les scripts sources (Avaries, Carburant...) n'ont plus besoin de groupe.
//
// Nomenclature des blocs :
//   - Bus     : bloc nommé exactement SERVEUR_ALARMES (voir "Bus d'Alarmes").
//   - Alarmes : groupe nommé exactement ALARMES (gyrophares, lumières, blocs
//               sonores...). Filtre optionnel dans le Custom Data d'un bloc :
//                   [Alarme]
//                   Sources=Carburant,Avaries
//               (le bloc ne réagit alors qu'à ces sources).
//   - Écrans  : nom contenant [alarme-line]  -> liste des alarmes actives
//               nom contenant [alarme-quant] -> nombre d'alarmes par criticité,
//                   en cases colorées qui s'adaptent à la forme de l'écran
//                   (4 en ligne sur un corner LCD, 2x2 sur un écran carré).
//               [alarme-line:1] / [alarme-quant:1] = surface n° 1 d'un cockpit.
//
// Commandes (argument du bloc programmable, majuscules/minuscules indifférentes
// pour la commande) :
//   - on <Source> [niveau] : active l'alarme <Source> (bouton, capteur,
//                    minuterie...). Niveau facultatif : critique, majeur,
//                    normal ou mineur (écrit dans [Criticite]).
//   - off <Source> : désactive l'alarme <Source>.
//   - rescan       : relit immédiatement serveur, groupe et écrans.
//
// Dépendances :
//   - SERVEUR_ALARMES (OBLIGATOIRE) : sans lui, aucune alarme n'est connue.
// ==============================================================================

// ------------------------------------------------------------------------------
// CONSTANTES DE CONFIGURATION (toutes les valeurs réglables sont ici)
// ------------------------------------------------------------------------------

// true = ignore les vaisseaux amarrés par connecteur (serveur, groupe et écrans).
const bool SEULEMENT_CETTE_CONSTRUCTION = true;

// Nom exact du bloc dont le Custom Data sert de bus (identique à BusAlarmes).
const string NOM_SERVEUR = "SERVEUR_ALARMES";

// Nom exact du groupe de blocs d'alarme piloté par la Centrale.
const string NOM_GROUPE_ALARMES = "ALARMES";

// Balises cherchées dans le NOM des écrans (sans le "]" final, qui peut être
// précédé de ":N" pour choisir la surface d'un cockpit).
const string BALISE_LISTE = "[alarme-line";
const string BALISE_QUANTITE = "[alarme-quant";

// Intervalle entre deux relectures automatiques de la grille (secondes de jeu).
const double INTERVALLE_RESCAN_SECONDES = 10.0;

// Nombre maximal d'alarmes listées sur un écran [alarme-line] ("... et N autres" ensuite).
const int MAX_LIGNES_LISTE = 15;

// true = le texte des écrans [alarme-line] prend la couleur du niveau le plus grave.
const bool COLORER_TEXTE_ECRANS = true;

// Couleur du texte des écrans quand aucune alarme n'est active.
readonly Color COULEUR_TEXTE_REPOS = new Color(255, 255, 255);

// Couleur de chaque niveau (gyrophares et cases [alarme-quant]) si la section
// [Couleurs] du bus ne la redéfinit pas (Critique=R,G,B, Majeur=..., etc.).
// Mêmes valeurs que dans la classe BusAlarmes des scripts sources.
readonly Color[] COULEURS_CRITICITE =
{
    new Color(220, 20, 60),    // Critique : rouge cramoisi (crimson)
    new Color(255, 0, 0),      // Majeur   : rouge
    new Color(255, 140, 0),    // Normal   : orange
    new Color(0, 255, 255)     // Mineur   : cyan
};
// Couleur du chiffre sur une case allumée (lisible sur le fond de la case).
readonly Color[] COULEURS_TEXTE_CASE =
{
    new Color(255, 255, 255), new Color(255, 255, 255), new Color(0, 0, 0), new Color(0, 0, 0)
};
// Luminosité d'une case à 0 alarme (0 = noire, 1 = aussi vive qu'une case active).
const float ATTENUATION_CASE_VIDE = 0.18f;
// Couleur du texte d'une case à 0 alarme.
readonly Color COULEUR_TEXTE_CASE_VIDE = new Color(120, 120, 120);
// Police des sprites ("White" = police standard du jeu).
const string POLICE_SPRITES = "White";

// ------------------------------------------------------------------------------
// NOMS DU BUS (identiques à ceux de la classe BusAlarmes des scripts sources)
// ------------------------------------------------------------------------------

const string SECTION_COULEURS = "Couleurs";
const string SECTION_ETAT = "Etat";
const string SECTION_CRITICITE = "Criticite";
const string SECTION_DETAIL = "Detail";

// Filtre optionnel dans le Custom Data d'un bloc du groupe ALARMES.
const string SECTION_FILTRE = "Alarme";
const string CLE_FILTRE_SOURCES = "Sources";

// Niveaux de criticité, du plus grave au moins grave (l'index sert de rang).
// Plusieurs mots acceptés par niveau (les anciens noms restent valides).
readonly string[][] NIVEAUX_CRITICITE =
{
    new[] { "critique" },
    new[] { "majeur", "majeure", "importante" },
    new[] { "normal", "normale" },
    new[] { "mineur", "mineure", "basse" }
};
readonly string[] LIBELLES_CRITICITE = { "Critique", "Majeur", "Normal", "Mineur" };
const int RANG_CRITICITE_DEFAUT = 2;   // "normal"

// ------------------------------------------------------------------------------
// ÉTAT DU SCRIPT (champs réutilisés d'une exécution à l'autre)
// ------------------------------------------------------------------------------

readonly MyIni lecteurIni = new MyIni();
readonly MyIni lecteurFiltre = new MyIni();
readonly List<MyIniKey> clesEtat = new List<MyIniKey>();
readonly List<IMyTerminalBlock> tamponRecherche = new List<IMyTerminalBlock>();
readonly List<IMyFunctionalBlock> tamponGroupe = new List<IMyFunctionalBlock>();
readonly StringBuilder constructeurTexte = new StringBuilder();
// Texte à mesurer (MeasureStringInPixels attend un StringBuilder).
readonly StringBuilder texteMesure = new StringBuilder();

// Alarmes actives triées de la plus grave à la moins grave.
readonly List<SourceAlarme> sourcesActives = new List<SourceAlarme>();
// Réserve d'objets SourceAlarme réutilisés (pas d'allocation à chaque relecture).
readonly List<SourceAlarme> reserveSources = new List<SourceAlarme>();
// Comparateur mis en cache (un "method group" passé à Sort allouerait à chaque appel).
readonly Comparison<SourceAlarme> comparateurSources;
// Nombre d'alarmes actives par rang de criticité.
readonly int[] nombreParRang = new int[4];
// Couleur de chaque niveau, relue dans [Couleurs] à chaque changement du bus.
readonly Color[] couleursNiveaux = new Color[4];

// Ordre d'arrivée : numéro donné à une source quand on la voit passer à true
// (plus petit = arrivée plus tôt). Sauvegardé dans Storage pour survivre au
// rechargement de la partie. Une source redevenue false perd son numéro.
readonly Dictionary<string, int> arrivees = new Dictionary<string, int>();
readonly List<string> tamponNoms = new List<string>();
int compteurArrivees = 0;

// Blocs du groupe ALARMES et écrans, conservés d'un rescan à l'autre (par
// EntityId / surface) pour garder leur dernier état connu.
List<BlocAlarme> blocsAlarme = new List<BlocAlarme>();
List<BlocAlarme> tamponBlocs = new List<BlocAlarme>();
List<EcranAlarme> ecrans = new List<EcranAlarme>();
List<EcranAlarme> tamponEcrans = new List<EcranAlarme>();

IMyTerminalBlock serveur;           // bloc SERVEUR_ALARMES (null = absent)
string dernierCustomDataLu = "";    // on ne reparse le bus que si le texte a changé
bool relectureForcee = true;        // serveur apparu / disparu / remplacé
bool busValide = false;             // Custom Data du serveur lisible ?
string diagnosticBus = "pas encore lu";
int nombreSources = 0;              // nombre de clés [Etat] (actives ou non)
bool etatAChange = true;            // il faut recalculer textes, blocs et écrans
bool groupeTrouve = false;
int nombreEcransListe = 0;
int nombreEcransQuantite = 0;
int balisesInvalides = 0;           // balise mal écrite ou surface inexistante
double secondesDepuisRescan = 0;
bool premierRescanFait = false;
string texteListe = "";
string texteQuantite = "";
Color couleurTexte;
string messageCommande = "";

/// <summary>
/// Constructeur : appelé une seule fois à la compilation / au chargement.
/// Update10 (~6 exécutions par seconde) : une alarme publiée ou une commande
/// on/off est visible presque immédiatement. Le coût reste faible : à chaque
/// exécution, on ne fait que comparer le Custom Data du serveur au dernier lu.
/// </summary>
public Program()
{
    comparateurSources = ComparerSources;
    couleurTexte = COULEUR_TEXTE_REPOS;
    Array.Copy(COULEURS_CRITICITE, couleursNiveaux, couleursNiveaux.Length);
    RestaurerArrivees();
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
}

/// <summary>
/// Sauvegarde l'ordre d'arrivée des alarmes actives (appelé par le jeu à
/// l'enregistrement). Format : compteur, puis une ligne "numéro TAB source".
/// </summary>
public void Save()
{
    StringBuilder texte = constructeurTexte;
    texte.Clear();
    texte.Append(compteurArrivees);
    foreach (KeyValuePair<string, int> paire in arrivees)
    {
        texte.Append('\n').Append(paire.Value).Append('\t').Append(paire.Key);
    }
    Storage = texte.ToString();
}

/// <summary>Relit l'ordre d'arrivée sauvegardé par Save() (Storage vide : rien à faire).</summary>
void RestaurerArrivees()
{
    string[] lignes = (Storage ?? "").Split('\n');
    if (!int.TryParse(lignes[0], out compteurArrivees))
    {
        compteurArrivees = 0;
        return;
    }
    for (int i = 1; i < lignes.Length; i++)
    {
        int tabulation = lignes[i].IndexOf('\t');
        int numero;
        if (tabulation > 0 && int.TryParse(lignes[i].Substring(0, tabulation), out numero))
        {
            arrivees[lignes[i].Substring(tabulation + 1)] = numero;
        }
    }
}

/// <summary>
/// Boucle principale. À chaque exécution :
/// 1. rescanner la grille si nécessaire (périodique) ;
/// 2. traiter la commande éventuelle (on / off / rescan) ;
/// 3. relire le bus s'il a changé ;
/// 4. si quelque chose a changé : textes, blocs d'alarme et écrans ;
/// 5. afficher un résumé dans Echo.
/// </summary>
public void Main(string argument, UpdateType updateSource)
{
    secondesDepuisRescan += Runtime.TimeSinceLastRun.TotalSeconds;
    if (!premierRescanFait || secondesDepuisRescan >= INTERVALLE_RESCAN_SECONDES) Rescanner();

    TraiterCommande(argument);

    LireBus();

    if (etatAChange)
    {
        ConstruireTextes();
        AppliquerAuxBlocs();
        EcrireEcrans();
        etatAChange = false;
    }

    AfficherEcho();
}

// ------------------------------------------------------------------------------
// COMMANDES
// ------------------------------------------------------------------------------

/// <summary>Interprète l'argument du bloc programmable (vide lors des exécutions automatiques).</summary>
void TraiterCommande(string argument)
{
    string texte = (argument ?? "").Trim();
    if (texte.Length == 0) return;

    int espace = texte.IndexOf(' ');
    string commande = (espace < 0 ? texte : texte.Substring(0, espace)).ToLower();
    string nomSource = espace < 0 ? "" : texte.Substring(espace + 1).Trim();

    if (commande == "rescan")
    {
        Rescanner();
        messageCommande = "Rescan effectué.";
    }
    else if ((commande == "on" || commande == "off") && nomSource.Length > 0)
    {
        // "on Intrusion majeur" : le dernier mot peut être un niveau de criticité.
        string niveau = "";
        int dernierEspace = nomSource.LastIndexOf(' ');
        if (commande == "on" && dernierEspace > 0 && TrouverRang(nomSource.Substring(dernierEspace + 1)) >= 0)
        {
            niveau = nomSource.Substring(dernierEspace + 1).ToLower();
            nomSource = nomSource.Substring(0, dernierEspace).Trim();
        }
        ForcerSource(nomSource, commande == "on", niveau);
    }
    else
    {
        messageCommande = "Commande inconnue : '" + argument + "' (attendu : on <Source> [niveau], off <Source> ou rescan)";
    }
}

/// <summary>
/// Écrit [Etat] Source=true/false dans le bus. Sert aux alarmes sans script
/// (bouton, capteur, minuterie qui lancent la Centrale avec un argument).
/// Si un script source publie déjà cette clé, il la réécrira à sa prochaine
/// exécution : on/off est fait pour les sources SANS script.
/// </summary>
/// <param name="niveau">Criticité à écrire dans [Criticite] (vide = ne rien changer).</param>
void ForcerSource(string nomSource, bool active, string niveau)
{
    if (serveur == null)
    {
        messageCommande = "Impossible : aucun bloc " + NOM_SERVEUR + ".";
        return;
    }
    LireBus();
    if (!busValide)
    {
        messageCommande = "Impossible : Custom Data de " + NOM_SERVEUR + " illisible.";
        return;
    }

    // On réutilise l'orthographe de la clé existante ("avaries" -> "Avaries").
    string cle = nomSource;
    clesEtat.Clear();
    lecteurIni.GetKeys(SECTION_ETAT, clesEtat);
    foreach (MyIniKey existante in clesEtat)
    {
        if (string.Equals(existante.Name, nomSource, StringComparison.OrdinalIgnoreCase))
        {
            cle = existante.Name;
            break;
        }
    }

    lecteurIni.Set(SECTION_ETAT, cle, active);
    if (niveau.Length > 0) lecteurIni.Set(SECTION_CRITICITE, cle, niveau);
    serveur.CustomData = lecteurIni.ToString();
    relectureForcee = true;   // le bus est relu juste après, dans Main
    messageCommande = "Alarme " + cle + (active ? " activée" : " désactivée")
                    + (niveau.Length > 0 ? " (" + niveau + ")." : ".");
}

// ------------------------------------------------------------------------------
// RESCAN (serveur, groupe d'alarme, écrans)
// ------------------------------------------------------------------------------

/// <summary>
/// Reconstruit les caches. Les blocs et écrans déjà connus sont réutilisés
/// pour conserver leur dernier état (pas de son relancé, pas de réécriture).
/// </summary>
void Rescanner()
{
    RechercherServeur();
    RechercherGroupe();
    RechercherEcrans();

    etatAChange = true;   // nouveaux blocs / écrans à synchroniser
    secondesDepuisRescan = 0;
    premierRescanFait = true;
}

/// <summary>Cherche SERVEUR_ALARMES (même logique que BusAlarmes.RechercherServeur).</summary>
void RechercherServeur()
{
    IMyTerminalBlock trouve = GridTerminalSystem.GetBlockWithName(NOM_SERVEUR);
    if (trouve != null && SEULEMENT_CETTE_CONSTRUCTION && !trouve.IsSameConstructAs(Me))
    {
        tamponRecherche.Clear();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tamponRecherche,
            b => b.CustomName == NOM_SERVEUR && b.IsSameConstructAs(Me));
        trouve = tamponRecherche.Count > 0 ? tamponRecherche[0] : null;
        tamponRecherche.Clear();
    }
    if (trouve != serveur)
    {
        serveur = trouve;
        relectureForcee = true;
    }
}

/// <summary>
/// Relit le groupe ALARMES et le filtre [Alarme] Sources=... de chaque bloc.
/// </summary>
void RechercherGroupe()
{
    tamponGroupe.Clear();
    IMyBlockGroup groupe = GridTerminalSystem.GetBlockGroupWithName(NOM_GROUPE_ALARMES);
    groupeTrouve = groupe != null;
    if (groupe != null) groupe.GetBlocksOfType<IMyFunctionalBlock>(tamponGroupe, EstDeCetteConstruction);

    tamponBlocs.Clear();
    foreach (IMyFunctionalBlock bloc in tamponGroupe)
    {
        BlocAlarme existant = null;
        foreach (BlocAlarme ancien in blocsAlarme)
        {
            if (ancien.Bloc == bloc) { existant = ancien; break; }
        }
        if (existant == null) existant = new BlocAlarme { Bloc = bloc };

        // Filtre relu à chaque rescan (le joueur a pu le modifier).
        existant.Filtre = LireFiltre(bloc.CustomData);
        tamponBlocs.Add(existant);
    }
    tamponGroupe.Clear();

    // Échange des listes : les blocs retirés du groupe sont oubliés.
    List<BlocAlarme> echange = blocsAlarme;
    blocsAlarme = tamponBlocs;
    tamponBlocs = echange;
    tamponBlocs.Clear();
}

/// <summary>
/// Lit [Alarme] Sources=A,B dans un Custom Data. Renvoie null (= toutes les
/// sources) si la section est absente, vide ou illisible.
/// </summary>
string[] LireFiltre(string customData)
{
    if (customData.IndexOf("[" + SECTION_FILTRE + "]", StringComparison.OrdinalIgnoreCase) < 0) return null;
    if (!lecteurFiltre.TryParse(customData)) return null;

    string liste = lecteurFiltre.Get(SECTION_FILTRE, CLE_FILTRE_SOURCES).ToString("").Trim();
    if (liste.Length == 0) return null;

    string[] noms = liste.Split(',');
    for (int i = 0; i < noms.Length; i++) noms[i] = noms[i].Trim();
    return noms;
}

/// <summary>
/// Cherche les écrans dont le NOM contient [alarme-line] ou [alarme-quant]
/// (éventuellement [alarme-line:N] pour la surface N d'un cockpit).
/// </summary>
void RechercherEcrans()
{
    balisesInvalides = 0;
    nombreEcransListe = 0;
    nombreEcransQuantite = 0;
    tamponEcrans.Clear();

    tamponRecherche.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(tamponRecherche,
        b => EstDeCetteConstruction(b) && b.CustomName.IndexOf("[alarme-", StringComparison.OrdinalIgnoreCase) >= 0);

    foreach (IMyTerminalBlock bloc in tamponRecherche)
    {
        AjouterEcran(bloc, BALISE_LISTE, false);
        AjouterEcran(bloc, BALISE_QUANTITE, true);
    }
    tamponRecherche.Clear();

    List<EcranAlarme> echange = ecrans;
    ecrans = tamponEcrans;
    tamponEcrans = echange;
    tamponEcrans.Clear();
}

/// <summary>Ajoute la surface désignée par une balise du nom du bloc (si présente).</summary>
void AjouterEcran(IMyTerminalBlock bloc, string balise, bool quantite)
{
    string nom = bloc.CustomName;
    int debut = nom.IndexOf(balise, StringComparison.OrdinalIgnoreCase);
    if (debut < 0) return;

    // Après la balise : "]" (surface 0) ou ":N]" (surface N).
    int indexSurface = 0;
    int suite = debut + balise.Length;
    int fin = nom.IndexOf(']', suite);
    if (fin < 0) { balisesInvalides++; return; }
    if (fin > suite)
    {
        string reste = nom.Substring(suite, fin - suite);
        if (reste[0] != ':' || !int.TryParse(reste.Substring(1), out indexSurface))
        {
            balisesInvalides++;
            return;
        }
    }

    IMyTextSurface surface = null;
    IMyTextPanel panneau = bloc as IMyTextPanel;
    if (panneau != null && indexSurface == 0)
    {
        surface = panneau;
    }
    else
    {
        IMyTextSurfaceProvider fournisseur = bloc as IMyTextSurfaceProvider;
        if (fournisseur != null && indexSurface >= 0 && indexSurface < fournisseur.SurfaceCount)
        {
            surface = fournisseur.GetSurface(indexSurface);
        }
    }
    if (surface == null) { balisesInvalides++; return; }

    EcranAlarme ecran = null;
    foreach (EcranAlarme ancien in ecrans)
    {
        if (ancien.Surface == surface && ancien.Quantite == quantite) { ecran = ancien; break; }
    }
    if (ecran == null) ecran = new EcranAlarme { Bloc = bloc, Surface = surface, Quantite = quantite };
    // Les cases sont redessinées à chaque rescan : un dessin en sprites peut se
    // perdre (chargement de la partie, écran recréé), contrairement au texte.
    if (quantite) ecran.DernierTexte = null;
    tamponEcrans.Add(ecran);

    if (quantite) nombreEcransQuantite++;
    else nombreEcransListe++;
}

/// <summary>Filtre de construction : écarte les vaisseaux amarrés par connecteur.</summary>
bool EstDeCetteConstruction(IMyTerminalBlock bloc)
{
    return !SEULEMENT_CETTE_CONSTRUCTION || bloc.IsSameConstructAs(Me);
}

// ------------------------------------------------------------------------------
// LECTURE DU BUS
// ------------------------------------------------------------------------------

/// <summary>
/// Relit SERVEUR_ALARMES seulement si son Custom Data a changé, puis construit
/// la liste des alarmes actives, triée par criticité (la plus grave d'abord),
/// puis par ordre d'arrivée (la plus ancienne d'abord). Relit aussi les
/// couleurs des niveaux dans [Couleurs].
/// </summary>
void LireBus()
{
    string texte = serveur == null ? "" : serveur.CustomData;
    if (!relectureForcee && texte == dernierCustomDataLu) return;
    relectureForcee = false;
    dernierCustomDataLu = texte;
    etatAChange = true;

    sourcesActives.Clear();
    nombreSources = 0;
    busValide = false;

    if (serveur == null)
    {
        diagnosticBus = "aucun bloc " + NOM_SERVEUR;
        return;
    }
    if (!lecteurIni.TryParse(texte))
    {
        diagnosticBus = "Custom Data de " + NOM_SERVEUR + " illisible (format INI)";
        return;
    }
    busValide = true;
    diagnosticBus = "connecté";

    for (int rang = 0; rang < couleursNiveaux.Length; rang++) couleursNiveaux[rang] = LireCouleurNiveau(rang);

    clesEtat.Clear();
    lecteurIni.GetKeys(SECTION_ETAT, clesEtat);
    nombreSources = clesEtat.Count;

    // Les clés sont parcourues dans l'ordre de [Etat] : si plusieurs sources
    // apparaissent en même temps, elles sont numérotées dans cet ordre.
    for (int i = 0; i < clesEtat.Count; i++)
    {
        string nom = clesEtat[i].Name;
        if (!lecteurIni.Get(SECTION_ETAT, nom).ToBoolean(false)) continue;

        // Ordre d'arrivée : numéro donné la première fois qu'on la voit active.
        int arrivee;
        if (!arrivees.TryGetValue(nom, out arrivee))
        {
            arrivee = ++compteurArrivees;
            arrivees[nom] = arrivee;
        }

        // Réutilisation d'un objet de la réserve.
        if (reserveSources.Count <= sourcesActives.Count) reserveSources.Add(new SourceAlarme());
        SourceAlarme source = reserveSources[sourcesActives.Count];
        source.Nom = nom;
        source.Rang = RangCriticite(nom);
        source.Arrivee = arrivee;
        source.Detail = lecteurIni.Get(SECTION_DETAIL, nom).ToString("").Trim();
        sourcesActives.Add(source);
    }

    // Une source redevenue inactive (ou supprimée) perd son numéro : si elle
    // revient, elle passe derrière les alarmes déjà présentes de son niveau.
    tamponNoms.Clear();
    foreach (string nom in arrivees.Keys)
    {
        if (!lecteurIni.Get(SECTION_ETAT, nom).ToBoolean(false)) tamponNoms.Add(nom);
    }
    foreach (string nom in tamponNoms) arrivees.Remove(nom);
    if (arrivees.Count == 0) compteurArrivees = 0;

    sourcesActives.Sort(comparateurSources);
}

/// <summary>Ordre d'affichage : criticité (la plus grave d'abord), puis ordre d'arrivée.</summary>
static int ComparerSources(SourceAlarme a, SourceAlarme b)
{
    if (a.Rang != b.Rang) return a.Rang.CompareTo(b.Rang);
    return a.Arrivee.CompareTo(b.Arrivee);
}

/// <summary>Rang de criticité d'une source lu dans [Criticite] (absente ou inconnue : normal).</summary>
int RangCriticite(string nomSource)
{
    int rang = TrouverRang(lecteurIni.Get(SECTION_CRITICITE, nomSource).ToString(""));
    return rang >= 0 ? rang : RANG_CRITICITE_DEFAUT;
}

/// <summary>
/// Rang d'un niveau écrit en toutes lettres : 0 = critique, 1 = majeur,
/// 2 = normal, 3 = mineur (anciens noms importante / normale / basse
/// acceptés). Renvoie -1 si le mot n'est pas un niveau.
/// </summary>
int TrouverRang(string niveau)
{
    string mot = (niveau ?? "").Trim();
    for (int rang = 0; rang < NIVEAUX_CRITICITE.Length; rang++)
    {
        foreach (string accepte in NIVEAUX_CRITICITE[rang])
        {
            if (string.Equals(mot, accepte, StringComparison.OrdinalIgnoreCase)) return rang;
        }
    }
    return -1;
}

/// <summary>
/// Lit la couleur "R,G,B" d'un niveau dans [Couleurs] (clé Critique, Majeur,
/// Normal ou Mineur). Couleur par défaut du niveau si absente ou mal écrite.
/// </summary>
Color LireCouleurNiveau(int rang)
{
    Color parDefaut = COULEURS_CRITICITE[rang];
    string[] morceaux = lecteurIni.Get(SECTION_COULEURS, LIBELLES_CRITICITE[rang]).ToString("").Split(',');
    if (morceaux.Length != 3) return parDefaut;

    int rouge, vert, bleu;
    if (!int.TryParse(morceaux[0].Trim(), out rouge)) return parDefaut;
    if (!int.TryParse(morceaux[1].Trim(), out vert)) return parDefaut;
    if (!int.TryParse(morceaux[2].Trim(), out bleu)) return parDefaut;

    return new Color(Borner(rouge), Borner(vert), Borner(bleu));
}

/// <summary>Ramène une composante de couleur dans l'intervalle 0..255.</summary>
static int Borner(int valeur)
{
    if (valeur < 0) return 0;
    if (valeur > 255) return 255;
    return valeur;
}

// ------------------------------------------------------------------------------
// APPLICATION (textes, gyrophares, écrans)
// ------------------------------------------------------------------------------

/// <summary>Construit le texte des écrans [alarme-line] et [alarme-quant].</summary>
void ConstruireTextes()
{
    Array.Clear(nombreParRang, 0, nombreParRang.Length);
    foreach (SourceAlarme source in sourcesActives) nombreParRang[source.Rang]++;

    couleurTexte = sourcesActives.Count > 0 ? couleursNiveaux[sourcesActives[0].Rang] : COULEUR_TEXTE_REPOS;

    // ---- Liste des alarmes ----------------------------------------------------
    StringBuilder texte = constructeurTexte;
    texte.Clear();
    texte.Append("=== ALARMES ===\n");
    if (!busValide)
    {
        texte.Append("Bus indisponible :\n").Append(diagnosticBus).Append('\n');
    }
    else
    {
        texte.Append("Actives : ").Append(sourcesActives.Count).Append(" / ").Append(nombreSources).Append("\n\n");
        if (sourcesActives.Count == 0) texte.Append("Aucune alarme active.\n");

        int affichees = Math.Min(sourcesActives.Count, MAX_LIGNES_LISTE);
        for (int i = 0; i < affichees; i++)
        {
            // Numéro = priorité affichée (1 = la plus urgente).
            SourceAlarme source = sourcesActives[i];
            texte.Append(i + 1).Append(". [").Append(LIBELLES_CRITICITE[source.Rang].ToUpper()).Append("] ")
                 .Append(source.Nom);
            // Détail publié par le script (ex. "Carburant + Uranium" pour Energie).
            if (source.Detail.Length > 0) texte.Append(" : ").Append(source.Detail);
            texte.Append('\n');
        }
        if (sourcesActives.Count > affichees)
        {
            texte.Append("... et ").Append(sourcesActives.Count - affichees).Append(" autres\n");
        }
    }
    texteListe = texte.ToString();

    // ---- Nombre par criticité -------------------------------------------------
    // Ce texte sert de "signature" : les cases ne sont redessinées que s'il change.
    texte.Clear();
    if (!busValide) texte.Append("indisponible");
    else
    {
        for (int rang = 0; rang < nombreParRang.Length; rang++) texte.Append(nombreParRang[rang]).Append(';');
    }
    texteQuantite = texte.ToString();
}

/// <summary>
/// Applique l'état aux blocs du groupe ALARMES. Pour chaque bloc, l'alarme
/// dominante est la première alarme active (liste déjà triée) acceptée par
/// son filtre. Chaque écriture n'est faite que si elle change quelque chose ;
/// un bloc sonore n'est lancé / arrêté que quand SON état change.
/// </summary>
void AppliquerAuxBlocs()
{
    foreach (BlocAlarme element in blocsAlarme)
    {
        IMyFunctionalBlock bloc = element.Bloc;
        if (bloc.Closed) continue;

        SourceAlarme dominante = TrouverDominante(element.Filtre);
        bool actif = dominante != null;

        // Allumage / extinction (le bloc doit être allumé AVANT Play()).
        if (bloc.Enabled != actif) bloc.Enabled = actif;

        IMyLightingBlock lumiere = bloc as IMyLightingBlock;
        // La couleur est celle du NIVEAU de l'alarme dominante.
        if (lumiere != null && actif)
        {
            Color couleur = couleursNiveaux[dominante.Rang];
            if (lumiere.Color != couleur) lumiere.Color = couleur;
        }

        IMySoundBlock son = bloc as IMySoundBlock;
        if (son != null && (!element.Synchronise || element.DernierEtat != actif))
        {
            if (actif) son.Play();
            else son.Stop();
        }

        element.DernierEtat = actif;
        element.Synchronise = true;
    }
}

/// <summary>Première alarme active acceptée par le filtre (null = aucune).</summary>
SourceAlarme TrouverDominante(string[] filtre)
{
    foreach (SourceAlarme source in sourcesActives)
    {
        if (filtre == null) return source;
        foreach (string nom in filtre)
        {
            if (string.Equals(nom, source.Nom, StringComparison.OrdinalIgnoreCase)) return source;
        }
    }
    return null;
}

/// <summary>Écrit les textes sur les écrans, seulement là où ils ont changé.</summary>
void EcrireEcrans()
{
    foreach (EcranAlarme ecran in ecrans)
    {
        if (ecran.Bloc.Closed) continue;   // bloc démonté : oublié au prochain rescan

        if (ecran.Quantite)
        {
            if (ecran.DernierTexte == texteQuantite) continue;
            DessinerCases(ecran);
            ecran.DernierTexte = texteQuantite;
            continue;
        }

        IMyTextSurface surface = ecran.Surface;
        if (surface.ContentType != ContentType.TEXT_AND_IMAGE) surface.ContentType = ContentType.TEXT_AND_IMAGE;
        if (ecran.DernierTexte != texteListe)
        {
            surface.WriteText(texteListe);
            ecran.DernierTexte = texteListe;
        }
        if (COLORER_TEXTE_ECRANS && surface.FontColor != couleurTexte) surface.FontColor = couleurTexte;
    }
}

/// <summary>
/// Dessine les 4 cases de criticité (nombre + libellé) en sprites, chacune dans
/// sa couleur. La disposition suit la forme de la surface :
///   très large (corner LCD) -> 4 cases en ligne ; à peu près carrée -> 2 x 2 ;
///   très haute -> 4 cases en colonne.
/// Une case à 0 alarme est assombrie, pour que les niveaux actifs ressortent.
/// </summary>
void DessinerCases(EcranAlarme ecran)
{
    IMyTextSurface surface = ecran.Surface;
    if (surface.ContentType != ContentType.SCRIPT) surface.ContentType = ContentType.SCRIPT;
    if (surface.Script != "") surface.Script = "";
    if (surface.ScriptBackgroundColor != Color.Black) surface.ScriptBackgroundColor = Color.Black;

    // Zone visible : SurfaceSize, centrée dans la texture (TextureSize).
    Vector2 taille = surface.SurfaceSize;
    Vector2 origine = (surface.TextureSize - taille) / 2f;

    float rapport = taille.X / Math.Max(taille.Y, 1f);
    int colonnes = rapport >= 2.5f ? 4 : (rapport >= 0.6f ? 2 : 1);
    int lignes = 4 / colonnes;
    Vector2 cellule = new Vector2(taille.X / colonnes, taille.Y / lignes);
    float marge = Math.Min(cellule.X, cellule.Y) * 0.06f;
    Vector2 tailleCase = cellule - new Vector2(2 * marge, 2 * marge);

    // Pas de "using" : le cadre doit être passé par ref à AjouterTexte
    // (c'est une struct ; Dispose() envoie le dessin à l'écran).
    MySpriteDrawFrame cadre = surface.DrawFrame();

    // Un sprite vide une fois sur deux : force le jeu à renvoyer le dessin
    // aux joueurs même s'il ressemble au précédent.
    if (ecran.Bascule) cadre.Add(new MySprite());
    ecran.Bascule = !ecran.Bascule;

    for (int rang = 0; rang < 4; rang++)
    {
        int nombre = nombreParRang[rang];
        bool allumee = busValide && nombre > 0;
        Color couleur = couleursNiveaux[rang];
        Color fond = allumee ? couleur : Attenuer(couleur, ATTENUATION_CASE_VIDE);
        Color encre = allumee ? COULEURS_TEXTE_CASE[rang] : COULEUR_TEXTE_CASE_VIDE;

        Vector2 centre = origine + new Vector2((rang % colonnes + 0.5f) * cellule.X,
                                               (rang / colonnes + 0.5f) * cellule.Y);
        cadre.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", centre, tailleCase, fond));

        // Nombre en grand (60 % de la hauteur), libellé dessous (25 %).
        string chiffre = busValide ? nombre.ToString() : "-";
        float hauteurChiffre = tailleCase.Y * 0.60f;
        float hauteurLibelle = tailleCase.Y * 0.25f;
        float largeurUtile = tailleCase.X * 0.90f;
        float hautCase = centre.Y - tailleCase.Y / 2f;

        AjouterTexte(ref cadre, surface, chiffre, encre, largeurUtile, hauteurChiffre,
                     new Vector2(centre.X, hautCase + tailleCase.Y * 0.05f));
        AjouterTexte(ref cadre, surface, LIBELLES_CRITICITE[rang], encre, largeurUtile, hauteurLibelle,
                     new Vector2(centre.X, hautCase + tailleCase.Y * 0.68f));
    }

    cadre.Dispose();
}

/// <summary>
/// Ajoute un texte centré horizontalement, à la plus grande taille qui tient
/// dans largeurMax x hauteurMax (mesurée avec MeasureStringInPixels).
/// </summary>
/// <param name="hautCentre">Centre horizontal et haut du rectangle réservé au texte.</param>
void AjouterTexte(ref MySpriteDrawFrame cadre, IMyTextSurface surface, string texte, Color couleur,
                  float largeurMax, float hauteurMax, Vector2 hautCentre)
{
    texteMesure.Clear();
    texteMesure.Append(texte);
    Vector2 mesure = surface.MeasureStringInPixels(texteMesure, POLICE_SPRITES, 1f);
    if (mesure.X <= 0 || mesure.Y <= 0) return;

    float echelle = Math.Min(largeurMax / mesure.X, hauteurMax / mesure.Y);
    // Centrage vertical dans la hauteur réservée.
    float decalage = (hauteurMax - mesure.Y * echelle) / 2f;

    MySprite sprite = MySprite.CreateText(texte, POLICE_SPRITES, couleur, echelle, TextAlignment.CENTER);
    sprite.Position = new Vector2(hautCentre.X, hautCentre.Y + decalage);
    cadre.Add(sprite);
}

/// <summary>Assombrit une couleur (facteur 0..1) sans toucher à sa transparence.</summary>
static Color Attenuer(Color couleur, float facteur)
{
    return new Color((int)(couleur.R * facteur), (int)(couleur.G * facteur), (int)(couleur.B * facteur));
}

/// <summary>Résumé court dans la fenêtre du bloc programmable (diagnostic).</summary>
void AfficherEcho()
{
    StringBuilder texte = constructeurTexte;
    texte.Clear();
    texte.Append("=== CENTRALE D'ALARMES ===\n");
    texte.Append("Bus : ").Append(diagnosticBus);
    if (busValide) texte.Append(" (").Append(sourcesActives.Count).Append(" active(s) / ").Append(nombreSources).Append(" sources)");
    texte.Append('\n');
    if (sourcesActives.Count > 0)
    {
        SourceAlarme dominante = sourcesActives[0];
        texte.Append("Dominante : ").Append(dominante.Nom).Append(" (").Append(LIBELLES_CRITICITE[dominante.Rang].ToLower()).Append(")\n");
    }
    if (groupeTrouve) texte.Append("Groupe ").Append(NOM_GROUPE_ALARMES).Append(" : ").Append(blocsAlarme.Count).Append(" blocs\n");
    else texte.Append("ATTENTION : groupe ").Append(NOM_GROUPE_ALARMES).Append(" introuvable\n");
    texte.Append("Écrans line : ").Append(nombreEcransListe).Append(" | quant : ").Append(nombreEcransQuantite);
    if (balisesInvalides > 0) texte.Append(" (").Append(balisesInvalides).Append(" balise(s) invalide(s))");
    texte.Append('\n');
    texte.Append("Dernière exécution : ").Append(Runtime.LastRunTimeMs.ToString("0.000")).Append(" ms\n");
    if (messageCommande.Length > 0) texte.Append(messageCommande).Append('\n');
    Echo(texte.ToString());
}

// ------------------------------------------------------------------------------
// STRUCTURES DE DONNÉES
// ------------------------------------------------------------------------------

/// <summary>Une alarme active lue dans le bus.</summary>
public class SourceAlarme
{
    public string Nom;      // clé de [Etat]
    public int Rang;        // 0 = critique, 1 = majeur, 2 = normal, 3 = mineur
    public int Arrivee;     // ordre d'arrivée (plus petit = plus ancienne)
    public string Detail;   // [Detail] Source=texte ("" si absent)
}

/// <summary>Un bloc du groupe ALARMES et son dernier état appliqué.</summary>
public class BlocAlarme
{
    public IMyFunctionalBlock Bloc;
    public string[] Filtre;     // null = réagit à toutes les sources
    public bool Synchronise;    // déjà traité au moins une fois ?
    public bool DernierEtat;    // dernier état appliqué (sert aux blocs sonores)
}

/// <summary>Une surface d'écran [alarme-line] ou [alarme-quant].</summary>
public class EcranAlarme
{
    public IMyTerminalBlock Bloc;   // bloc qui porte la balise dans son nom
    public IMyTextSurface Surface;
    public bool Quantite;       // true = [alarme-quant], false = [alarme-line]
    public string DernierTexte; // pour n'écrire que si le texte change
    public bool Bascule;        // alterne à chaque dessin en sprites (voir DessinerCases)
}

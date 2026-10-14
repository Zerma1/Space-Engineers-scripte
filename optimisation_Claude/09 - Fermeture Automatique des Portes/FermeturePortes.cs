// ==============================================================================
// FERMETURE AUTOMATIQUE DES PORTES - VERSION OPTIMISÉE (FermeturePortes.cs)
// ------------------------------------------------------------------------------
// Rôle :
//   Referme automatiquement toute porte restée ouverte plus de N secondes
//   (2 s par défaut), pour limiter les pertes d'air et les oublis.
//
// Nomenclature (balises dans le NOM de la porte, majuscules indifférentes) :
//   (aucune balise)  : porte gérée, délai par défaut (DELAI_PAR_DEFAUT_S)
//   [NoAuto]         : porte jamais refermée par ce script
//   [Auto:5]         : délai personnalisé en secondes (décimales acceptées :
//                      [Auto:2.5] ou [Auto:2,5]). Force aussi la gestion d'une
//                      porte de hangar.
//   [SAS-...]        : porte de sas, toujours ignorée (gérée par le script
//                      « Sas Automatique (Airlock) »)
//   Portes de hangar : ignorées par défaut (voir INCLURE_PORTES_HANGAR).
//
// Commandes (argument du Bloc Programmable) :
//   rescan          : relit immédiatement la liste des portes
//   pause  ou off   : suspend la fermeture automatique (mémorisé à la sauvegarde)
//   reprise ou on   : relance la fermeture automatique
//
// Dépendances : aucune.
// ==============================================================================

// ------------------------------------------------------------------------------
// CONSTANTES DE CONFIGURATION
// ------------------------------------------------------------------------------

// Délai (en secondes de jeu) avant fermeture d'une porte sans balise [Auto:x].
const double DELAI_PAR_DEFAUT_S = 2.0;

// Bornes acceptées pour un délai personnalisé [Auto:x] (hors bornes = délai par défaut).
const double DELAI_MINIMUM_S = 0.5;
const double DELAI_MAXIMUM_S = 3600.0;

// false : les portes de hangar (IMyAirtightHangarDoor) sont ignorées, sauf si
// elles portent une balise [Auto:x]. true : elles sont gérées comme les autres.
const bool INCLURE_PORTES_HANGAR = false;

// true : ne gère que les portes de CETTE construction (pas celles d'un vaisseau
// amarré par connecteur).
const bool SEULEMENT_CETTE_CONSTRUCTION = true;

// Intervalle (en secondes) du rescan automatique de la liste des portes.
const double INTERVALLE_RESCAN_S = 10.0;

// Balises reconnues dans le nom des portes.
const string BALISE_SAS = "[SAS-";
const string BALISE_EXCLUSION = "[NoAuto]";
const string PREFIXE_BALISE_DELAI = "[Auto:";

// ------------------------------------------------------------------------------
// DONNÉES
// ------------------------------------------------------------------------------

/// <summary>
/// Une porte gérée et son délai de fermeture (lu une fois au scan, pas à chaque
/// exécution : le nom n'est pas réanalysé en permanence).
/// </summary>
class PorteSuivie
{
    public IMyDoor Porte;
    public double DelaiSecondes;
}

// Cache des portes gérées (rempli par le scan, pas à chaque exécution).
List<PorteSuivie> portesGerees = new List<PorteSuivie>();

// Temps d'ouverture accumulé (secondes de jeu), indexé par l'EntityId de la porte.
// Un identifiant (long) est une clé stable et légère, contrairement à l'objet porte.
Dictionary<long, double> tempsOuverture = new Dictionary<long, double>();

// Collections réutilisées (pas d'allocation à chaque exécution).
List<IMyDoor> portesBrutes = new List<IMyDoor>();
HashSet<long> identifiantsPresents = new HashSet<long>();
List<long> identifiantsASupprimer = new List<long>();
StringBuilder texteConsole = new StringBuilder();

// Statistiques du dernier scan (pour la console).
int nombreExclues = 0;
List<string> avertissements = new List<string>();

// État général.
bool enPause = false;
double chronoRescan = 0;
string messageCommande = "";

/// <summary>
/// Constructeur : relit l'état « pause » sauvegardé, puis scanne les portes.
/// </summary>
public Program()
{
    enPause = (Storage == "pause");
    AppliquerFrequence();
    ScannerLesPortes();
}

/// <summary>
/// Sauvegarde : mémorise la pause pour qu'elle survive au rechargement de la partie.
/// </summary>
public void Save()
{
    Storage = enPause ? "pause" : "";
}

/// <summary>
/// En pause, le script ne tourne plus du tout (UpdateFrequency.None) : coût nul.
/// Il reste exécutable par un bouton (argument « reprise »).
/// </summary>
void AppliquerFrequence()
{
    Runtime.UpdateFrequency = enPause ? UpdateFrequency.None : UpdateFrequency.Update10;
}

/// <summary>
/// Point d'entrée : exécuté ~6 fois par seconde (Update10) ou par un bouton.
/// </summary>
public void Main(string argument, UpdateType updateSource)
{
    // Temps de JEU écoulé depuis la dernière exécution. Contrairement à
    // DateTime.Now (horloge réelle), il suit les ralentissements du serveur.
    double secondesEcoulees = Runtime.TimeSinceLastRun.TotalSeconds;

    if (!string.IsNullOrWhiteSpace(argument)) TraiterCommande(argument);

    if (!enPause)
    {
        chronoRescan += secondesEcoulees;
        if (chronoRescan >= INTERVALLE_RESCAN_S) ScannerLesPortes();
        SurveillerLesPortes(secondesEcoulees);
    }

    AfficherConsole();
}

/// <summary>
/// Pour chaque porte en cache : cumule son temps d'ouverture et la referme
/// une fois son délai atteint. Une porte refermée perd son chronomètre.
/// </summary>
void SurveillerLesPortes(double secondesEcoulees)
{
    bool porteDisparue = false;

    foreach (PorteSuivie suivie in portesGerees)
    {
        IMyDoor porte = suivie.Porte;
        long identifiant = porte.EntityId;
        DoorStatus statut = porte.Status;

        if (statut == DoorStatus.Opening || statut == DoorStatus.Open)
        {
            double temps;
            if (!tempsOuverture.TryGetValue(identifiant, out temps))
            {
                // Première exécution où la porte est vue ouverte : chrono à zéro.
                tempsOuverture[identifiant] = 0;
                continue;
            }

            temps += secondesEcoulees;
            if (temps < suivie.DelaiSecondes)
            {
                tempsOuverture[identifiant] = temps;
                continue;
            }

            // Délai atteint. On vérifie que la porte existe encore avant d'agir
            // (détruite / démontée depuis le dernier scan ?). Ce contrôle n'est fait
            // qu'ici, au moment d'agir, pour rester léger.
            if (GridTerminalSystem.GetBlockWithId(identifiant) == null)
            {
                porteDisparue = true;
            }
            else
            {
                porte.CloseDoor();
            }
            tempsOuverture.Remove(identifiant);
        }
        else if (tempsOuverture.ContainsKey(identifiant))
        {
            // Fermée ou en fermeture (par le joueur ou par nous) : fin du suivi.
            tempsOuverture.Remove(identifiant);
        }
    }

    // Une porte a disparu : on rescanne tout de suite (hors de la boucle,
    // car le scan modifie la liste parcourue).
    if (porteDisparue) ScannerLesPortes();
}

// ------------------------------------------------------------------------------
// SCAN DES PORTES
// ------------------------------------------------------------------------------

/// <summary>
/// Filtre du système de grille : même construction uniquement (si demandé).
/// </summary>
bool ConserverCettePorte(IMyDoor porte)
{
    return !SEULEMENT_CETTE_CONSTRUCTION || porte.IsSameConstructAs(Me);
}

/// <summary>
/// Reconstruit le cache des portes gérées, lit leurs balises, puis nettoie les
/// chronomètres des portes qui ne sont plus gérées (disparues ou exclues).
/// </summary>
void ScannerLesPortes()
{
    portesBrutes.Clear();
    portesGerees.Clear();
    identifiantsPresents.Clear();
    avertissements.Clear();
    nombreExclues = 0;

    GridTerminalSystem.GetBlocksOfType<IMyDoor>(portesBrutes, ConserverCettePorte);

    foreach (IMyDoor porte in portesBrutes)
    {
        string nom = porte.CustomName;

        // 1. Exclusions explicites : portes de sas et balise [NoAuto].
        if (nom.Contains(BALISE_SAS)
            || nom.IndexOf(BALISE_EXCLUSION, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            nombreExclues++;
            continue;
        }

        // 2. Délai : balise [Auto:x] ou délai par défaut.
        bool baliseDelaiPresente;
        double delai = LireDelai(nom, out baliseDelaiPresente);

        // 3. Portes de hangar : ignorées par défaut, sauf balise [Auto:x] explicite.
        //    Pourquoi : IMyAirtightHangarDoor hérite de IMyDoor, l'original les
        //    refermait donc au bout de 2 s (vaisseau coincé en plein passage).
        if (porte is IMyAirtightHangarDoor && !INCLURE_PORTES_HANGAR && !baliseDelaiPresente)
        {
            nombreExclues++;
            continue;
        }

        PorteSuivie suivie = new PorteSuivie();
        suivie.Porte = porte;
        suivie.DelaiSecondes = delai;
        portesGerees.Add(suivie);
        identifiantsPresents.Add(porte.EntityId);
    }

    // Nettoyage : chronomètres des portes qui ne sont plus gérées.
    identifiantsASupprimer.Clear();
    foreach (long identifiant in tempsOuverture.Keys)
    {
        if (!identifiantsPresents.Contains(identifiant)) identifiantsASupprimer.Add(identifiant);
    }
    foreach (long identifiant in identifiantsASupprimer)
    {
        tempsOuverture.Remove(identifiant);
    }

    chronoRescan = 0;
}

/// <summary>
/// Lit la balise [Auto:x] dans le nom de la porte et renvoie le délai en secondes.
/// Parsing robuste : casse ignorée, virgule ou point décimal, espaces tolérés.
/// Valeur absente, illisible ou hors bornes -> délai par défaut (+ avertissement).
/// </summary>
/// <param name="baliseTrouvee">Vrai si une balise [Auto:...] VALIDE est présente.</param>
double LireDelai(string nom, out bool baliseTrouvee)
{
    baliseTrouvee = false;

    int indexDebut = nom.IndexOf(PREFIXE_BALISE_DELAI, StringComparison.OrdinalIgnoreCase);
    if (indexDebut < 0) return DELAI_PAR_DEFAUT_S;
    indexDebut += PREFIXE_BALISE_DELAI.Length;

    int indexFin = nom.IndexOf(']', indexDebut);
    if (indexFin < 0)
    {
        avertissements.Add(nom + " : balise [Auto: non fermée");
        return DELAI_PAR_DEFAUT_S;
    }

    // "2,5" et "2.5" sont acceptés : on normalise sur le point, puis on lit avec
    // la culture invariante (indépendante de la langue du jeu / du serveur).
    string texteDelai = nom.Substring(indexDebut, indexFin - indexDebut).Trim().Replace(',', '.');
    double delai;
    if (!double.TryParse(texteDelai, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out delai))
    {
        avertissements.Add(nom + " : délai illisible");
        return DELAI_PAR_DEFAUT_S;
    }
    if (delai < DELAI_MINIMUM_S || delai > DELAI_MAXIMUM_S)
    {
        avertissements.Add(nom + " : délai hors bornes (" + DELAI_MINIMUM_S + " à " + DELAI_MAXIMUM_S + " s)");
        return DELAI_PAR_DEFAUT_S;
    }

    baliseTrouvee = true;
    return delai;
}

// ------------------------------------------------------------------------------
// COMMANDES ET CONSOLE
// ------------------------------------------------------------------------------

/// <summary>
/// Interprète l'argument reçu (bouton, barre d'outils, terminal).
/// </summary>
void TraiterCommande(string argument)
{
    string commande = argument.Trim().ToLower();

    if (commande == "rescan")
    {
        ScannerLesPortes();
        messageCommande = "Scan forcé terminé.";
    }
    else if (commande == "pause" || commande == "off")
    {
        enPause = true;
        tempsOuverture.Clear(); // On repartira de zéro à la reprise.
        AppliquerFrequence();
        messageCommande = "Fermeture automatique en PAUSE.";
    }
    else if (commande == "reprise" || commande == "on")
    {
        enPause = false;
        AppliquerFrequence();
        ScannerLesPortes(); // Des portes ont pu changer pendant la pause.
        messageCommande = "Fermeture automatique RELANCÉE.";
    }
    else
    {
        messageCommande = "Commande inconnue : « " + argument.Trim() + " » (rescan, pause/off, reprise/on).";
    }
}

/// <summary>
/// Résumé court dans la console du Bloc Programmable.
/// </summary>
void AfficherConsole()
{
    texteConsole.Clear();
    texteConsole.Append("Fermeture auto des portes : ").Append(enPause ? "EN PAUSE" : "ACTIVE").Append("\n");
    texteConsole.Append("Portes gérées : ").Append(portesGerees.Count)
                .Append(" | ignorées : ").Append(nombreExclues)
                .Append(" | ouvertes suivies : ").Append(tempsOuverture.Count).Append("\n");
    texteConsole.Append("Exécution : ").Append(Runtime.LastRunTimeMs.ToString("0.000")).Append(" ms\n");
    foreach (string avertissement in avertissements)
    {
        texteConsole.Append("Attention : ").Append(avertissement).Append("\n");
    }
    if (messageCommande != "") texteConsole.Append("Dernière commande : ").Append(messageCommande).Append("\n");
    Echo(texteConsole.ToString());
}

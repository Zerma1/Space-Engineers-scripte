// ==============================================================================
// GESTIONNAIRE DE SAS (AIRLOCK) INTELLIGENT - VERSION OPTIMISÉE
// ------------------------------------------------------------------------------
// Nomenclature des blocs (inchangée) :
//   [SAS-NomDuSas-int]  : porte intérieure (vers la base)
//   [SAS-NomDuSas-ext]  : porte extérieure (vers l'espace)
//   [SAS-NomDuSas-vent] : ventilation(s) à l'intérieur du sas (une ou plusieurs)
//   [SAS-NomDuSas-lcd]  : écran(s) de monitoring (optionnel)
//
// Commande disponible (argument du Bloc Programmable) :
//   rescan : relit tous les blocs (à utiliser après avoir construit/renommé un sas)
//
// Principe de fonctionnement (identique à la version d'origine) :
//   1. Le joueur "tape" la porte extérieure alors que le sas contient de l'air :
//      le script la referme aussitôt, lance la dépressurisation, puis l'ouvre
//      une fois le sas vide.
//   2. Même logique, en sens inverse, pour la porte intérieure (pressurisation).
//   3. Une porte ouverte verrouille l'autre : les deux ne peuvent jamais être
//      ouvertes en même temps.
// ==============================================================================

// ------------------------------------------------------------------------------
// CONSTANTES DE CONFIGURATION
// Regroupées ici pour être modifiées facilement sans fouiller dans la logique.
// ------------------------------------------------------------------------------

// Balise qui identifie un bloc comme faisant partie d'un sas.
const string PREFIXE_BALISE = "[SAS-";

// En dessous de ce niveau d'oxygène, le sas est considéré comme vide (0 % à 1 %).
const float SEUIL_VIDE = 0.01f;

// Au dessus de ce niveau d'oxygène, le sas est considéré comme plein (99 % à 100 %).
const float SEUIL_PLEIN = 0.99f;

// true  : ne gère que les blocs de CETTE construction (évite de piloter les sas
//         d'un vaisseau amarré par connecteur).
// false : gère tous les blocs balisés accessibles (comportement d'origine).
const bool SEULEMENT_CETTE_CONSTRUCTION = true;

// ------------------------------------------------------------------------------
// ÉTAT D'UN SAS
// Un enum remplace les deux booléens d'origine : un sas ne peut être que dans
// UN seul état à la fois, ce qui supprime les combinaisons impossibles.
// ------------------------------------------------------------------------------
enum EtatSas
{
    Repos,             // Rien en cours
    Depressurisation,  // On vide le sas avant d'ouvrir la porte extérieure
    Pressurisation     // On remplit le sas avant d'ouvrir la porte intérieure
}

// ------------------------------------------------------------------------------
// CLASSE : GESTIONNAIRE D'UN SEUL SAS
// ------------------------------------------------------------------------------
class GestionnaireSas
{
    public string NomDuSas;
    public IMyDoor PorteInterieure;
    public IMyDoor PorteExterieure;

    // Plusieurs ventilations possibles pour un même sas.
    public List<IMyAirVent> Ventilations = new List<IMyAirVent>();
    public List<IMyTextPanel> Ecrans = new List<IMyTextPanel>();

    // État courant du cycle de pressurisation.
    EtatSas etatCourant = EtatSas.Repos;

    // Dernier texte envoyé aux écrans : permet de ne réécrire que si ça a changé.
    string dernierTexteAffiche = "";

    // StringBuilder réutilisé : évite de créer de nombreuses chaînes temporaires
    // à chaque exécution (ce que faisait l'opérateur += d'origine).
    StringBuilder constructeurDeTexte = new StringBuilder();

    /// <summary>
    /// Un sas est exploitable s'il possède ses deux portes ET au moins une ventilation.
    /// </summary>
    public bool EstComplet()
    {
        return PorteInterieure != null && PorteExterieure != null && Ventilations.Count > 0;
    }

    /// <summary>
    /// Vérifie que tous les blocs du sas existent toujours (non détruits/démontés).
    /// Pourquoi : lire un bloc détruit pourrait faire planter le script.
    /// GetBlockWithId renvoie null si le bloc n'est plus dans le système terminal.
    /// </summary>
    public bool TousLesBlocsExistent(IMyGridTerminalSystem systemeDeGrille)
    {
        if (!BlocExiste(systemeDeGrille, PorteInterieure)) return false;
        if (!BlocExiste(systemeDeGrille, PorteExterieure)) return false;

        foreach (IMyAirVent ventilation in Ventilations)
        {
            if (!BlocExiste(systemeDeGrille, ventilation)) return false;
        }
        foreach (IMyTextPanel ecran in Ecrans)
        {
            if (!BlocExiste(systemeDeGrille, ecran)) return false;
        }
        return true;
    }

    static bool BlocExiste(IMyGridTerminalSystem systemeDeGrille, IMyTerminalBlock bloc)
    {
        return bloc != null && systemeDeGrille.GetBlockWithId(bloc.EntityId) != null;
    }

    /// <summary>
    /// Cœur de la logique : à appeler à chaque cycle du Bloc Programmable.
    /// Précondition : EstComplet() est vrai (garanti par le scanner).
    /// </summary>
    public void MettreAJour()
    {
        // L'oxygène est identique dans toute la pièce : on lit la 1ère ventilation.
        float niveauO2 = Ventilations[0].GetOxygenLevel();
        bool estEtanche = Ventilations[0].CanPressurize;

        // On lit chaque statut de porte UNE seule fois par cycle (lecture de propriété
        // répétée = travail inutile, et valeurs cohérentes pendant tout le cycle).
        DoorStatus statutExterieure = PorteExterieure.Status;
        DoorStatus statutInterieure = PorteInterieure.Status;

        // --- 1. PORTE EXTÉRIEURE ---
        if (statutExterieure == DoorStatus.Opening)
        {
            if (niveauO2 > SEUIL_VIDE)
            {
                // Il reste de l'air : on claque la porte et on aspire.
                PorteExterieure.CloseDoor();
                ReglerVentilations(true);
                etatCourant = EtatSas.Depressurisation;
            }
            else
            {
                // Sas déjà vide : la porte peut s'ouvrir, on verrouille l'autre.
                FermerEtVerrouiller(PorteInterieure);
                etatCourant = EtatSas.Repos;
            }
        }
        else if (statutExterieure == DoorStatus.Open)
        {
            FermerEtVerrouiller(PorteInterieure);
        }

        // --- 2. PORTE INTÉRIEURE ---
        if (statutInterieure == DoorStatus.Opening)
        {
            if (niveauO2 < SEUIL_PLEIN && estEtanche)
            {
                // Sas vide (ou partiel) et étanche : on claque la porte et on remplit.
                PorteInterieure.CloseDoor();
                ReglerVentilations(false);
                etatCourant = EtatSas.Pressurisation;
            }
            else
            {
                // Sas plein (ou non étanche) : la porte peut s'ouvrir.
                FermerEtVerrouiller(PorteExterieure);
                etatCourant = EtatSas.Repos;
            }
        }
        else if (statutInterieure == DoorStatus.Open)
        {
            FermerEtVerrouiller(PorteExterieure);
        }

        // --- 3. OUVERTURE AUTOMATIQUE ---
        // Les deux portes sont fermées : on les déverrouille, puis on ouvre la bonne
        // porte si le cycle en attente est terminé.
        if (statutInterieure == DoorStatus.Closed && statutExterieure == DoorStatus.Closed)
        {
            // Réécrire Enabled uniquement si nécessaire (évite des appels inutiles).
            if (!PorteInterieure.Enabled) PorteInterieure.Enabled = true;
            if (!PorteExterieure.Enabled) PorteExterieure.Enabled = true;

            if (etatCourant == EtatSas.Depressurisation && niveauO2 <= SEUIL_VIDE)
            {
                PorteExterieure.OpenDoor();
                etatCourant = EtatSas.Repos;
            }
            else if (etatCourant == EtatSas.Pressurisation && (niveauO2 >= SEUIL_PLEIN || !estEtanche))
            {
                // Le "|| !estEtanche" évite d'attendre indéfiniment un remplissage
                // impossible si le sas fuit.
                PorteInterieure.OpenDoor();
                etatCourant = EtatSas.Repos;
            }
        }

        AfficherStatut(niveauO2, estEtanche);
    }

    /// <summary>
    /// Commande toutes les ventilations du sas d'un coup.
    /// La propriété n'est modifiée que si sa valeur doit réellement changer.
    /// </summary>
    void ReglerVentilations(bool depressuriser)
    {
        foreach (IMyAirVent ventilation in Ventilations)
        {
            if (ventilation.Depressurize != depressuriser)
            {
                ventilation.Depressurize = depressuriser;
            }
        }
    }

    /// <summary>
    /// Ferme la porte (si elle ne l'est pas déjà) puis la verrouille (Enabled = false).
    /// </summary>
    void FermerEtVerrouiller(IMyDoor porte)
    {
        if (porte == null) return;

        // Inutile d'envoyer l'ordre de fermeture à une porte déjà fermée/en fermeture.
        if (porte.Status != DoorStatus.Closed && porte.Status != DoorStatus.Closing)
        {
            porte.CloseDoor();
        }
        if (porte.Enabled) porte.Enabled = false;
    }

    /// <summary>
    /// Met à jour les écrans LCD, mais SEULEMENT si le texte a changé.
    /// Pourquoi : écrire sur un écran à chaque cycle est coûteux et inutile
    /// quand rien ne bouge (cas le plus fréquent : sas au repos).
    /// </summary>
    void AfficherStatut(float niveauO2, bool estEtanche)
    {
        if (Ecrans.Count == 0) return;

        string statut = "PRÊT - EN ATTENTE";
        if (etatCourant == EtatSas.Depressurisation) statut = "DÉPRESSURISATION (Création du vide)";
        else if (etatCourant == EtatSas.Pressurisation) statut = "PRESSURISATION (Remplissage O2)";
        else if (!estEtanche) statut = "ALERTE : FUITE DÉTECTÉE (Non étanche)";

        constructeurDeTexte.Clear();
        constructeurDeTexte.Append("=== CONTRÔLE SAS : ").Append(NomDuSas.ToUpper()).Append(" ===\n\n");
        constructeurDeTexte.Append("STATUT : ").Append(statut).Append("\n");
        constructeurDeTexte.Append("OXYGÈNE : ").Append((niveauO2 * 100).ToString("0.0")).Append(" %\n\n");
        constructeurDeTexte.Append("--- SÉCURITÉ DES PORTES ---\n");
        constructeurDeTexte.Append("Intérieure (Base) : ").Append(ObtenirEtatPorte(PorteInterieure)).Append("\n");
        constructeurDeTexte.Append("Extérieure (Espace): ").Append(ObtenirEtatPorte(PorteExterieure)).Append("\n");

        string nouveauTexte = constructeurDeTexte.ToString();

        // Rien n'a changé depuis le dernier affichage : on ne touche pas aux écrans.
        if (nouveauTexte == dernierTexteAffiche) return;
        dernierTexteAffiche = nouveauTexte;

        foreach (IMyTextPanel ecran in Ecrans)
        {
            ecran.ContentType = ContentType.TEXT_AND_IMAGE;
            ecran.WriteText(nouveauTexte);
        }
    }

    string ObtenirEtatPorte(IMyDoor porte)
    {
        if (porte == null) return "NON DÉTECTÉE";
        if (porte.Status == DoorStatus.Open) return "OUVERTE";
        if (porte.Status == DoorStatus.Opening) return "EN OUVERTURE";
        if (porte.Status == DoorStatus.Closing) return "EN FERMETURE";
        if (!porte.Enabled) return "VERROUILLÉE (Sécurité)";
        return "FERMÉE";
    }
}

// ------------------------------------------------------------------------------
// LE CŒUR DU PROGRAMME
// ------------------------------------------------------------------------------
List<GestionnaireSas> ListeDesSas = new List<GestionnaireSas>();

// Noms des sas trouvés mais inutilisables (porte ou ventilation manquante),
// affichés dans la console du Bloc Programmable pour faciliter le diagnostic.
List<string> NomsDesSasIncomplets = new List<string>();

public Program()
{
    // Update10 = exécution toutes les 10 ticks (~6 fois par seconde) : suffisant
    // pour réagir à un joueur qui tape une porte.
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    ScannerLesSas();
}

public void Main(string argument, UpdateType updateSource)
{
    // Scan manuel demandé par le joueur.
    if (argument.Trim().ToLower() == "rescan")
    {
        ScannerLesSas();
        Echo("Scan forcé terminé.");
    }

    // Scan automatique si un bloc d'un sas a été détruit ou démonté :
    // le sas concerné est alors retiré (ou reconstruit sans le bloc manquant).
    foreach (GestionnaireSas sas in ListeDesSas)
    {
        if (!sas.TousLesBlocsExistent(GridTerminalSystem))
        {
            ScannerLesSas();
            break; // La liste vient d'être reconstruite : on sort de la boucle.
        }
    }

    foreach (GestionnaireSas sas in ListeDesSas)
    {
        sas.MettreAJour();
    }

    Echo("Airlock Intelligent Actif.\nNombre de sas gérés : " + ListeDesSas.Count);
    if (NomsDesSasIncomplets.Count > 0)
    {
        Echo("Sas incomplets (porte ou ventilation manquante) : " + string.Join(", ", NomsDesSasIncomplets));
    }
}

// ------------------------------------------------------------------------------
// LE SCANNER
// ------------------------------------------------------------------------------

/// <summary>
/// Filtre appliqué par le système de grille : ne ramène que les blocs balisés
/// (et, si demandé, ceux de la même construction que ce Bloc Programmable).
/// Filtrer à la source évite de manipuler tous les blocs de la base.
/// </summary>
bool ConserverCeBloc(IMyTerminalBlock bloc)
{
    if (!bloc.CustomName.Contains(PREFIXE_BALISE)) return false;
    if (SEULEMENT_CETTE_CONSTRUCTION && !bloc.IsSameConstructAs(Me)) return false;
    return true;
}

void ScannerLesSas()
{
    ListeDesSas.Clear();
    NomsDesSasIncomplets.Clear();

    Dictionary<string, GestionnaireSas> sasTemporaires = new Dictionary<string, GestionnaireSas>();
    List<IMyTerminalBlock> blocsBalises = new List<IMyTerminalBlock>();

    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocsBalises, ConserverCeBloc);

    foreach (IMyTerminalBlock bloc in blocsBalises)
    {
        string nomDuBloc = bloc.CustomName;

        // Extraction du contenu des crochets : "[SAS-Hangar-int]" -> "Hangar-int"
        int indexDebut = nomDuBloc.IndexOf(PREFIXE_BALISE) + PREFIXE_BALISE.Length;
        int indexFin = nomDuBloc.IndexOf("]", indexDebut);
        if (indexFin == -1) continue;

        string contenuCrochets = nomDuBloc.Substring(indexDebut, indexFin - indexDebut);

        // Le DERNIER tiret sépare le nom du sas de son rôle (le nom peut donc contenir des tirets).
        int indexTiret = contenuCrochets.LastIndexOf('-');
        if (indexTiret == -1) continue;

        string nomDuSas = contenuCrochets.Substring(0, indexTiret);
        string roleDuBloc = contenuCrochets.Substring(indexTiret + 1).ToLower();

        // Création du gestionnaire à la première rencontre du sas.
        GestionnaireSas sas;
        if (!sasTemporaires.TryGetValue(nomDuSas, out sas))
        {
            sas = new GestionnaireSas();
            sas.NomDuSas = nomDuSas;
            sasTemporaires[nomDuSas] = sas;
        }

        // Affectation du bloc à son rôle (le type du bloc est vérifié pour éviter les erreurs de nommage).
        if (roleDuBloc == "int" && bloc is IMyDoor)
            sas.PorteInterieure = (IMyDoor)bloc;
        else if (roleDuBloc == "ext" && bloc is IMyDoor)
            sas.PorteExterieure = (IMyDoor)bloc;
        else if (roleDuBloc == "vent" && bloc is IMyAirVent)
            sas.Ventilations.Add((IMyAirVent)bloc);
        else if (roleDuBloc == "lcd" && bloc is IMyTextPanel)
            sas.Ecrans.Add((IMyTextPanel)bloc);
    }

    // Seuls les sas complets sont gérés ; les autres sont signalés dans la console.
    foreach (GestionnaireSas sas in sasTemporaires.Values)
    {
        if (sas.EstComplet()) ListeDesSas.Add(sas);
        else NomsDesSasIncomplets.Add(sas.NomDuSas);
    }
}

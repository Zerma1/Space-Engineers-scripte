// Copyright (c) 2026 Zerma1 - https://github.com/Zerma1/Space-Engineers-scripte
// Licence : PolyForm Noncommercial 1.0.0 - https://polyformproject.org/licenses/noncommercial/1.0.0
// Usage et partage gratuits autorisés, vente interdite. / Free use and sharing allowed, selling prohibited.
// ==============================================================================
// SAS AUTOMATIQUE (AIRLOCK) INTELLIGENT - VERSION OPTIMISÉE (GestionnaireSas.cs)
// ------------------------------------------------------------------------------
// Rôle :
//   Gère automatiquement un ou plusieurs sas de décompression selon le principe
//   du « door bump » : le joueur tente d'ouvrir une porte, le script la referme
//   aussitôt si l'air n'est pas au bon niveau, vide ou remplit le sas, puis ouvre
//   la porte lui-même. Les deux portes d'un même sas ne sont jamais ouvertes en
//   même temps (verrouillage croisé).
//
// Nomenclature des blocs (dans le NOM du bloc, n'importe où) :
//   [SAS-NomDuSas-int]  : porte intérieure (vers la base)            - obligatoire
//   [SAS-NomDuSas-ext]  : porte extérieure (vers l'espace)           - obligatoire
//   [SAS-NomDuSas-vent] : ventilation(s) DANS le sas (une ou plus)   - obligatoire
//   [SAS-NomDuSas-lcd]  : écran(s) de suivi (LCD, ou surface 0 d'un
//                         cockpit / bloc à écrans)                    - optionnel
//   Le nom du sas peut contenir des tirets et des espaces : c'est le DERNIER
//   tiret qui sépare le nom du rôle. Ex. « Porte [SAS-Hangar Nord-ext] ».
//
// Commandes (argument du Bloc Programmable, majuscules indifférentes) :
//   rescan          : relit tous les blocs balisés immédiatement
//   cycle  <NomSas> : inverse le sas (plein -> sortie, vide -> entrée)
//   entrer <NomSas> : pressurise puis ouvre la porte intérieure
//   sortir <NomSas> : dépressurise puis ouvre la porte extérieure
//   (Le nom peut être omis s'il n'existe qu'un seul sas.)
//
// Dépendances : aucune (pas de serveur dictionnaire ni de serveur d'alarmes).
// Script compagnon : « Fermeture Automatique des Portes » ignore les portes [SAS-.
// ==============================================================================

// ------------------------------------------------------------------------------
// CONSTANTES DE CONFIGURATION
// Toutes les valeurs réglables sont ici : inutile de fouiller dans la logique.
// ------------------------------------------------------------------------------

// Balise qui identifie un bloc comme faisant partie d'un sas.
const string PREFIXE_BALISE = "[SAS-";

// En dessous de ce niveau d'oxygène (0 à 1), le sas est considéré comme vide.
const float SEUIL_VIDE = 0.01f;

// Au dessus de ce niveau d'oxygène (0 à 1), le sas est considéré comme plein.
const float SEUIL_PLEIN = 0.99f;

// true : ne gère que les blocs de CETTE construction (évite de piloter les sas
// d'un vaisseau amarré par connecteur). false : comportement d'origine.
const bool SEULEMENT_CETTE_CONSTRUCTION = true;

// Délai maximal (en secondes de jeu) pendant lequel le niveau d'O2 peut rester
// sans progresser pendant un cycle. Au-delà, le cycle est abandonné (ÉCHEC),
// les portes sont déverrouillées et l'écran affiche la cause probable.
const double DELAI_MAX_SANS_PROGRES_S = 15.0;

// Variation minimale du niveau d'O2 (0.02 = 2 points de %) considérée comme
// un « progrès » : chaque progrès remet le chronomètre du délai à zéro. Ainsi un
// grand sas qui se vide lentement mais sûrement n'est jamais abandonné à tort.
const float PROGRES_MINIMAL = 0.02f;

// Durée (en secondes) du mode manuel après un ÉCHEC : les portes ne sont plus
// interceptées (seul le verrouillage croisé reste actif), puis retour au repos.
const double DUREE_MODE_MANUEL_S = 30.0;

// Intervalle (en secondes) du rescan automatique léger des blocs balisés :
// prend en compte les sas construits ou renommés sans taper « rescan ».
const double INTERVALLE_RESCAN_S = 10.0;

// Nombre de caractères de la barre de progression O2 sur les écrans.
const int LARGEUR_BARRE_O2 = 10;

// ------------------------------------------------------------------------------
// ÉTATS D'UN SAS
// Un sas est dans UN SEUL état à la fois (machine à états) : cela supprime les
// combinaisons impossibles des deux booléens de la version d'origine.
// ------------------------------------------------------------------------------
enum EtatSas
{
    Repos,             // Aucun cycle en cours : les ouvertures sont surveillées
    Depressurisation,  // On vide le sas avant d'ouvrir la porte extérieure
    Pressurisation,    // On remplit le sas avant d'ouvrir la porte intérieure
    Echec,             // Cycle abandonné (délai dépassé) : mode manuel temporaire
    Panne              // Porte endommagée ou aucune ventilation opérationnelle
}

/// <summary>
/// Gère UN sas : ses blocs, sa machine à états et son affichage.
/// Une instance est conservée d'un rescan à l'autre (repérée par le nom du sas)
/// pour ne pas perdre un cycle en cours quand la liste des blocs est relue.
/// </summary>
class GestionnaireSas
{
    // --- Blocs du sas (réaffectés à chaque scan) ---
    public string NomDuSas;
    public IMyDoor PorteInterieure;
    public IMyDoor PorteExterieure;
    public List<IMyAirVent> Ventilations = new List<IMyAirVent>();

    // Blocs d'écran (pour vérifier qu'ils existent toujours) et surfaces
    // d'affichage correspondantes (là où l'on écrit vraiment le texte).
    public List<IMyTerminalBlock> BlocsEcrans = new List<IMyTerminalBlock>();
    public List<IMyTextSurface> Surfaces = new List<IMyTextSurface>();

    // --- État de la machine à états ---
    public EtatSas Etat = EtatSas.Repos;

    // Message d'alerte affiché à l'écran (vide = pas d'alerte).
    public string MessageAlerte = "";

    // Dernier niveau d'O2 lu (-1 = inconnu) : utilisé par les commandes.
    float dernierNiveauO2 = -1f;

    // Temps écoulé sans progrès significatif du niveau d'O2 pendant un cycle.
    double chronoSansProgres = 0;

    // Niveau d'O2 au dernier progrès constaté (référence de comparaison).
    float niveauReference = 0f;

    // Temps écoulé depuis l'entrée en mode manuel (état Echec).
    double chronoModeManuel = 0;

    // Statuts des portes lors de l'exécution précédente : servent à détecter le
    // « front montant » d'une ouverture (Fermée -> En ouverture), c'est-à-dire
    // le moment précis où un joueur tape la porte.
    DoorStatus statutPrecedentInt = DoorStatus.Closed;
    DoorStatus statutPrecedentExt = DoorStatus.Closed;

    // Liste réutilisée des ventilations réellement opérationnelles ce cycle-ci.
    List<IMyAirVent> ventilationsActives = new List<IMyAirVent>();

    // Dernier texte envoyé aux écrans : on ne réécrit que s'il a changé.
    string dernierTexteAffiche = "";

    // StringBuilder réutilisé : évite d'allouer des chaînes à chaque exécution.
    StringBuilder constructeurDeTexte = new StringBuilder();

    /// <summary>
    /// Oublie tous les blocs (appelé au début d'un rescan) SANS toucher à l'état
    /// du cycle : le sas garde sa mémoire, seuls ses blocs sont relus.
    /// </summary>
    public void ViderBlocs()
    {
        PorteInterieure = null;
        PorteExterieure = null;
        Ventilations.Clear();
        BlocsEcrans.Clear();
        Surfaces.Clear();
        // Le texte sera réécrit sur les (éventuels nouveaux) écrans.
        dernierTexteAffiche = "";
    }

    /// <summary>
    /// Un sas est exploitable s'il possède ses deux portes ET au moins une ventilation.
    /// </summary>
    public bool EstComplet()
    {
        return PorteInterieure != null && PorteExterieure != null && Ventilations.Count > 0;
    }

    /// <summary>
    /// Vérifie que tous les blocs du sas existent toujours (non détruits / démontés).
    /// GetBlockWithId renvoie null si le bloc a quitté le système terminal.
    /// </summary>
    public bool TousLesBlocsExistent(IMyGridTerminalSystem systemeDeGrille)
    {
        if (!BlocExiste(systemeDeGrille, PorteInterieure)) return false;
        if (!BlocExiste(systemeDeGrille, PorteExterieure)) return false;
        foreach (IMyAirVent ventilation in Ventilations)
        {
            if (!BlocExiste(systemeDeGrille, ventilation)) return false;
        }
        foreach (IMyTerminalBlock ecran in BlocsEcrans)
        {
            if (!BlocExiste(systemeDeGrille, ecran)) return false;
        }
        return true;
    }

    static bool BlocExiste(IMyGridTerminalSystem systemeDeGrille, IMyTerminalBlock bloc)
    {
        return bloc != null && systemeDeGrille.GetBlockWithId(bloc.EntityId) != null;
    }

    // ==========================================================================
    // BOUCLE PRINCIPALE DU SAS
    // ==========================================================================

    /// <summary>
    /// Cœur de la logique, appelé à chaque exécution du Bloc Programmable.
    /// Précondition : EstComplet() est vrai (garanti par le scanner).
    /// </summary>
    /// <param name="secondesEcoulees">Temps de jeu écoulé depuis l'exécution précédente.</param>
    public void MettreAJour(double secondesEcoulees)
    {
        // --- 1. Lecture des statuts des portes (une seule fois par exécution) ---
        DoorStatus statutInt = PorteInterieure.Status;
        DoorStatus statutExt = PorteExterieure.Status;

        // Un joueur vient-il de taper une porte ? (front montant de l'ouverture)
        bool ouvertureDemandeeInt = EstNouvelleOuverture(statutInt, statutPrecedentInt);
        bool ouvertureDemandeeExt = EstNouvelleOuverture(statutExt, statutPrecedentExt);

        // On mémorise les statuts pour la prochaine exécution. (OuvrirPorteCible
        // peut ensuite les surcharger quand c'est le script qui ouvre la porte.)
        statutPrecedentInt = statutInt;
        statutPrecedentExt = statutExt;

        // --- 2. Diagnostic de santé : portes et ventilations utilisables ? ---
        ventilationsActives.Clear();
        foreach (IMyAirVent ventilation in Ventilations)
        {
            // IsWorking = construite, allumée ET alimentée. Une ventilation hors
            // service peut renvoyer un niveau d'O2 faux : on ne la lit pas.
            if (ventilation.IsWorking) ventilationsActives.Add(ventilation);
        }

        string diagnosticPanne = DiagnostiquerPanne();
        if (diagnosticPanne != "")
        {
            // En panne : on abandonne tout cycle, on garde seulement la sécurité
            // (jamais deux portes ouvertes) et on affiche la cause.
            Etat = EtatSas.Panne;
            MessageAlerte = diagnosticPanne;
            dernierNiveauO2 = -1f;
            AppliquerVerrouillageCroise(statutInt, statutExt);
            AfficherStatut(-1f, true, statutInt, statutExt);
            return;
        }
        if (Etat == EtatSas.Panne)
        {
            // La panne est réparée : retour au fonctionnement normal.
            Etat = EtatSas.Repos;
            MessageAlerte = "";
        }

        // --- 3. Lecture de l'air (identique dans toute la pièce : 1ère ventilation active) ---
        float niveauO2 = ventilationsActives[0].GetOxygenLevel();
        bool estEtanche = ventilationsActives[0].CanPressurize;
        dernierNiveauO2 = niveauO2;

        // --- 4. Machine à états ---
        switch (Etat)
        {
            case EtatSas.Repos:
                TraiterRepos(niveauO2, statutInt, statutExt, ouvertureDemandeeInt, ouvertureDemandeeExt);
                break;
            case EtatSas.Depressurisation:
            case EtatSas.Pressurisation:
                TraiterCycle(niveauO2, estEtanche, statutInt, statutExt, ouvertureDemandeeInt, ouvertureDemandeeExt, secondesEcoulees);
                break;
            case EtatSas.Echec:
                TraiterModeManuel(statutInt, statutExt, secondesEcoulees);
                break;
        }

        // --- 5. Affichage (n'écrit que si le texte a changé) ---
        AfficherStatut(niveauO2, estEtanche, statutInt, statutExt);
    }

    /// <summary>
    /// Vrai si la porte vient de COMMENCER à s'ouvrir depuis l'exécution précédente.
    /// Pourquoi un front montant : la version d'origine re-testait la porte tant
    /// qu'elle était « En ouverture » ; or dès que la porte intérieure s'entrouvre,
    /// l'air du sas se mélange à celui de la base. Si la base n'est pas à 99 %,
    /// le niveau lu retombait sous le seuil et le script reclaquait la porte qu'il
    /// venait lui-même d'ouvrir. On ne juge donc une ouverture qu'une seule fois.
    /// </summary>
    static bool EstNouvelleOuverture(DoorStatus statutActuel, DoorStatus statutPrecedent)
    {
        return statutActuel == DoorStatus.Opening
            && statutPrecedent != DoorStatus.Opening
            && statutPrecedent != DoorStatus.Open;
    }

    /// <summary>
    /// Renvoie une description de la panne, ou "" si le sas est utilisable.
    /// IsFunctional = le bloc est assez construit / pas trop endommagé pour marcher.
    /// </summary>
    string DiagnostiquerPanne()
    {
        if (!PorteInterieure.IsFunctional) return "PANNE : porte intérieure endommagée";
        if (!PorteExterieure.IsFunctional) return "PANNE : porte extérieure endommagée";
        if (ventilationsActives.Count == 0) return "PANNE : aucune ventilation opérationnelle (énergie ? dégâts ? éteinte ?)";
        return "";
    }

    // ==========================================================================
    // TRAITEMENT DE CHAQUE ÉTAT
    // ==========================================================================

    /// <summary>
    /// État REPOS : on surveille les « door bumps ».
    /// - Porte extérieure tapée alors qu'il reste de l'air -> dépressurisation.
    /// - Porte intérieure tapée alors que le sas n'est pas plein -> pressurisation.
    /// - Sinon la porte s'ouvre normalement et le verrouillage croisé bloque l'autre.
    /// </summary>
    void TraiterRepos(float niveauO2, DoorStatus statutInt, DoorStatus statutExt,
                      bool ouvertureDemandeeInt, bool ouvertureDemandeeExt)
    {
        if (ouvertureDemandeeExt && niveauO2 > SEUIL_VIDE)
        {
            DemarrerCycle(EtatSas.Depressurisation, niveauO2);
            return;
        }
        if (ouvertureDemandeeInt && niveauO2 < SEUIL_PLEIN)
        {
            // Changement par rapport à l'original : on pressurise MÊME si le sas
            // n'est pas étanche. L'original ouvrait directement la porte intérieure,
            // ce qui vidait l'air de la BASE dans un sas qui fuit. Désormais le cycle
            // échoue proprement au bout du délai (« sas non étanche ») et le joueur
            // peut alors ouvrir la porte en connaissance de cause.
            DemarrerCycle(EtatSas.Pressurisation, niveauO2);
            return;
        }
        AppliquerVerrouillageCroise(statutInt, statutExt);
    }

    /// <summary>
    /// États DÉPRESSURISATION / PRESSURISATION : les deux portes restent fermées,
    /// les ventilations travaillent, puis la porte cible est ouverte par le script.
    /// Un délai sans progrès fait passer le sas en ÉCHEC.
    /// </summary>
    void TraiterCycle(float niveauO2, bool estEtanche, DoorStatus statutInt, DoorStatus statutExt,
                      bool ouvertureDemandeeInt, bool ouvertureDemandeeExt, double secondesEcoulees)
    {
        bool depressuriser = (Etat == EtatSas.Depressurisation);

        // a) Changement d'avis : le joueur tape la porte « de départ » -> on inverse.
        if (depressuriser && ouvertureDemandeeInt)
        {
            DemarrerCycle(EtatSas.Pressurisation, niveauO2);
            return;
        }
        if (!depressuriser && ouvertureDemandeeExt)
        {
            DemarrerCycle(EtatSas.Depressurisation, niveauO2);
            return;
        }

        // b) Porte cible (celle qu'on ouvrira à la fin) : fermée puis verrouillée,
        //    pour qu'on ne puisse pas l'ouvrir trop tôt. Porte d'origine : fermée,
        //    mais laissée déverrouillée pour permettre le changement d'avis.
        IMyDoor porteCible = depressuriser ? PorteExterieure : PorteInterieure;
        IMyDoor porteOrigine = depressuriser ? PorteInterieure : PorteExterieure;
        Verrouiller(porteCible);
        if (porteOrigine.Status == DoorStatus.Closed) Deverrouiller(porteOrigine);
        else Fermer(porteOrigine);

        // c) Réglage des ventilations (réappliqué à chaque fois : corrige un joueur
        //    qui aurait modifié le réglage à la main ; n'écrit que si nécessaire).
        ReglerVentilations(depressuriser);

        // d) Objectif atteint et sas bien fermé -> ouverture de la porte cible.
        bool deuxPortesFermees = statutInt == DoorStatus.Closed && statutExt == DoorStatus.Closed;
        bool objectifAtteint = depressuriser ? niveauO2 <= SEUIL_VIDE : niveauO2 >= SEUIL_PLEIN;
        if (deuxPortesFermees && objectifAtteint)
        {
            OuvrirPorteCible(depressuriser);
            Etat = EtatSas.Repos;
            return;
        }

        // e) Surveillance de la progression : tout progrès remet le chrono à zéro.
        bool progres = depressuriser
            ? niveauO2 <= niveauReference - PROGRES_MINIMAL
            : niveauO2 >= niveauReference + PROGRES_MINIMAL;
        if (progres)
        {
            niveauReference = niveauO2;
            chronoSansProgres = 0;
        }
        else
        {
            chronoSansProgres += secondesEcoulees;
        }

        // f) Délai dépassé -> abandon avec la cause la plus probable.
        if (chronoSansProgres >= DELAI_MAX_SANS_PROGRES_S)
        {
            string cause;
            if (depressuriser) cause = "ÉCHEC : réservoirs O2 pleins ?";
            else if (!estEtanche) cause = "ÉCHEC : sas non étanche (fuite) ?";
            else cause = "ÉCHEC : plus d'O2 (réservoirs vides ?)";
            PasserEnEchec(cause);
        }
    }

    /// <summary>
    /// État ÉCHEC (mode manuel temporaire) : les portes ne sont plus interceptées,
    /// seul le verrouillage croisé protège la base. Après DUREE_MODE_MANUEL_S et
    /// une fois les deux portes fermées, le sas revient au repos.
    /// </summary>
    void TraiterModeManuel(DoorStatus statutInt, DoorStatus statutExt, double secondesEcoulees)
    {
        chronoModeManuel += secondesEcoulees;
        AppliquerVerrouillageCroise(statutInt, statutExt);

        if (chronoModeManuel >= DUREE_MODE_MANUEL_S
            && statutInt == DoorStatus.Closed && statutExt == DoorStatus.Closed)
        {
            Etat = EtatSas.Repos;
            MessageAlerte = "";
        }
    }

    // ==========================================================================
    // ACTIONS
    // ==========================================================================

    /// <summary>
    /// Lance un cycle : ferme les deux portes, règle les ventilations et remet
    /// à zéro la surveillance du délai.
    /// </summary>
    void DemarrerCycle(EtatSas nouvelEtat, float niveauO2)
    {
        Etat = nouvelEtat;
        MessageAlerte = "";
        chronoSansProgres = 0;
        niveauReference = niveauO2;
        Fermer(PorteInterieure);
        Fermer(PorteExterieure);
        ReglerVentilations(nouvelEtat == EtatSas.Depressurisation);
    }

    /// <summary>
    /// Abandonne le cycle en cours : déverrouille les portes (le joueur n'est plus
    /// bloqué) et affiche la cause. Les ventilations sont laissées telles quelles.
    /// </summary>
    void PasserEnEchec(string cause)
    {
        Etat = EtatSas.Echec;
        MessageAlerte = cause;
        chronoModeManuel = 0;
        // Déverrouiller uniquement les portes fermées ; le verrouillage croisé
        // s'occupera des autres dès la prochaine exécution.
        if (PorteInterieure.Status == DoorStatus.Closed) Deverrouiller(PorteInterieure);
        if (PorteExterieure.Status == DoorStatus.Closed) Deverrouiller(PorteExterieure);
    }

    /// <summary>
    /// Ouvre la porte cible en fin de cycle (extérieure après dépressurisation,
    /// intérieure après pressurisation). Le statut précédent est forcé à
    /// « En ouverture » pour que cette ouverture, voulue par le script, ne soit
    /// pas prise pour un « door bump » du joueur à l'exécution suivante.
    /// </summary>
    void OuvrirPorteCible(bool exterieure)
    {
        IMyDoor porte = exterieure ? PorteExterieure : PorteInterieure;
        if (!porte.Enabled) porte.Enabled = true;
        porte.OpenDoor();
        if (exterieure) statutPrecedentExt = DoorStatus.Opening;
        else statutPrecedentInt = DoorStatus.Opening;
    }

    /// <summary>
    /// Verrouillage croisé : tant qu'une porte n'est pas complètement fermée,
    /// l'autre est fermée puis verrouillée. Si les DEUX sont ouvertes (démarrage du
    /// script, ouverture depuis le terminal...), la porte intérieure est fermée en
    /// priorité pour protéger l'air de la base. Deux portes fermées = déverrouillées.
    /// </summary>
    void AppliquerVerrouillageCroise(DoorStatus statutInt, DoorStatus statutExt)
    {
        if (statutExt != DoorStatus.Closed)
        {
            Verrouiller(PorteInterieure);
        }
        else if (statutInt != DoorStatus.Closed)
        {
            Verrouiller(PorteExterieure);
        }
        else
        {
            Deverrouiller(PorteInterieure);
            Deverrouiller(PorteExterieure);
        }
    }

    /// <summary>
    /// Ordonne la fermeture d'une porte si elle n'est ni fermée ni en fermeture.
    /// Une porte éteinte (Enabled = false) n'obéit pas : on la rallume d'abord.
    /// </summary>
    static void Fermer(IMyDoor porte)
    {
        DoorStatus statut = porte.Status;
        if (statut == DoorStatus.Closed || statut == DoorStatus.Closing) return;
        if (!porte.Enabled) porte.Enabled = true;
        porte.CloseDoor();
    }

    /// <summary>
    /// Ferme la porte, puis la verrouille (Enabled = false) SEULEMENT une fois
    /// complètement fermée. Correction : l'original éteignait la porte dans la même
    /// exécution que l'ordre de fermeture ; une porte éteinte en plein mouvement
    /// risque de rester bloquée entrouverte (et le sas avec).
    /// </summary>
    static void Verrouiller(IMyDoor porte)
    {
        if (porte.Status != DoorStatus.Closed)
        {
            Fermer(porte);
            return;
        }
        if (porte.Enabled) porte.Enabled = false;
    }

    /// <summary>
    /// Déverrouille une porte (Enabled = true), uniquement si nécessaire.
    /// </summary>
    static void Deverrouiller(IMyDoor porte)
    {
        if (!porte.Enabled) porte.Enabled = true;
    }

    /// <summary>
    /// Règle TOUTES les ventilations du sas (aspiration ou remplissage).
    /// La propriété n'est écrite que si sa valeur doit changer.
    /// </summary>
    void ReglerVentilations(bool depressuriser)
    {
        foreach (IMyAirVent ventilation in Ventilations)
        {
            if (ventilation.Depressurize != depressuriser) ventilation.Depressurize = depressuriser;
        }
    }

    // ==========================================================================
    // COMMANDES (bouton, barre d'outils...)
    // ==========================================================================

    /// <summary>
    /// Exécute une commande sur ce sas et renvoie un message pour la console.
    /// </summary>
    /// <param name="action">"cycle", "entrer" ou "sortir" (déjà en minuscules).</param>
    public string Commander(string action)
    {
        if (Etat == EtatSas.Panne) return "Sas " + NomDuSas + " en panne : commande refusée.";
        if (dernierNiveauO2 < 0f) return "Sas " + NomDuSas + " pas encore lu : réessayez.";

        // « cycle » : inverse un cycle en cours, sinon choisit selon le niveau d'air.
        if (action == "cycle")
        {
            if (Etat == EtatSas.Depressurisation) action = "entrer";
            else if (Etat == EtatSas.Pressurisation) action = "sortir";
            else action = dernierNiveauO2 >= 0.5f ? "sortir" : "entrer";
        }

        if (action == "sortir")
        {
            if (PorteExterieure.Status == DoorStatus.Open) return "Sas " + NomDuSas + " : porte extérieure déjà ouverte.";
            DemarrerCycle(EtatSas.Depressurisation, dernierNiveauO2);
            return "Sas " + NomDuSas + " : sortie (dépressurisation).";
        }
        if (PorteInterieure.Status == DoorStatus.Open) return "Sas " + NomDuSas + " : porte intérieure déjà ouverte.";
        DemarrerCycle(EtatSas.Pressurisation, dernierNiveauO2);
        return "Sas " + NomDuSas + " : entrée (pressurisation).";
    }

    // ==========================================================================
    // AFFICHAGE
    // ==========================================================================

    /// <summary>
    /// Ligne de résumé pour la console (Echo) du Bloc Programmable.
    /// </summary>
    public string Resume()
    {
        string resume = NomDuSas + " : " + LibelleEtat();
        if (MessageAlerte != "") resume += " (" + MessageAlerte + ")";
        return resume;
    }

    string LibelleEtat()
    {
        switch (Etat)
        {
            case EtatSas.Depressurisation: return "DÉPRESSURISATION";
            case EtatSas.Pressurisation: return "PRESSURISATION";
            case EtatSas.Echec: return "ÉCHEC - MODE MANUEL";
            case EtatSas.Panne: return "PANNE";
            default: return "PRÊT";
        }
    }

    /// <summary>
    /// Met à jour les écrans, SEULEMENT si le texte a changé (le cas le plus
    /// fréquent, sas au repos, ne coûte donc presque rien).
    /// </summary>
    /// <param name="niveauO2">Niveau d'O2 de 0 à 1, ou -1 si inconnu.</param>
    void AfficherStatut(float niveauO2, bool estEtanche, DoorStatus statutInt, DoorStatus statutExt)
    {
        if (Surfaces.Count == 0) return;

        StringBuilder texte = constructeurDeTexte;
        texte.Clear();
        texte.Append("=== SAS : ").Append(NomDuSas.ToUpper()).Append(" ===\n\n");
        texte.Append("ÉTAT : ").Append(LibelleEtat()).Append("\n");

        // Barre de progression texte, ex. « O2 : [#####-----] 50.0 % ».
        texte.Append("O2   : ");
        if (niveauO2 < 0f)
        {
            texte.Append("inconnu\n");
        }
        else
        {
            AjouterBarre(texte, niveauO2);
            texte.Append(" ").Append((niveauO2 * 100f).ToString("0.0")).Append(" %\n");
        }

        // Pendant un cycle : temps écoulé sans progrès (arrondi à la seconde pour
        // ne pas réécrire l'écran à chaque exécution).
        if (Etat == EtatSas.Depressurisation || Etat == EtatSas.Pressurisation)
        {
            texte.Append("Sans progrès : ").Append(((int)chronoSansProgres).ToString())
                 .Append(" / ").Append(((int)DELAI_MAX_SANS_PROGRES_S).ToString()).Append(" s\n");
        }

        // Alerte : message explicite, sinon fuite constatée au repos (portes fermées).
        if (MessageAlerte != "")
        {
            texte.Append("\n/!\\ ").Append(MessageAlerte).Append("\n");
            if (Etat == EtatSas.Echec) texte.Append("Portes déverrouillées (mode manuel).\n");
        }
        else if (Etat == EtatSas.Repos && !estEtanche
                 && statutInt == DoorStatus.Closed && statutExt == DoorStatus.Closed)
        {
            texte.Append("\n/!\\ ALERTE : FUITE (sas non étanche)\n");
        }

        texte.Append("\n--- PORTES ---\n");
        texte.Append("Intérieure (base)   : ").Append(LibellePorte(PorteInterieure, statutInt)).Append("\n");
        texte.Append("Extérieure (espace) : ").Append(LibellePorte(PorteExterieure, statutExt)).Append("\n");

        string nouveauTexte = texte.ToString();
        if (nouveauTexte == dernierTexteAffiche) return;
        dernierTexteAffiche = nouveauTexte;

        foreach (IMyTextSurface surface in Surfaces)
        {
            // Le mode texte n'est imposé que s'il n'est pas déjà actif.
            if (surface.ContentType != ContentType.TEXT_AND_IMAGE) surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.WriteText(nouveauTexte);
        }
    }

    /// <summary>
    /// Ajoute une barre « [#####-----] » proportionnelle au ratio (0 à 1).
    /// </summary>
    static void AjouterBarre(StringBuilder texte, float ratio)
    {
        int pleins = (int)Math.Round(ratio * LARGEUR_BARRE_O2);
        if (pleins < 0) pleins = 0;
        if (pleins > LARGEUR_BARRE_O2) pleins = LARGEUR_BARRE_O2;
        texte.Append('[').Append('#', pleins).Append('-', LARGEUR_BARRE_O2 - pleins).Append(']');
    }

    static string LibellePorte(IMyDoor porte, DoorStatus statut)
    {
        if (!porte.IsFunctional) return "ENDOMMAGÉE";
        if (statut == DoorStatus.Open) return "OUVERTE";
        if (statut == DoorStatus.Opening) return "EN OUVERTURE";
        if (statut == DoorStatus.Closing) return "EN FERMETURE";
        if (!porte.Enabled) return "VERROUILLÉE";
        return "FERMÉE";
    }
}

// ------------------------------------------------------------------------------
// LE CŒUR DU PROGRAMME
// ------------------------------------------------------------------------------

// Tous les sas connus, complets ou non, indexés par leur nom. Conservés d'un
// rescan à l'autre pour ne pas perdre un cycle en cours.
Dictionary<string, GestionnaireSas> sasParNom = new Dictionary<string, GestionnaireSas>();

// Sas complets (gérés) et noms des sas incomplets (signalés dans la console).
List<GestionnaireSas> ListeDesSas = new List<GestionnaireSas>();
List<string> NomsDesSasIncomplets = new List<string>();

// Collections réutilisées par le scanner (pas d'allocation à chaque scan).
List<IMyTerminalBlock> blocsBalises = new List<IMyTerminalBlock>();
Dictionary<string, GestionnaireSas> sasTrouves = new Dictionary<string, GestionnaireSas>();

// Temps écoulé depuis le dernier scan (pour le rescan périodique).
double chronoRescan = 0;

// Dernier message lié à une commande (affiché dans la console).
string messageCommande = "";

// Construction du texte de la console, réutilisée.
StringBuilder texteConsole = new StringBuilder();

/// <summary>
/// Constructeur : appelé à la compilation et au chargement de la partie.
/// </summary>
public Program()
{
    // Update10 = une exécution toutes les 10 ticks (~6 fois par seconde) :
    // suffisant pour réagir à un joueur qui tape une porte.
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    ScannerLesSas();
}

/// <summary>
/// Point d'entrée : exécuté automatiquement (Update10) ou par un bouton (argument).
/// </summary>
public void Main(string argument, UpdateType updateSource)
{
    // Temps de JEU écoulé depuis la dernière exécution (suit la vitesse de simulation).
    double secondesEcoulees = Runtime.TimeSinceLastRun.TotalSeconds;

    // 1. Rescan : périodique, ou immédiat si un bloc d'un sas a disparu.
    chronoRescan += secondesEcoulees;
    bool besoinRescan = chronoRescan >= INTERVALLE_RESCAN_S;
    if (!besoinRescan)
    {
        foreach (GestionnaireSas sas in ListeDesSas)
        {
            if (!sas.TousLesBlocsExistent(GridTerminalSystem))
            {
                besoinRescan = true;
                break;
            }
        }
    }
    if (besoinRescan) ScannerLesSas();

    // 2. Commande éventuelle (bouton, barre d'outils, terminal).
    if (!string.IsNullOrWhiteSpace(argument)) TraiterCommande(argument);

    // 3. Mise à jour de chaque sas.
    foreach (GestionnaireSas sas in ListeDesSas)
    {
        sas.MettreAJour(secondesEcoulees);
    }

    AfficherConsole();
}

/// <summary>
/// Interprète l'argument : « verbe [nom du sas] ».
/// Le nom peut contenir des espaces ; il est comparé sans tenir compte de la casse.
/// </summary>
void TraiterCommande(string argument)
{
    string brut = argument.Trim();
    int indexEspace = brut.IndexOf(' ');
    string verbe = (indexEspace < 0 ? brut : brut.Substring(0, indexEspace)).ToLower();
    string nomDemande = indexEspace < 0 ? "" : brut.Substring(indexEspace + 1).Trim();

    if (verbe == "rescan")
    {
        ScannerLesSas();
        messageCommande = "Scan forcé terminé.";
        return;
    }

    if (verbe != "cycle" && verbe != "entrer" && verbe != "sortir")
    {
        messageCommande = "Commande inconnue : « " + brut + " » (rescan, cycle, entrer, sortir).";
        return;
    }

    GestionnaireSas cible = TrouverSas(nomDemande);
    if (cible == null)
    {
        messageCommande = "Sas introuvable : « " + nomDemande + " ». Sas gérés : " + ListerNomsDesSas();
        return;
    }
    messageCommande = cible.Commander(verbe);
}

/// <summary>
/// Cherche un sas complet par son nom (casse ignorée). Nom vide : accepté
/// seulement s'il n'existe qu'un seul sas (sinon ambigu -> null).
/// </summary>
GestionnaireSas TrouverSas(string nomDemande)
{
    if (nomDemande == "") return ListeDesSas.Count == 1 ? ListeDesSas[0] : null;
    foreach (GestionnaireSas sas in ListeDesSas)
    {
        if (string.Equals(sas.NomDuSas, nomDemande, StringComparison.OrdinalIgnoreCase)) return sas;
    }
    return null;
}

string ListerNomsDesSas()
{
    if (ListeDesSas.Count == 0) return "(aucun)";
    StringBuilder noms = new StringBuilder();
    foreach (GestionnaireSas sas in ListeDesSas)
    {
        if (noms.Length > 0) noms.Append(", ");
        noms.Append(sas.NomDuSas);
    }
    return noms.ToString();
}

/// <summary>
/// Résumé court dans la console du Bloc Programmable.
/// </summary>
void AfficherConsole()
{
    texteConsole.Clear();
    texteConsole.Append("Sas automatique actif\n");
    texteConsole.Append("Sas gérés : ").Append(ListeDesSas.Count)
                .Append(" | Exécution : ").Append(Runtime.LastRunTimeMs.ToString("0.000")).Append(" ms\n");
    foreach (GestionnaireSas sas in ListeDesSas)
    {
        texteConsole.Append("- ").Append(sas.Resume()).Append("\n");
    }
    if (NomsDesSasIncomplets.Count > 0)
    {
        texteConsole.Append("Sas incomplets (porte int/ext ou ventilation manquante) : ")
                    .Append(string.Join(", ", NomsDesSasIncomplets)).Append("\n");
    }
    if (messageCommande != "") texteConsole.Append("Dernière commande : ").Append(messageCommande).Append("\n");
    Echo(texteConsole.ToString());
}

// ------------------------------------------------------------------------------
// LE SCANNER
// ------------------------------------------------------------------------------

/// <summary>
/// Filtre appliqué par le système de grille : ne ramène que les blocs balisés
/// (et, si demandé, ceux de la même construction que ce Bloc Programmable).
/// </summary>
bool ConserverCeBloc(IMyTerminalBlock bloc)
{
    if (!bloc.CustomName.Contains(PREFIXE_BALISE)) return false;
    if (SEULEMENT_CETTE_CONSTRUCTION && !bloc.IsSameConstructAs(Me)) return false;
    return true;
}

/// <summary>
/// Relit tous les blocs balisés et les répartit par sas et par rôle.
/// Les instances de sas déjà connues sont RÉUTILISÉES (même nom) : un cycle en
/// cours n'est donc pas interrompu par un rescan périodique.
/// </summary>
void ScannerLesSas()
{
    blocsBalises.Clear();
    sasTrouves.Clear();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocsBalises, ConserverCeBloc);

    foreach (IMyTerminalBlock bloc in blocsBalises)
    {
        string nomDuBloc = bloc.CustomName;

        // Extraction du contenu des crochets : "[SAS-Hangar-int]" -> "Hangar-int"
        int indexDebut = nomDuBloc.IndexOf(PREFIXE_BALISE) + PREFIXE_BALISE.Length;
        int indexFin = nomDuBloc.IndexOf("]", indexDebut);
        if (indexFin == -1) continue;
        string contenuCrochets = nomDuBloc.Substring(indexDebut, indexFin - indexDebut);

        // Le DERNIER tiret sépare le nom du sas de son rôle.
        int indexTiret = contenuCrochets.LastIndexOf('-');
        if (indexTiret <= 0) continue;
        string nomDuSas = contenuCrochets.Substring(0, indexTiret);
        string roleDuBloc = contenuCrochets.Substring(indexTiret + 1).ToLower();

        // Première rencontre de ce sas pendant ce scan : on réutilise l'instance
        // existante (état conservé) ou on en crée une, puis on vide ses blocs.
        GestionnaireSas sas;
        if (!sasTrouves.TryGetValue(nomDuSas, out sas))
        {
            if (!sasParNom.TryGetValue(nomDuSas, out sas))
            {
                sas = new GestionnaireSas();
                sas.NomDuSas = nomDuSas;
            }
            sas.ViderBlocs();
            sasTrouves[nomDuSas] = sas;
        }

        // Affectation du bloc à son rôle (le type est vérifié pour éviter les erreurs
        // de nommage, ex. un écran nommé [SAS-X-int] est ignoré).
        if (roleDuBloc == "int" && bloc is IMyDoor)
        {
            sas.PorteInterieure = (IMyDoor)bloc;
        }
        else if (roleDuBloc == "ext" && bloc is IMyDoor)
        {
            sas.PorteExterieure = (IMyDoor)bloc;
        }
        else if (roleDuBloc == "vent" && bloc is IMyAirVent)
        {
            sas.Ventilations.Add((IMyAirVent)bloc);
        }
        else if (roleDuBloc == "lcd")
        {
            // Écran LCD classique, ou première surface d'un bloc à écrans (cockpit...).
            if (bloc is IMyTextPanel)
            {
                sas.BlocsEcrans.Add(bloc);
                sas.Surfaces.Add((IMyTextSurface)bloc);
            }
            else if (bloc is IMyTextSurfaceProvider && ((IMyTextSurfaceProvider)bloc).SurfaceCount > 0)
            {
                sas.BlocsEcrans.Add(bloc);
                sas.Surfaces.Add(((IMyTextSurfaceProvider)bloc).GetSurface(0));
            }
        }
    }

    // Les sas non retrouvés sont oubliés ; les autres remplacent la liste connue.
    sasParNom.Clear();
    ListeDesSas.Clear();
    NomsDesSasIncomplets.Clear();
    foreach (KeyValuePair<string, GestionnaireSas> paire in sasTrouves)
    {
        sasParNom[paire.Key] = paire.Value;
        if (paire.Value.EstComplet()) ListeDesSas.Add(paire.Value);
        else NomsDesSasIncomplets.Add(paire.Key);
    }

    chronoRescan = 0;
}

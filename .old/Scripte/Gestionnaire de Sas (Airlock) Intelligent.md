# GUIDE DE L'INGÉNIEUR : Gestionnaire de Sas (Airlock) Intelligent

Bienvenue dans ce guide dédié à la survie et à l'ingénierie avancée dans Space Engineers ! Aujourd'hui, nous allons aborder un classique de la survie dans l'espace : la **dépressurisation**. Ouvrir une porte donnant sur le vide sans sas fonctionnel, c'est gaspiller l'oxygène durement généré et risquer d'aspirer le matériel environnant.

Ce guide vous fournit un script en C# (à placer dans un Bloc Programmable) qui va automatiser la sécurité de vos sas de décompression.

## 1. Pourquoi ce script ? (L'approche "Door Bump")

Ce script repose sur une technique très appréciée par la communauté : la **collision de porte (Door Bump)**. Contrairement à des systèmes complexes avec des Boutons Poussoirs, ce script analyse _votre_ intention :

1. Vous vous approchez de la porte extérieure et tentez de l'ouvrir.
    
2. Le script détecte l'ouverture, mais voit que la pièce contient encore de l'oxygène.
    
3. Il **claque immédiatement la porte** pour empêcher l'air de sortir.
    
4. Il active la ventilation pour vider la pièce.
    
5. Une fois le sas vidé (0% O2), le script ouvre la porte pour vous !
    

### Prérequis de construction :

Pour qu'un sas soit fonctionnel et reconnu par le script, il DOIT contenir exactement 3 éléments, nommés selon cette règle :

- **La porte intérieure (vers la base) :** `[SAS-NomDuSas-int]`
    
- **La porte extérieure (vers l'espace) :** `[SAS-NomDuSas-ext]`
    
- **La ventilation à l'intérieur du sas :** `[SAS-NomDuSas-vent]`

- **Le LCD de monitoring :** `[SAS-NomDuSas-lcd]`

_Exemple de noms valides pour le sas du Hangar :_ `Porte Blindée [SAS-Hangar-int]`, `Porte Spatiale [SAS-Hangar-ext]`, `Grille [SAS-Hangar-vent]`.

## 2. Le Script Décortiqué

Copiez ce code dans un Bloc Programmable. Lisez les commentaires (lignes commençant par `//`) pour comprendre le comportement du script !

C#

``` C#
// ==============================================================================
// SCRIPT DE GESTION DES SAS (AIRLOCK) + MONITORING LCD + MULTI-VENTILATIONS
// Nomenclature : [SAS-NomDuSas-int], [SAS-NomDuSas-ext], [SAS-NomDuSas-vent], [SAS-NomDuSas-lcd]
// ==============================================================================

class GestionnaireSas
{
    public string NomDuSas;
    public IMyDoor PorteInterieure;
    public IMyDoor PorteExterieure;
    
    // NOUVEAU : Une liste pour gérer plusieurs ventilations simultanément
    public List<IMyAirVent> Ventilations = new List<IMyAirVent>();
    public List<IMyTextPanel> Ecrans = new List<IMyTextPanel>();

    bool EnAttenteDepressurisation = false;
    bool EnAttentePressurisation = false;

    public void MettreAJour()
    {
        // On vérifie qu'il y a au moins une ventilation connectée au sas
        if (Ventilations.Count == 0) return; 

        // L'oxygène est le même dans toute la pièce, on lit donc la valeur de la 1ère ventilation
        float niveauO2 = Ventilations[0].GetOxygenLevel();
        bool estEtanche = Ventilations[0].CanPressurize; 

        // --- 1. PORTE EXTÉRIEURE ---
        if (PorteExterieure.Status == DoorStatus.Opening)
        {
            if (niveauO2 > 0.01f) 
            {
                PorteExterieure.CloseDoor();     
                ReglerVentilations(true); // Lance l'aspiration sur TOUTES les ventilations
                EnAttenteDepressurisation = true;
                EnAttentePressurisation = false;
            }
            else 
            {
                FermerEtVerrouiller(PorteInterieure);
                EnAttenteDepressurisation = false;
            }
        }
        else if (PorteExterieure.Status == DoorStatus.Open)
        {
            FermerEtVerrouiller(PorteInterieure);
        }

        // --- 2. PORTE INTÉRIEURE ---
        if (PorteInterieure.Status == DoorStatus.Opening)
        {
            if (niveauO2 < 0.99f && estEtanche) 
            {
                PorteInterieure.CloseDoor();      
                ReglerVentilations(false); // Lance le remplissage sur TOUTES les ventilations
                EnAttentePressurisation = true;
                EnAttenteDepressurisation = false;
            }
            else 
            {
                FermerEtVerrouiller(PorteExterieure); 
                EnAttentePressurisation = false;
            }
        }
        else if (PorteInterieure.Status == DoorStatus.Open)
        {
            FermerEtVerrouiller(PorteExterieure);
        }

        // --- 3. OUVERTURE AUTOMATIQUE ---
        if (PorteInterieure.Status == DoorStatus.Closed && PorteExterieure.Status == DoorStatus.Closed)
        {
            PorteInterieure.Enabled = true;
            PorteExterieure.Enabled = true;

            if (EnAttenteDepressurisation && niveauO2 <= 0.01f)
            {
                PorteExterieure.OpenDoor(); 
                EnAttenteDepressurisation = false;
            }
            
            if (EnAttentePressurisation && (niveauO2 >= 0.99f || !estEtanche))
            {
                PorteInterieure.OpenDoor(); 
                EnAttentePressurisation = false;
            }
        }
        
        AfficherStatut(niveauO2, estEtanche);
    }

    // NOUVEAU : Commande toutes les ventilations du sas d'un coup
    void ReglerVentilations(bool depressuriser)
    {
        foreach(var vent in Ventilations)
        {
            vent.Depressurize = depressuriser;
        }
    }

    void FermerEtVerrouiller(IMyDoor porte)
    {
        if (porte == null) return;
        porte.CloseDoor();
        porte.Enabled = false; 
    }

    void AfficherStatut(float niveauO2, bool estEtanche)
    {
        if (Ecrans.Count == 0) return;

        string statut = "PRÊT - EN ATTENTE";
        if (EnAttenteDepressurisation) statut = "DÉPRESSURISATION (Création du vide)";
        else if (EnAttentePressurisation) statut = "PRESSURISATION (Remplissage O2)";
        else if (!estEtanche) statut = "ALERTE : FUITE DÉTECTÉE (Non étanche)";

        string texte = $"=== CONTRÔLE SAS : {NomDuSas.ToUpper()} ===\n\n";
        texte += $"STATUT : {statut}\n";
        texte += $"OXYGÈNE : {(niveauO2 * 100):0.0} %\n\n";
        
        texte += "--- SÉCURITÉ DES PORTES ---\n";
        texte += $"Intérieure (Base) : {ObtenirEtatPorte(PorteInterieure)}\n";
        texte += $"Extérieure (Espace): {ObtenirEtatPorte(PorteExterieure)}\n";

        foreach (var ecran in Ecrans)
        {
            ecran.ContentType = SpaceEngineers.Game.ModAPI.Ingame.ContentType.TEXT_AND_IMAGE;
            ecran.WriteText(texte);
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
// LE COEUR DU PROGRAMME
// ------------------------------------------------------------------------------
List<GestionnaireSas> ListeDesSas = new List<GestionnaireSas>();

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10;
    ScannerLesSas();
}

public void Main(string argument, UpdateType updateSource)
{
    if (argument.ToLower() == "rescan")
    {
        ScannerLesSas();
        Echo("Scan forcé terminé.");
    }

    foreach (var sas in ListeDesSas)
    {
        sas.MettreAJour();
    }
    
    Echo($"Airlock Intelligent Actif.\nNombre de sas gérés : {ListeDesSas.Count}");
}

// ------------------------------------------------------------------------------
// LE SCANNER
// ------------------------------------------------------------------------------
void ScannerLesSas()
{
    ListeDesSas.Clear();
    Dictionary<string, GestionnaireSas> sasTemporaires = new Dictionary<string, GestionnaireSas>();
    List<IMyTerminalBlock> tousLesBlocs = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(tousLesBlocs);

    foreach (var bloc in tousLesBlocs)
    {
        string nomDuBloc = bloc.CustomName;
        
        if (nomDuBloc.Contains("[SAS-"))
        {
            int indexDebut = nomDuBloc.IndexOf("[SAS-") + 5;
            int indexFin = nomDuBloc.IndexOf("]", indexDebut);
            
            if (indexFin == -1) continue;

            string contenuCrochets = nomDuBloc.Substring(indexDebut, indexFin - indexDebut); 
            int indexTiret = contenuCrochets.LastIndexOf('-'); 
            if (indexTiret == -1) continue;

            string nomDuSas = contenuCrochets.Substring(0, indexTiret); 
            string roleDuBloc = contenuCrochets.Substring(indexTiret + 1).ToLower(); 

            if (!sasTemporaires.ContainsKey(nomDuSas))
            {
                sasTemporaires[nomDuSas] = new GestionnaireSas();
                sasTemporaires[nomDuSas].NomDuSas = nomDuSas;
            }

            if (roleDuBloc == "int" && bloc is IMyDoor) 
                sasTemporaires[nomDuSas].PorteInterieure = (IMyDoor)bloc;
                
            else if (roleDuBloc == "ext" && bloc is IMyDoor) 
                sasTemporaires[nomDuSas].PorteExterieure = (IMyDoor)bloc;
                
            //On utilise .Add() pour ajouter toutes les ventilations trouvées à la liste
            else if (roleDuBloc == "vent" && bloc is IMyAirVent) 
                sasTemporaires[nomDuSas].Ventilations.Add((IMyAirVent)bloc);
                
            else if (roleDuBloc == "lcd" && bloc is IMyTextPanel)
                sasTemporaires[nomDuSas].Ecrans.Add((IMyTextPanel)bloc);
        }
    }

    foreach (var sas in sasTemporaires.Values)
    {
        // Un sas n'est valide que s'il a ses deux portes ET au moins une ventilation
        if (sas.PorteInterieure != null && sas.PorteExterieure != null && sas.Ventilations.Count > 0)
        {
            ListeDesSas.Add(sas);
        }
    }
}
```
# Documentation : Script de Contrôle des Avaries & Alarmes

Ce document détaille le fonctionnement du script de **Damage Control** pour Space Engineers. Ce script agit comme un microservice au sein d'une architecture de "Data Center", en isolant la gestion des dégâts et des alarmes dans un Bloc Programmable dédié.

## 1. Prérequis et Installation en jeu

Pour que le script fonctionne de manière autonome, il faut préparer les blocs terminaux du vaisseau :

1. **Les Écrans (LCD Panels)** :
   - Construire un ou plusieurs écrans LCD.
   - Ouvrir le terminal, sélectionner l'écran et cliquer sur **Données Personnalisées (Custom Data)**.
   - Coller exactement ceci pour que le script reconnaisse l'écran :
     ```ini
     [Ecran]
     Role=Avaries
     ```
2. **Les Alarmes (Gyrophares, Sirènes, Portes blindées)** :
   - Construire les blocs d'alerte (ex: Rotating Light).
   - Les sélectionner dans le terminal.
   - Créer un groupe nommé **exactement** `ALARMES_CRITIQUES`.
   - Éteindre ces blocs manuellement (le script s'occupera de les allumer).

---

## 2. Le Script C# Complet (Hautement commenté)

À copier-coller dans un Bloc Programmable dédié au Contrôle des Avaries.

```csharp
// ==============================================================================
// MICROSERVICE : CONTRÔLE DES AVARIES & ALARMES
// Objectif : Scanner l'intégrité des blocs fonctionnels et gérer les alertes.
// ==============================================================================

// Déclaration de l'outil de lecture INI (intégré à l'API du jeu)
// Il nous permet de lire facilement les Custom Data des blocs.
MyIni lecteurIni = new MyIni();

// Le Constructeur (Program) est appelé une seule fois lors de la compilation du script.
public Program()
{
    // On configure le script pour qu'il s'exécute automatiquement.
    // Update100 = exécution tous les 100 tics physiques du moteur de jeu (environ 1.6 secondes).
    // C'est le compromis parfait : le script est réactif, mais ne fait pas lagger le serveur.
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

// La méthode Main est la boucle principale qui se répète.
public void Main(string argument, UpdateType updateSource)
{
    // --------------------------------------------------------------------------
    // ÉTAPE 1 : CIBLER LES ÉCRANS LCD DÉDIÉS AUX AVARIES
    // --------------------------------------------------------------------------
    
    List<IMyTextPanel> ecransAvaries = new List<IMyTextPanel>();
    List<IMyTextPanel> tousLesEcrans = new List<IMyTextPanel>();
    
    // On demande au système du vaisseau de lister TOUS les écrans LCD présents.
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(tousLesEcrans);

    foreach (var ecran in tousLesEcrans)
    {
        // On tente de lire les Custom Data de chaque écran
        if (lecteurIni.TryParse(ecran.CustomData))
        {
            // On vérifie la section [Ecran] et la clé "Role"
            string role = lecteurIni.Get("Ecran", "Role").ToString("").ToLower();
            
            // Si le rôle est bien "avaries", on ajoute cet écran à notre liste de cibles.
            if (role == "avaries")
            {
                ecransAvaries.Add(ecran);
            }
        }
    }

    // --------------------------------------------------------------------------
    // ÉTAPE 2 : SCANNER LES DÉGÂTS SUR L'ENSEMBLE DU VAISSEAU
    // --------------------------------------------------------------------------
    
    List<IMyTerminalBlock> tousLesBlocs = new List<IMyTerminalBlock>();
    
    // On liste TOUS les blocs terminaux (ceux qui ont une interface de contrôle)
    GridTerminalSystem.GetBlocks(tousLesBlocs);
    
    // Listes pour trier la gravité des dégâts
    List<string> listeCritiques = new List<string>();
    List<string> listeAvertissements = new List<string>();

    foreach (var bloc in tousLesBlocs)
    {
        // IMySlimBlock représente la structure physique (les composants, le blindage) du bloc.
        // C'est nécessaire car IMyTerminalBlock ne gère que la partie logicielle (on/off, nom...).
        IMySlimBlock structure = bloc.CubeGrid.GetCubeBlock(bloc.Position);
        
        // On vérifie si la structure a pris des dégâts (CurrentDamage > 0 indique une perte de points de vie).
        if (structure != null && structure.CurrentDamage > 0)
        {
            // Calcul mathématique de l'intégrité
            // BuildIntegrity = PV actuels si le bloc était sain
            // MaxIntegrity = PV totaux du bloc
            float santeActuelle = structure.BuildIntegrity - structure.CurrentDamage;
            float pourcentage = (santeActuelle / structure.MaxIntegrity) * 100f;
            
            // IsFunctional vérifie si le bloc a passé la fameuse "ligne rouge" de piratage/dégâts.
            // S'il n'est plus fonctionnel, la situation est critique.
            if (!bloc.IsFunctional)
            {
                // On ajoute le nom personnalisé du bloc pour savoir exactement lequel réparer.
                listeCritiques.Add($"[CRITIQUE] {bloc.CustomName} H.S.");
            }
            else
            {
                // Le bloc fonctionne encore, mais on prévient l'ingénieur de l'usure.
                // ":0" formate le chiffre pour ne pas afficher de décimales (ex: 85% au lieu de 85.345%).
                listeAvertissements.Add($"[DÉGÂTS] {bloc.CustomName} : {pourcentage:0}%");
            }
        }
    }

    // --------------------------------------------------------------------------
    // ÉTAPE 3 : GESTION DES ALARMES (Blocs Groupés)
    // --------------------------------------------------------------------------
    
    // Si la liste des erreurs critiques contient au moins 1 élément, l'alarme doit s'activer.
    bool alarmeRequise = (listeCritiques.Count > 0);
    
    // On cherche le groupe d'alarmes défini par l'utilisateur dans le terminal.
    IMyBlockGroup groupeAlarmes = GridTerminalSystem.GetBlockGroupWithName("ALARMES_CRITIQUES");
    
    if (groupeAlarmes != null)
    {
        List<IMyFunctionalBlock> blocsAlarme = new List<IMyFunctionalBlock>();
        
        // IMyFunctionalBlock englobe tout ce qui peut être Allumé (Enabled = true) ou Éteint (Enabled = false).
        groupeAlarmes.GetBlocksOfType<IMyFunctionalBlock>(blocsAlarme);
        
        foreach (var blocAlarme in blocsAlarme)
        {
            // Si alarmeRequise = true  -> Allume le bloc (gyrophare, sirène)
            // Si alarmeRequise = false -> Éteint le bloc
            blocAlarme.Enabled = alarmeRequise;
        }
    }

    // Optimisation : Si le script n'a trouvé aucun écran pour afficher le texte,
    // on stoppe la méthode ici pour économiser la puissance de calcul du processeur.
    // (Les alarmes, elles, ont déjà été gérées juste au-dessus !).
    if (ecransAvaries.Count == 0) return;

    // --------------------------------------------------------------------------
    // ÉTAPE 4 : CRÉATION DU RAPPORT VISUEL (Formatage du texte)
    // --------------------------------------------------------------------------
    
    string rapportFinal = "=== ÉTAT DES SYSTÈMES ===\n\n";

    if (listeCritiques.Count == 0 && listeAvertissements.Count == 0)
    {
        // Tout va bien
        rapportFinal += "   [ SYSTÈMES NOMINAUX ]\n   Intégrité à 100%\n   Aucune avarie détectée.";
    }
    else
    {
        // Ajout des pannes majeures en premier
        if (listeCritiques.Count > 0)
        {
            rapportFinal += "!!! PANNES MAJEURES !!!\n";
            // string.Join relie tous les éléments de la liste avec un saut de ligne (\n)
            rapportFinal += string.Join("\n", listeCritiques) + "\n\n";
        }
        
        // Ajout des dégâts mineurs ensuite
        if (listeAvertissements.Count > 0)
        {
            rapportFinal += "--- RÉPARATIONS REQUISES ---\n";
            rapportFinal += string.Join("\n", listeAvertissements);
        }
    }

    // --------------------------------------------------------------------------
    // ÉTAPE 5 : AFFICHAGE SUR LES ÉCRANS
    // --------------------------------------------------------------------------
    
    foreach (var ecran in ecransAvaries)
    {
        // On s'assure que l'écran est bien en mode "Affichage de texte" et pas en mode "Image/Texture"
        ecran.ContentType = SpaceEngineers.Game.ModAPI.Ingame.ContentType.TEXT_AND_IMAGE;
        
        // On pousse le texte généré vers la dalle LCD
        ecran.WriteText(rapportFinal);
    }
}
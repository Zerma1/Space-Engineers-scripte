// ==============================================================================
// DATA CENTER : MONITORING DES STOCKS ET SYSTÈMES
// ==============================================================================

MyIni lecteurIni = new MyIni();
int compteurTicksGlobal = 0;
const int TICKS_PAR_PAGE = 5; // Définit la vitesse de rotation des écrans

// Dictionnaire de traduction (Anglais -> Français)
Dictionary<string, string> DicoAffichage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

// Modèle de données pour un écran
class ConfigEcran
{
    public IMyTextPanel BlocEcran;
    public string Mode; 
    public int PageFixe;
    public int TotalPages;
    public List<string> Inclusions = new List<string>();
    public List<string> Exclusions = new List<string>();
    public bool AfficherGaz;
    public bool AfficherEnergie;
}

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    InitialiserTraduction();
}

// Connexion au serveur pour récupérer la liste de traduction
void InitialiserTraduction()
{
    IMyTerminalBlock serveur = GridTerminalSystem.GetBlockWithName("SERVEUR_DICTIONNAIRE");
    
    if (serveur == null) {
        Echo("Info : SERVEUR_DICTIONNAIRE introuvable. Noms originaux utilisés.");
        return;
    }

    if (lecteurIni.TryParse(serveur.CustomData))
    {
        List<MyIniKey> cles = new List<MyIniKey>();
        lecteurIni.GetKeys("Affichage", cles); // On ne prend que la section [Affichage]
        
        foreach (var cle in cles)
        {
            string nomJoli = lecteurIni.Get("Affichage", cle.Name).ToString();
            DicoAffichage[cle.Name] = nomJoli; // "Iron" devient "Fer"
        }
    }
}

public void Main(string argument, UpdateType updateSource)
{
    compteurTicksGlobal++;
    if (compteurTicksGlobal >= TICKS_PAR_PAGE) compteurTicksGlobal = 0;

    // 1. CHERCHER TOUS LES ÉCRANS CONFIGURÉS
    List<ConfigEcran> ecransConfigures = new List<ConfigEcran>();
    List<IMyTextPanel> tousLesEcrans = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(tousLesEcrans);

    foreach (var ecran in tousLesEcrans)
    {
        // Si l'écran a une section [Ecran] dans ses Custom Data
        if (lecteurIni.TryParse(ecran.CustomData) && lecteurIni.ContainsSection("Ecran"))
        {
            ConfigEcran config = new ConfigEcran();
            config.BlocEcran = ecran;
            config.Mode = lecteurIni.Get("Ecran", "Mode").ToString("Fixe");
            config.PageFixe = lecteurIni.Get("Ecran", "Page").ToInt32(1);
            config.TotalPages = lecteurIni.Get("Ecran", "TotalPages").ToInt32(1);
            config.AfficherGaz = lecteurIni.Get("Ecran", "AfficherGaz").ToBoolean();
            config.AfficherEnergie = lecteurIni.Get("Ecran", "AfficherEnergie").ToBoolean();

            string catStr = lecteurIni.Get("Ecran", "Categories").ToString("");
            if (!string.IsNullOrEmpty(catStr)) config.Inclusions = new List<string>(catStr.Split(','));

            string exStr = lecteurIni.Get("Ecran", "Exclusions").ToString("");
            if (!string.IsNullOrEmpty(exStr)) config.Exclusions = new List<string>(exStr.Split(','));

            // Nettoyage des listes (minuscules, retrait des espaces)
            for (int i = 0; i < config.Inclusions.Count; i++) config.Inclusions[i] = config.Inclusions[i].Trim().ToLower();
            for (int i = 0; i < config.Exclusions.Count; i++) config.Exclusions[i] = config.Exclusions[i].Trim().ToLower();

            ecransConfigures.Add(config);
        }
    }

    if (ecransConfigures.Count == 0) return; 

    // 2. RÉCOLTER LES DONNÉES DU VAISSEAU
    Dictionary<string, double> toutesRessources = CompterRessources();
    string rapportGaz = CompterGaz();
    string rapportEnergie = CompterEnergie();

    // 3. CRÉATION DES AFFICHAGES
    foreach (var config in ecransConfigures)
    {
        List<string> lignesAAfficher = new List<string>();

        if (config.AfficherGaz) lignesAAfficher.Add(rapportGaz);
        if (config.AfficherEnergie) lignesAAfficher.Add(rapportEnergie);

        foreach (var ressource in toutesRessources)
        {
            string[] parties = ressource.Key.Split(':');
            string categorie = parties[0].ToLower();
            string nomInterne = parties[1]; 

            // Filtres depuis le fichier INI
            bool estInclus = config.Inclusions.Count == 0 || config.Inclusions.Contains(categorie);
            bool estExclu = config.Exclusions.Contains(nomInterne.ToLower());

            if (estInclus && !estExclu)
            {
                // Traduction du nom
                string nomAAfficher = nomInterne; 
                if (DicoAffichage.ContainsKey(nomInterne))
                {
                    nomAAfficher = DicoAffichage[nomInterne];
                }

                lignesAAfficher.Add($"{nomAAfficher} : {ressource.Value:N0}");
            }
        }

        // Configuration du panneau LCD
        string texteFinal = "";
        config.BlocEcran.ContentType = SpaceEngineers.Game.ModAPI.Ingame.ContentType.TEXT_AND_IMAGE;

        // Comportement Fixe (Pagination stricte)
        if (config.Mode.ToLower() == "fixe")
        {
            int tailleChunk = (int)Math.Ceiling(lignesAAfficher.Count / (double)config.TotalPages);
            int indexDepart = (config.PageFixe - 1) * tailleChunk;
            var lignesFiltrees = lignesAAfficher.Skip(indexDepart).Take(tailleChunk);
            
            texteFinal = $"=== DATA CENTER (Page {config.PageFixe}/{config.TotalPages}) ===\n";
            texteFinal += string.Join("\n", lignesFiltrees);
        }
        // Comportement Rotatif (Défilement automatique)
        else 
        {
            int lignesParPage = 10; // Nombre max d'items avant de passer à la page suivante
            int totalPagesRot = (int)Math.Ceiling(lignesAAfficher.Count / (double)lignesParPage);
            if (totalPagesRot == 0) totalPagesRot = 1;

            // Astuce : utilise les secondes réelles du serveur pour synchroniser la rotation
            int pageActuelle = (int)(Math.Floor(DateTime.Now.Second / 8.0) % totalPagesRot);

            var lignesFiltrees = lignesAAfficher.Skip(pageActuelle * lignesParPage).Take(lignesParPage);
            
            texteFinal = $"=== ROTATION ({pageActuelle + 1}/{totalPagesRot}) ===\n";
            texteFinal += string.Join("\n", lignesFiltrees);
        }

        config.BlocEcran.WriteText(texteFinal);
    }
}

// ------------------------------------------------------------------------------
// OUTILS DE COLLECTE
// ------------------------------------------------------------------------------

Dictionary<string, double> CompterRessources()
{
    Dictionary<string, double> invGlobal = new Dictionary<string, double>();
    List<IMyTerminalBlock> blocs = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(blocs);

    foreach (var bloc in blocs)
    {
        if (!bloc.HasInventory) continue;
        for (int i = 0; i < bloc.InventoryCount; i++)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            bloc.GetInventory(i).GetItems(items);

            foreach (var item in items)
            {
                string cat = item.Type.TypeId.ToString().Replace("MyObjectBuilder_", "");
                string cle = $"{cat}:{item.Type.SubtypeId}"; // ex: "ore:Iron"
                
                if (!invGlobal.ContainsKey(cle)) invGlobal[cle] = 0;
                invGlobal[cle] += (double)item.Amount;
            }
        }
    }
    return invGlobal;
}

string CompterGaz()
{
    List<IMyGasTank> reservoirs = new List<IMyGasTank>();
    GridTerminalSystem.GetBlocksOfType<IMyGasTank>(reservoirs);

    double o2Cap = 0, o2Actuel = 0, h2Cap = 0, h2Actuel = 0;

    foreach(var res in reservoirs)
    {
        if (res.BlockDefinition.SubtypeId.Contains("Oxygen")) {
            o2Cap += res.Capacity; o2Actuel += res.Capacity * res.FilledRatio;
        } else {
            h2Cap += res.Capacity; h2Actuel += res.Capacity * res.FilledRatio;
        }
    }

    string txt = "--- RÉSERVES DE GAZ ---\n";
    if(o2Cap > 0) txt += $"Oxygène : {(o2Actuel/o2Cap)*100:0.0}% ({o2Actuel:N0} L)\n";
    if(h2Cap > 0) txt += $"Hydrogène : {(h2Actuel/h2Cap)*100:0.0}% ({h2Actuel:N0} L)\n";
    return txt + "\n";
}

string CompterEnergie()
{
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
    
    double chargeActuelle = 0, chargeMax = 0, entreeTotal = 0, sortieTotal = 0;
    foreach(var bat in batteries) {
        chargeActuelle += bat.CurrentStoredPower; chargeMax += bat.MaxStoredPower;
        entreeTotal += bat.CurrentInput; sortieTotal += bat.CurrentOutput;
    }

    List<IMyPowerProducer> producteurs = new List<IMyPowerProducer>();
    GridTerminalSystem.GetBlocksOfType<IMyPowerProducer>(producteurs);

    double solaire = 0, reacteur = 0, moteurH2 = 0;
    foreach(var prod in producteurs)
    {
        string type = prod.BlockDefinition.TypeIdString;
        if (type.Contains("SolarPanel")) solaire += prod.CurrentOutput;
        if (type.Contains("Reactor")) reacteur += prod.CurrentOutput;
        if (type.Contains("HydrogenEngine")) moteurH2 += prod.CurrentOutput;
    }

    string txt = "--- GESTION ÉNERGIE ---\n";
    if (chargeMax > 0) txt += $"Batteries : {(chargeActuelle/chargeMax)*100:0.0}% ({chargeActuelle:0.0} MWh)\n";
    txt += $"Charge In: {entreeTotal:0.0} MW | Out: {sortieTotal:0.0} MW\n";
    txt += $"Solaire: {solaire:0.0} MW | Réacteurs: {reacteur:0.0} MW\n";
    return txt + "\n";
}
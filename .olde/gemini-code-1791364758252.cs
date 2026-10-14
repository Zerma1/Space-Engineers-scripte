// ==============================================================================
// TRI D'INVENTAIRE AUTOMATIQUE - CLIENT DATA CENTER
// ==============================================================================

// Dictionnaire qui fera la liaison entre les mots français et le code du jeu
Dictionary<string, string> DicoRessources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

// Outil natif de Space Engineers pour lire les configurations au format INI
MyIni lecteurIni = new MyIni();

// Structure pour mémoriser les règles de chaque coffre
class ConteneurCible
{
    public IMyCargoContainer Bloc;
    public string Categorie; // ex: "ore", "ingot"
    public int Priorite;     // 0 = la plus haute
    public bool AccepteTout; // Vrai si le tag '*-' est présent
    public List<string> Inclusions = new List<string>();
    public List<string> Exclusions = new List<string>();
}

// Liste en mémoire de tous les coffres configurés
List<ConteneurCible> ListeConteneurs = new List<ConteneurCible>();

public Program()
{
    // 1. RECHERCHE DU SERVEUR DE DONNÉES
    IMyTerminalBlock blocServeur = GridTerminalSystem.GetBlockWithName("SERVEUR_DICTIONNAIRE");

    if (blocServeur == null)
    {
        Echo("ERREUR : 'SERVEUR_DICTIONNAIRE' introuvable.");
        return;
    }

    // 2. LECTURE DU DICTIONNAIRE
    MyIniParseResult resultat;
    if (lecteurIni.TryParse(blocServeur.CustomData, out resultat))
    {
        List<MyIniKey> cles = new List<MyIniKey>();
        lecteurIni.GetKeys("Dictionnaire", cles);

        foreach (var cle in cles)
        {
            string valeur = lecteurIni.Get("Dictionnaire", cle.Name).ToString();
            DicoRessources[cle.Name] = valeur; // ex: DicoRessources["fer"] = "Iron"
        }
    }

    // 3. ACTIVATION DE LA BOUCLE
    // Exécute le script tous les 100 tics (~1.6s) pour ne pas lagger
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    // Réinitialise la liste à chaque passage pour prendre en compte les nouveaux coffres
    ListeConteneurs.Clear(); 
    
    List<IMyTerminalBlock> tousLesBlocs = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(tousLesBlocs);
    
    // ÉTAPE 1 : Scanner et mémoriser les conteneurs avec une syntaxe spéciale
    foreach (var bloc in tousLesBlocs)
    {
        if (bloc is IMyCargoContainer && bloc.CustomName.Contains("{") && bloc.CustomName.Contains("}"))
        {
            AnalyserFormat((IMyCargoContainer)bloc, bloc.CustomName);
        }
    }
    
    // ÉTAPE 2 : Déplacer les objets depuis tous les inventaires du vaisseau
    foreach (var bloc in tousLesBlocs)
    {
        TrierInventaire(bloc);
    }
}

// Fonction de traduction utilisant le dictionnaire
string NormaliserNom(string entree)
{
    entree = entree.Trim();
    if (DicoRessources.ContainsKey(entree)) return DicoRessources[entree];
    return entree;
}

// Décrypte le nom du coffre (ex: {ore}[*-stone]0 )
void AnalyserFormat(IMyCargoContainer conteneur, string nom)
{
    int debutCat = nom.IndexOf('{');
    int finCat = nom.IndexOf('}');
    int debutItems = nom.IndexOf('[');
    int finItems = nom.IndexOf(']');
    
    if (debutCat < finCat && debutItems < finItems && finCat < debutItems) 
    {
        ConteneurCible cible = new ConteneurCible();
        cible.Bloc = conteneur;
        cible.Categorie = nom.Substring(debutCat + 1, finCat - debutCat - 1).ToLower();
        
        string contenuBrut = nom.Substring(debutItems + 1, finItems - debutItems - 1).Trim();
        string strPriorite = nom.Substring(finItems + 1).Trim();
        
        cible.Priorite = 99; // Priorité faible par défaut
        int.TryParse(strPriorite, out cible.Priorite);

        // Analyse du mode (Tout accepter sauf... ou Accepter uniquement...)
        if (contenuBrut.StartsWith("*-"))
        {
            cible.AccepteTout = true;
            contenuBrut = contenuBrut.Substring(2);
        }
        else if (contenuBrut == "*")
        {
            cible.AccepteTout = true;
            contenuBrut = "";
        }

        if (!string.IsNullOrWhiteSpace(contenuBrut))
        {
            string[] elements = contenuBrut.Split(';');
            foreach(string element in elements)
            {
                if (cible.AccepteTout) 
                    cible.Exclusions.Add(NormaliserNom(element));
                else 
                    cible.Inclusions.Add(NormaliserNom(element));
            }
        }
        ListeConteneurs.Add(cible);
    }
}

// Détermine où un objet doit aller
ConteneurCible TrouverMeilleurConteneur(string categorieObjet, string nomObjet, IMyTerminalBlock blocSource)
{
    ConteneurCible meilleur = null;
    bool sourceEstUnBonCoffre = false;
    int prioriteDeLaSource = 999;

    foreach (var dest in ListeConteneurs)
    {
        if (dest.Categorie != categorieObjet) continue;

        bool estAccepte = false;
        if (dest.AccepteTout) {
            if (!dest.Exclusions.Contains(nomObjet)) estAccepte = true;
        } else {
            if (dest.Inclusions.Contains(nomObjet)) estAccepte = true;
        }

        if (estAccepte)
        {
            if (dest.Bloc == blocSource) 
            {
                sourceEstUnBonCoffre = true;
                prioriteDeLaSource = dest.Priorite;
            }

            if (meilleur == null || dest.Priorite < meilleur.Priorite)
            {
                meilleur = dest;
            }
        }
    }

    // Sécurité Anti Ping-Pong : on ne déplace pas si l'objet est déjà bien placé
    if (sourceEstUnBonCoffre && meilleur != null && prioriteDeLaSource <= meilleur.Priorite) {
        return null; 
    }

    return meilleur;
}

// Effectue le transfert physique via l'API IMyInventory
void TrierInventaire(IMyTerminalBlock blocSource)
{
    if (!blocSource.HasInventory) return;

    for (int i = 0; i < blocSource.InventoryCount; i++)
    {
        IMyInventory inventaireSource = blocSource.GetInventory(i);
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        inventaireSource.GetItems(items);

        // Boucle inversée indispensable lors de transferts d'inventaire
        for (int j = items.Count - 1; j >= 0; j--)
        {
            MyInventoryItem item = items[j];

            string categorieBrute = item.Type.TypeId.ToString();
            string categorieObjet = categorieBrute.Replace("MyObjectBuilder_", "").ToLower();
            string nomObjet = item.Type.SubtypeId.ToString();

            ConteneurCible destination = TrouverMeilleurConteneur(categorieObjet, nomObjet, blocSource);

            if (destination != null && destination.Bloc != blocSource)
            {
                IMyInventory inventaireDest = destination.Bloc.GetInventory(0);
                inventaireSource.TransferItemTo(inventaireDest, item, null); // null = toute la pile
            }
        }
    }
}